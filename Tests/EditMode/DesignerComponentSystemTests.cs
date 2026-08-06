using System.Collections.Generic;
using System.Linq;
using emiteat.NexUI.Designer.Editor;
using emiteat.NexUI.Designer.Editor.Components.Definitions;
using emiteat.NexUI.Designer.Editor.Properties;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Phase 3 (reusable components): expansion identity, override/variant resolution, slot routing,
    /// failure reporting and - most importantly - that expanding never mutates authored data.
    /// All pure logic; no window, AssetDatabase or Undo required, so the whole file runs in EditMode.
    /// </summary>
    public sealed class DesignerComponentSystemTests
    {
        // ---- Fixtures ---------------------------------------------------------------------

        private sealed class StubResolver : IDesignerComponentDefinitionResolver
        {
            private readonly Dictionary<string, DesignerComponentDefinitionAsset> _byGuid = new();

            public StubResolver Add(string guid, DesignerComponentDefinitionAsset definition)
            {
                _byGuid[guid] = definition;
                return this;
            }

            public DesignerComponentDefinitionAsset Resolve(string definitionGuid, string definitionId)
                => !string.IsNullOrEmpty(definitionGuid) && _byGuid.TryGetValue(definitionGuid, out var d) ? d : null;
        }

        private static DesignerMetadataAsset NewScreen()
        {
            var asset = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            asset.schemaVersion = DesignerMetadataAsset.CurrentSchemaVersion;
            return asset;
        }

        private static DesignerElementMetadata Add(DesignerMetadataAsset asset, string id, string parent = null,
            string slot = null, Rect? rect = null)
        {
            var e = new DesignerElementMetadata
            {
                elementId = id,
                stableId = "stable-" + id,
                parentId = parent,
                parentSlotId = slot,
                rect = rect ?? new Rect(0, 0, 100, 40)
            };
            asset.elements.Add(e);
            return e;
        }

        /// <summary>Card = root panel with a title label and a body container that hosts the "content" slot.</summary>
        private static DesignerComponentDefinitionAsset NewCardDefinition()
        {
            var definition = ScriptableObject.CreateInstance<DesignerComponentDefinitionAsset>();
            definition.componentId = "card-component";
            definition.displayName = "Card";
            definition.version = 1;
            definition.rootElementId = "root";
            definition.elements.Add(new DesignerElementMetadata
            {
                elementId = "root", stableId = "def-root", elementType = "Panel",
                rect = new Rect(0, 0, 200, 120), tint = Color.gray
            });
            definition.elements.Add(new DesignerElementMetadata
            {
                elementId = "title", stableId = "def-title", parentId = "root", elementType = "Label",
                rect = new Rect(8, 8, 180, 24), text = "Title"
            });
            definition.elements.Add(new DesignerElementMetadata
            {
                elementId = "body", stableId = "def-body", parentId = "root", elementType = "Container",
                rect = new Rect(8, 40, 180, 72)
            });
            definition.slots.Add(new DesignerComponentSlotDefinition
            {
                slotId = "content", displayName = "Content", hostElementId = "body"
            });
            definition.exposedProperties.Add(new DesignerComponentExposedProperty
            {
                propertyName = "title", displayName = "Title",
                targetElementId = "title", propertyId = DesignerPropertyId.Text
            });
            return definition;
        }

        private static DesignerElementMetadata AddInstance(DesignerMetadataAsset screen, string id, string guid,
            DesignerComponentDefinitionAsset definition, Rect? rect = null)
        {
            var element = Add(screen, id, rect: rect ?? new Rect(50, 60, 200, 120));
            element.componentInstance = new DesignerComponentInstanceMetadata
            {
                definitionGuid = guid,
                definitionId = definition.componentId,
                definitionVersion = definition.version
            };
            return element;
        }

        private static DesignerElementMetadata Element(DesignerComponentExpansion expansion, string id)
            => expansion.Expanded.elements.FirstOrDefault(e => e.elementId == id);

        // ---- Pass-through -----------------------------------------------------------------

        [Test]
        public void Expand_WithoutInstances_ReturnsAuthoredAssetUnchanged()
        {
            var screen = NewScreen();
            Add(screen, "panel");

            var expansion = DesignerComponentExpander.Expand(screen, new StubResolver());

            Assert.IsFalse(expansion.ContainsInstances);
            Assert.AreSame(screen, expansion.Expanded, "A screen with no instances must not pay for a copy.");
            expansion.Dispose();
        }

        // ---- Identity ---------------------------------------------------------------------

        [Test]
        public void Expand_InstanceBecomesDefinitionRoot_KeepingItsOwnIdentityAndPlacement()
        {
            var definition = NewCardDefinition();
            var screen = NewScreen();
            var instance = AddInstance(screen, "card1", "guid-card", definition, new Rect(50, 60, 200, 120));

            var expansion = DesignerComponentExpander.Expand(screen, new StubResolver().Add("guid-card", definition));

            var root = Element(expansion, "card1");
            Assert.IsNotNull(root, "The instance element must remain, not be wrapped in a new object.");
            Assert.AreEqual(instance.stableId, root.stableId, "Keeping the stableId is what reconnects the prefab object across saves.");
            Assert.AreEqual("Panel", root.elementType, "The root takes the definition root's type.");
            Assert.AreEqual(new Rect(50, 60, 200, 120), root.rect);
            expansion.Dispose();
        }

        [Test]
        public void Expand_GeneratesPrefixedChildrenOffsetToTheInstancePosition()
        {
            var definition = NewCardDefinition();
            var screen = NewScreen();
            AddInstance(screen, "card1", "guid-card", definition, new Rect(50, 60, 200, 120));

            var expansion = DesignerComponentExpander.Expand(screen, new StubResolver().Add("guid-card", definition));

            var title = Element(expansion, "card1--title");
            Assert.IsNotNull(title);
            Assert.AreEqual("card1", title.parentId);
            // Definition title sits at (8,8); the instance origin is (50,60) and the definition root
            // origin is (0,0), so the offset is exactly the instance position.
            Assert.AreEqual(new Vector2(58, 68), title.rect.position);
            expansion.Dispose();
        }

        [Test]
        public void Expand_GeneratedStableIdsAreDeterministicAcrossExpansions()
        {
            var definition = NewCardDefinition();
            var screen = NewScreen();
            AddInstance(screen, "card1", "guid-card", definition);
            var resolver = new StubResolver().Add("guid-card", definition);

            var first = DesignerComponentExpander.Expand(screen, resolver);
            var firstId = Element(first, "card1--title").stableId;
            first.Dispose();

            var second = DesignerComponentExpander.Expand(screen, resolver);
            var secondId = Element(second, "card1--title").stableId;
            second.Dispose();

            Assert.AreEqual(firstId, secondId, "Regenerated ids would orphan the backend object on every save.");
            Assert.AreNotEqual("def-title", firstId, "Two instances of one definition must not share a stableId.");
        }

        [Test]
        public void Expand_TwoInstancesOfOneDefinitionGetDistinctStableIds()
        {
            var definition = NewCardDefinition();
            var screen = NewScreen();
            AddInstance(screen, "card1", "guid-card", definition);
            AddInstance(screen, "card2", "guid-card", definition);

            var expansion = DesignerComponentExpander.Expand(screen, new StubResolver().Add("guid-card", definition));

            Assert.AreNotEqual(Element(expansion, "card1--title").stableId, Element(expansion, "card2--title").stableId);
            expansion.Dispose();
        }

        // ---- Data safety ------------------------------------------------------------------

        [Test]
        public void Expand_NeverMutatesTheAuthoredAsset()
        {
            var definition = NewCardDefinition();
            var screen = NewScreen();
            var instance = AddInstance(screen, "card1", "guid-card", definition);
            instance.componentInstance.overrides.Add(new DesignerComponentPropertyOverride
            {
                exposedPropertyName = "title",
                value = new DesignerPropertyValue { type = DesignerPropertyValueType.String, stringValue = "Overridden" }
            });
            var authoredCount = screen.elements.Count;

            var expansion = DesignerComponentExpander.Expand(screen, new StubResolver().Add("guid-card", definition));

            Assert.AreEqual(authoredCount, screen.elements.Count, "Expansion must not add elements to the authored screen.");
            Assert.AreEqual("Title", definition.Find("title").text, "Applying an override must not write into the definition.");
            Assert.AreEqual(3, expansion.Expanded.elements.Count);
            expansion.Dispose();
        }

        // ---- Overrides --------------------------------------------------------------------

        [Test]
        public void Expand_AppliesExposedPropertyOverride()
        {
            var definition = NewCardDefinition();
            var screen = NewScreen();
            var instance = AddInstance(screen, "card1", "guid-card", definition);
            instance.componentInstance.overrides.Add(new DesignerComponentPropertyOverride
            {
                exposedPropertyName = "title",
                value = new DesignerPropertyValue { type = DesignerPropertyValueType.String, stringValue = "Hello" }
            });

            var expansion = DesignerComponentExpander.Expand(screen, new StubResolver().Add("guid-card", definition));

            Assert.AreEqual("Hello", Element(expansion, "card1--title").text);
            CollectionAssert.IsEmpty(expansion.Issues);
            expansion.Dispose();
        }

        [Test]
        public void Expand_ReportsOverrideThatNoLongerResolves()
        {
            var definition = NewCardDefinition();
            var screen = NewScreen();
            var instance = AddInstance(screen, "card1", "guid-card", definition);
            instance.componentInstance.overrides.Add(new DesignerComponentPropertyOverride
            {
                exposedPropertyName = "subtitle",
                value = new DesignerPropertyValue { type = DesignerPropertyValueType.String, stringValue = "x" }
            });

            var expansion = DesignerComponentExpander.Expand(screen, new StubResolver().Add("guid-card", definition));

            Assert.AreEqual(1, expansion.Issues.Count(i => i.Kind == DesignerComponentExpansionIssueKind.UnresolvedOverride));
            Assert.AreEqual("card1", expansion.Issues[0].InstanceElementId);
            expansion.Dispose();
        }

        // ---- Variants ---------------------------------------------------------------------

        [Test]
        public void Expand_VariantRuleAppliesOverridesAndVisibility_AndInstanceOverrideWins()
        {
            var definition = NewCardDefinition();
            definition.variantProperties.Add(new DesignerComponentVariantProperty
            {
                propertyName = "size", options = { "small", "large" }, defaultValue = "small"
            });
            definition.variantRules.Add(new DesignerComponentVariantRule
            {
                propertyName = "size", equalsValue = "large",
                hiddenElementIds = { "body" },
                overrides =
                {
                    new DesignerComponentPropertyOverride
                    {
                        exposedPropertyName = "title",
                        value = new DesignerPropertyValue { type = DesignerPropertyValueType.String, stringValue = "FromVariant" }
                    }
                }
            });

            var screen = NewScreen();
            var instance = AddInstance(screen, "card1", "guid-card", definition);
            instance.componentInstance.SetVariantSelection("size", "large");

            var resolver = new StubResolver().Add("guid-card", definition);
            var expansion = DesignerComponentExpander.Expand(screen, resolver);
            Assert.AreEqual("FromVariant", Element(expansion, "card1--title").text);
            Assert.IsFalse(Element(expansion, "card1--body").runtimeVisible);
            expansion.Dispose();

            instance.componentInstance.overrides.Add(new DesignerComponentPropertyOverride
            {
                exposedPropertyName = "title",
                value = new DesignerPropertyValue { type = DesignerPropertyValueType.String, stringValue = "FromInstance" }
            });
            var second = DesignerComponentExpander.Expand(screen, resolver);
            Assert.AreEqual("FromInstance", Element(second, "card1--title").text,
                "An instance override must beat a variant rule.");
            second.Dispose();
        }

        [Test]
        public void Expand_DefaultVariantSelectionAppliesWithoutAnExplicitSelection()
        {
            var definition = NewCardDefinition();
            definition.variantProperties.Add(new DesignerComponentVariantProperty
            {
                propertyName = "state", options = { "idle", "busy" }, defaultValue = "busy"
            });
            definition.variantRules.Add(new DesignerComponentVariantRule
            {
                propertyName = "state", equalsValue = "busy", hiddenElementIds = { "title" }
            });

            var screen = NewScreen();
            AddInstance(screen, "card1", "guid-card", definition);

            var expansion = DesignerComponentExpander.Expand(screen, new StubResolver().Add("guid-card", definition));

            Assert.IsFalse(Element(expansion, "card1--title").runtimeVisible);
            expansion.Dispose();
        }

        [Test]
        public void Expand_ReportsUnknownVariantSelection()
        {
            var definition = NewCardDefinition();
            var screen = NewScreen();
            var instance = AddInstance(screen, "card1", "guid-card", definition);
            instance.componentInstance.SetVariantSelection("ghost", "x");

            var expansion = DesignerComponentExpander.Expand(screen, new StubResolver().Add("guid-card", definition));

            Assert.IsTrue(expansion.Issues.Any(i => i.Kind == DesignerComponentExpansionIssueKind.UnknownVariantProperty));
            expansion.Dispose();
        }

        // ---- Slots ------------------------------------------------------------------------

        [Test]
        public void Expand_RoutesSlotContentToTheSlotHostElement()
        {
            var definition = NewCardDefinition();
            var screen = NewScreen();
            AddInstance(screen, "card1", "guid-card", definition);
            Add(screen, "userLabel", parent: "card1", slot: "content");

            var expansion = DesignerComponentExpander.Expand(screen, new StubResolver().Add("guid-card", definition));

            Assert.AreEqual("card1--body", Element(expansion, "userLabel").parentId,
                "Slot content belongs under the element the definition nominates, not the root.");
            expansion.Dispose();
        }

        [Test]
        public void Expand_UnknownSlotKeepsContentOnTheRootAndReports()
        {
            var definition = NewCardDefinition();
            var screen = NewScreen();
            AddInstance(screen, "card1", "guid-card", definition);
            Add(screen, "userLabel", parent: "card1", slot: "nope");

            var expansion = DesignerComponentExpander.Expand(screen, new StubResolver().Add("guid-card", definition));

            Assert.AreEqual("card1", Element(expansion, "userLabel").parentId);
            Assert.IsTrue(expansion.Issues.Any(i => i.Kind == DesignerComponentExpansionIssueKind.UnknownSlot));
            expansion.Dispose();
        }

        [Test]
        public void Expand_ReportsEmptyRequiredSlot()
        {
            var definition = NewCardDefinition();
            definition.FindSlot("content").required = true;
            var screen = NewScreen();
            AddInstance(screen, "card1", "guid-card", definition);

            var expansion = DesignerComponentExpander.Expand(screen, new StubResolver().Add("guid-card", definition));

            Assert.IsTrue(expansion.Issues.Any(i => i.Kind == DesignerComponentExpansionIssueKind.RequiredSlotEmpty));
            expansion.Dispose();
        }

        [Test]
        public void Expand_ReportsSlotTypeRejectionWithoutDroppingTheChild()
        {
            var definition = NewCardDefinition();
            definition.FindSlot("content").acceptedTypes.Add("Label");
            var screen = NewScreen();
            AddInstance(screen, "card1", "guid-card", definition);
            var child = Add(screen, "userButton", parent: "card1", slot: "content");
            child.elementType = "Button";

            var expansion = DesignerComponentExpander.Expand(screen, new StubResolver().Add("guid-card", definition));

            Assert.IsTrue(expansion.Issues.Any(i => i.Kind == DesignerComponentExpansionIssueKind.SlotRejectedType));
            Assert.IsNotNull(Element(expansion, "userButton"), "A rejected type is reported, never deleted.");
            expansion.Dispose();
        }

        // ---- Failure handling -------------------------------------------------------------

        [Test]
        public void Expand_MissingDefinitionKeepsTheInstanceAndItsChildren()
        {
            var definition = NewCardDefinition();
            var screen = NewScreen();
            AddInstance(screen, "card1", "guid-missing", definition);
            Add(screen, "userLabel", parent: "card1");

            var expansion = DesignerComponentExpander.Expand(screen, new StubResolver());

            Assert.IsTrue(expansion.Issues.Any(i => i.Kind == DesignerComponentExpansionIssueKind.MissingDefinition));
            Assert.IsNotNull(Element(expansion, "card1"));
            Assert.IsNotNull(Element(expansion, "userLabel"), "Authored slot content must survive a broken reference.");
            expansion.Dispose();
        }

        [Test]
        public void Expand_SelfReferencingDefinitionReportsACycleAndTerminates()
        {
            var definition = NewCardDefinition();
            definition.elements.Add(new DesignerElementMetadata
            {
                elementId = "inner", stableId = "def-inner", parentId = "root",
                componentInstance = new DesignerComponentInstanceMetadata
                {
                    definitionGuid = "guid-card", definitionId = definition.componentId, definitionVersion = 1
                }
            });

            var screen = NewScreen();
            AddInstance(screen, "card1", "guid-card", definition);

            var expansion = DesignerComponentExpander.Expand(screen, new StubResolver().Add("guid-card", definition));

            Assert.IsTrue(expansion.Issues.Any(i => i.Kind == DesignerComponentExpansionIssueKind.CircularReference));
            expansion.Dispose();
        }

        [Test]
        public void Expand_NestedComponentsAreExpandedThrough()
        {
            var inner = NewCardDefinition();
            inner.componentId = "inner-component";

            var outer = ScriptableObject.CreateInstance<DesignerComponentDefinitionAsset>();
            outer.componentId = "outer-component";
            outer.rootElementId = "shell";
            outer.elements.Add(new DesignerElementMetadata { elementId = "shell", stableId = "outer-shell", elementType = "Panel" });
            outer.elements.Add(new DesignerElementMetadata
            {
                elementId = "nested", stableId = "outer-nested", parentId = "shell",
                componentInstance = new DesignerComponentInstanceMetadata
                {
                    definitionGuid = "guid-inner", definitionId = inner.componentId, definitionVersion = inner.version
                }
            });

            var screen = NewScreen();
            AddInstance(screen, "page", "guid-outer", outer);

            var resolver = new StubResolver().Add("guid-outer", outer).Add("guid-inner", inner);
            var expansion = DesignerComponentExpander.Expand(screen, resolver);

            Assert.IsNotNull(Element(expansion, "page--nested"), "The nested instance keeps its own id...");
            Assert.IsNotNull(Element(expansion, "page--nested--title"), "...and expands the inner definition below it.");
            CollectionAssert.IsEmpty(expansion.Issues);
            expansion.Dispose();
        }

        [Test]
        public void Expand_ReportsVersionMismatch()
        {
            var definition = NewCardDefinition();
            definition.version = 3;
            var screen = NewScreen();
            var instance = AddInstance(screen, "card1", "guid-card", definition);
            instance.componentInstance.definitionVersion = 1;

            var expansion = DesignerComponentExpander.Expand(screen, new StubResolver().Add("guid-card", definition));

            Assert.IsTrue(expansion.Issues.Any(i => i.Kind == DesignerComponentExpansionIssueKind.VersionMismatch));
            expansion.Dispose();
        }

        [Test]
        public void Expand_DetachedInstanceIsNotExpanded()
        {
            var definition = NewCardDefinition();
            var screen = NewScreen();
            var instance = AddInstance(screen, "card1", "guid-card", definition);
            instance.componentInstance.detached = true;

            var expansion = DesignerComponentExpander.Expand(screen, new StubResolver().Add("guid-card", definition));

            Assert.IsFalse(expansion.ContainsInstances);
            Assert.AreEqual(1, screen.elements.Count);
            expansion.Dispose();
        }

        // ---- Typed property application ---------------------------------------------------

        [Test]
        public void PropertyApplier_WritesAndReadsBackCoreProperties()
        {
            var element = new DesignerElementMetadata { elementId = "e" };

            Assert.IsTrue(DesignerPropertyApplier.Apply(element, DesignerPropertyId.Width,
                new DesignerPropertyValue { type = DesignerPropertyValueType.Float, floatValue = 320f }));
            Assert.IsTrue(DesignerPropertyApplier.Apply(element, DesignerPropertyId.BackgroundColor,
                new DesignerPropertyValue { type = DesignerPropertyValueType.Color, colorValue = Color.red }));
            Assert.IsTrue(DesignerPropertyApplier.Apply(element, DesignerPropertyId.RuntimeVisible,
                new DesignerPropertyValue { type = DesignerPropertyValueType.Boolean, boolValue = false }));

            Assert.AreEqual(320f, element.rect.width);
            Assert.AreEqual(Color.red, DesignerPropertyApplier.Read(element, DesignerPropertyId.BackgroundColor).colorValue);
            Assert.IsFalse(element.runtimeVisible);
        }

        [Test]
        public void PropertyApplier_ReturnsFalseForPropertiesWithNoAuthoredRepresentation()
        {
            // Texture, not Gradient: gradients gained an authored representation and now apply,
            // so using one here would assert that a working property does not work.
            var element = new DesignerElementMetadata { elementId = "e" };
            Assert.IsFalse(DesignerPropertyApplier.Apply(element, DesignerPropertyId.Texture,
                new DesignerPropertyValue { type = DesignerPropertyValueType.AssetReference }));
            Assert.IsFalse(DesignerPropertyApplier.Apply(element, DesignerPropertyId.None, new DesignerPropertyValue()));
        }

        // ---- Migration --------------------------------------------------------------------

        [Test]
        public void Migration_V3ToV4_IsIdempotentAndPreservesAuthoredData()
        {
            var screen = NewScreen();
            screen.schemaVersion = 3;
            var element = Add(screen, "panel");
            element.text = "keep me";
            element.componentInstance = null;

            Assert.IsTrue(DesignerHierarchyMigration.Migrate(screen, recordUndo: false));
            Assert.AreEqual(DesignerMetadataAsset.CurrentSchemaVersion, screen.schemaVersion);
            Assert.IsNotNull(screen.elements[0].componentInstance);
            Assert.AreEqual("keep me", screen.elements[0].text);

            // Re-running must not change anything (NormalizeSiblingIndices already ran).
            Assert.IsFalse(DesignerHierarchyMigration.Migrate(screen, recordUndo: false));
        }

        [Test]
        public void Migration_DropsOverridesThatCanNeverResolve()
        {
            var screen = NewScreen();
            screen.schemaVersion = 3;
            var element = Add(screen, "card1");
            element.componentInstance = new DesignerComponentInstanceMetadata
            {
                definitionGuid = "guid",
                overrides =
                {
                    new DesignerComponentPropertyOverride { propertyId = DesignerPropertyId.None },
                    new DesignerComponentPropertyOverride { targetElementId = "title", propertyId = DesignerPropertyId.Text }
                }
            };

            DesignerHierarchyMigration.Migrate(screen, recordUndo: false);

            Assert.AreEqual(1, element.componentInstance.overrides.Count);
            Assert.AreEqual(DesignerPropertyId.Text, element.componentInstance.overrides[0].propertyId);
        }
    }
}

using System.Linq;
using emiteat.NexUI.Designer.Editor.Components.Definitions;
using emiteat.NexUI.Designer.Editor.Properties;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Renaming an element inside a component definition used to strand every instance override that
    /// pointed at it: expansion reported "no such element" and Update From Definition only stamped a
    /// new version number on top of the same broken data.
    /// </summary>
    /// <remarks>
    /// The fix is an identity that a rename cannot touch - the definition element's <c>stableId</c>,
    /// recorded on the override alongside the readable element id. These tests pin both halves: that a
    /// rename no longer breaks anything, and that overrides written before the field existed are
    /// repaired rather than left to rot.
    ///
    /// The definition has to be a real asset because <c>DesignerComponentLibrary</c> resolves by GUID.
    /// </remarks>
    public sealed class DesignerComponentRemapTests
    {
        private const string Folder = "Assets/NexUIComponentRemapTests";
        private const string DefinitionPath = Folder + "/Card.asset";

        private DesignerComponentDefinitionAsset _definition;
        private DesignerMetadataAsset _screen;
        private DesignerElementMetadata _instance;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "NexUIComponentRemapTests");

            _definition = ScriptableObject.CreateInstance<DesignerComponentDefinitionAsset>();
            _definition.componentId = "card-component";
            _definition.displayName = "Card";
            _definition.version = 1;
            _definition.rootElementId = "root";
            _definition.elements.Add(new DesignerElementMetadata
            {
                elementId = "root", stableId = "def-root", elementType = "Panel",
                rect = new Rect(0f, 0f, 200f, 120f)
            });
            _definition.elements.Add(new DesignerElementMetadata
            {
                elementId = "title", stableId = "def-title", parentId = "root", elementType = "Label",
                rect = new Rect(8f, 8f, 180f, 24f), text = "Title"
            });
            AssetDatabase.CreateAsset(_definition, DefinitionPath);
            AssetDatabase.SaveAssets();
            DesignerComponentLibrary.Invalidate();

            _screen = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            _screen.schemaVersion = DesignerMetadataAsset.CurrentSchemaVersion;
            _instance = new DesignerElementMetadata
            {
                elementId = "card1", stableId = "stable-card1", rect = new Rect(0f, 0f, 200f, 120f),
                componentInstance = new DesignerComponentInstanceMetadata
                {
                    definitionGuid = DesignerComponentLibrary.GuidOf(_definition),
                    definitionId = _definition.componentId,
                    definitionVersion = 1
                }
            };
            _screen.elements.Add(_instance);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
            DesignerComponentLibrary.Invalidate();
            if (_screen != null) Object.DestroyImmediate(_screen);
            Undo.ClearAll();
        }

        private DesignerComponentPropertyOverride AddOverride(string targetElementId, string text)
        {
            var item = new DesignerComponentPropertyOverride
            {
                targetElementId = targetElementId,
                propertyId = DesignerPropertyId.Text,
                value = new DesignerPropertyValue
                    { type = DesignerPropertyValueType.String, stringValue = text }
            };
            Assert.IsTrue(DesignerComponentService.SetOverride(_screen, _instance, item));
            return _instance.componentInstance.overrides.Last();
        }

        private static void Rename(DesignerComponentDefinitionAsset definition, string from, string to)
        {
            var element = definition.Find(from);
            element.elementId = to;
            foreach (var child in definition.elements)
                if (child != null && child.parentId == from) child.parentId = to;
        }

        // ---- Authoring records the identity -------------------------------------------------

        [Test]
        public void AnAuthoredOverrideRecordsItsTargetsStableId()
        {
            var item = AddOverride("title", "Hello");

            Assert.AreEqual("def-title", item.targetStableId,
                "recording it at authoring time is what makes the later rename a non-event");
        }

        [Test]
        public void ARenamedTargetStillResolvesDuringExpansion()
        {
            AddOverride("title", "Hello");
            Rename(_definition, "title", "heading");

            var expansion = DesignerComponentExpander.Expand(_screen, DesignerComponentLibrary.Resolver);
            try
            {
                var title = expansion.Expanded.elements.First(e => e.elementId.EndsWith("heading"));
                Assert.AreEqual("Hello", title.text);
                CollectionAssert.IsEmpty(expansion.Issues.Where(i =>
                        i.Kind == DesignerComponentExpansionIssueKind.UnresolvedOverride),
                    "a rename must not read as an unresolved override");
            }
            finally
            {
                expansion.Dispose();
            }
        }

        // ---- Update From Definition repairs --------------------------------------------------

        [Test]
        public void UpdateFromDefinitionRePointsARenamedTarget()
        {
            var item = AddOverride("title", "Hello");
            Rename(_definition, "title", "heading");

            var result = DesignerComponentService.UpdateFromDefinition(_screen, _instance);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("heading", item.targetElementId, "the readable id follows the rename");
            Assert.AreEqual("def-title", item.targetStableId);
            CollectionAssert.IsEmpty(result.Warnings);
            Assert.AreEqual(1, result.Notes.Count, result.Message);
        }

        /// <summary>An override written before stable ids existed has to be adopted, not just tolerated.</summary>
        [Test]
        public void UpdateFromDefinitionBackfillsALegacyOverride()
        {
            var item = AddOverride("title", "Hello");
            item.targetStableId = null;

            var result = DesignerComponentService.UpdateFromDefinition(_screen, _instance);

            Assert.AreEqual("def-title", item.targetStableId);
            Assert.AreEqual(1, result.Notes.Count);
            CollectionAssert.IsEmpty(result.Warnings);
        }

        [Test]
        public void ATrulyDeletedTargetIsReportedAndKept()
        {
            AddOverride("title", "Hello");
            _definition.elements.RemoveAll(e => e.elementId == "title");

            var result = DesignerComponentService.UpdateFromDefinition(_screen, _instance);

            Assert.AreEqual(1, result.Warnings.Count);
            Assert.AreEqual(1, _instance.componentInstance.overrides.Count,
                "the authored value is kept: the element may come back, the value will not");
        }

        [Test]
        public void ResetUnresolvedRemovesOnlyTheStrandedOverrides()
        {
            AddOverride("title", "Hello");
            AddOverride("root", "Kept");
            _definition.elements.RemoveAll(e => e.elementId == "title");

            var result = DesignerComponentService.UpdateFromDefinition(_screen, _instance, resetUnresolved: true);

            Assert.AreEqual(1, _instance.componentInstance.overrides.Count);
            Assert.AreEqual("root", _instance.componentInstance.overrides[0].targetElementId);
            Assert.AreEqual(1, result.Warnings.Count);
        }

        [Test]
        public void ANewVariantAxisIsAddedAtItsDefault()
        {
            _definition.variantProperties.Add(new DesignerComponentVariantProperty
            {
                propertyName = "size",
                type = DesignerComponentVariantPropertyType.Enum,
                options = { "small", "large" },
                defaultValue = "large"
            });
            _definition.version = 2;

            var result = DesignerComponentService.UpdateFromDefinition(_screen, _instance);

            Assert.AreEqual("large", _instance.componentInstance.GetVariantSelection("size"));
            Assert.AreEqual(2, _instance.componentInstance.definitionVersion);
            Assert.AreEqual(1, result.Notes.Count);
        }

        /// <summary>
        /// Exposed properties and variant-rule overrides live in the definition and point at its own
        /// elements by id, so they need the same identity or the repair is only half a repair.
        /// </summary>
        [Test]
        public void ReconcilingBackfillsTheDefinitionsOwnTargets()
        {
            _definition.exposedProperties.Add(new DesignerComponentExposedProperty
            {
                propertyName = "title", targetElementId = "title", propertyId = DesignerPropertyId.Text
            });
            _definition.variantRules.Add(new DesignerComponentVariantRule
            {
                propertyName = "size", equalsValue = "large",
                overrides =
                {
                    new DesignerComponentPropertyOverride
                        { targetElementId = "title", propertyId = DesignerPropertyId.Text }
                }
            });

            DesignerComponentService.UpdateFromDefinition(_screen, _instance);

            Assert.AreEqual("def-title", _definition.exposedProperties[0].targetStableId);
            Assert.AreEqual("def-title", _definition.variantRules[0].overrides[0].targetStableId);
        }

        [Test]
        public void BackfillingIsIdempotentAndNeverRePointsWhatIsAlreadyRecorded()
        {
            _definition.exposedProperties.Add(new DesignerComponentExposedProperty
            {
                propertyName = "title", targetElementId = "title", targetStableId = "def-title",
                propertyId = DesignerPropertyId.Text
            });

            Assert.IsFalse(DesignerComponentService.BackfillDefinitionTargets(_definition),
                "nothing to record means nothing to dirty");
        }
    }
}

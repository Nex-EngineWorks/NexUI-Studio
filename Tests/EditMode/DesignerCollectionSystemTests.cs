using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Components;
using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Validation;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// The Designer half of the CollectionView system: that presets really are the Core component,
    /// that authored properties survive the trip to runtime options, and that the failure modes a
    /// collection has at runtime are caught at author time instead.
    /// </summary>
    public sealed class DesignerCollectionSystemTests
    {
        [SetUp]
        public void SetUp() => DesignerBackendRegistry.RegisterDefaults();

        private static DesignerElementMetadata Collection(string typeId = "CollectionView")
            => new DesignerElementMetadata
            {
                stableId = "collection-stable", elementId = "items", elementType = typeId,
                rect = new Rect(0, 0, 300, 400)
            };

        private static void SetInt(DesignerElementMetadata element, string key, int value)
            => DesignerComponentPropertyAccess.Set(element, key,
                new DesignerPropertyValue { type = DesignerPropertyValueType.Integer, intValue = value });

        private static void SetEnum(DesignerElementMetadata element, string key, int index)
            => DesignerComponentPropertyAccess.Set(element, key,
                new DesignerPropertyValue { type = DesignerPropertyValueType.Enum, intValue = index });

        private static void SetBool(DesignerElementMetadata element, string key, bool value)
            => DesignerComponentPropertyAccess.Set(element, key,
                new DesignerPropertyValue { type = DesignerPropertyValueType.Boolean, boolValue = value });

        private static void SetFloat(DesignerElementMetadata element, string key, float value)
            => DesignerComponentPropertyAccess.Set(element, key,
                new DesignerPropertyValue { type = DesignerPropertyValueType.Float, floatValue = value });

        // ---- Registry -------------------------------------------------------------------------

        [Test]
        public void CollectionViewIsRegisteredAsACoreComponentWithBothBackends()
        {
            var descriptor = DesignerComponentRegistry.Get("CollectionView");

            Assert.AreEqual("CollectionView", descriptor.TypeId);
            Assert.AreEqual(DesignerComponentKind.Core, descriptor.Kind);
            Assert.AreEqual("CollectionView", descriptor.UGUIControl);
            Assert.IsNotEmpty(descriptor.UxmlTag);
            Assert.AreEqual(DesignerBackendSupport.Full, descriptor.UGUISupport);
            Assert.IsTrue(descriptor.IsCollectionComponent);
        }

        [TestCase("List")]
        [TestCase("Grid")]
        [TestCase("InventoryGrid")]
        [TestCase("SelectionList")]
        [TestCase("VirtualGrid")]
        public void CollectionPresetsInheritTheCoreBackendMapping(string typeId)
        {
            var descriptor = DesignerComponentRegistry.Get(typeId);
            var core = DesignerComponentRegistry.Get("CollectionView");

            Assert.AreEqual(DesignerComponentKind.Preset, descriptor.Kind, $"'{typeId}' should be a preset.");
            Assert.AreEqual("CollectionView", descriptor.BaseTypeId);
            Assert.AreEqual(core.UGUIControl, descriptor.UGUIControl,
                "A preset must write the same control as its Core, or it needs its own serializer.");
            Assert.AreEqual(core.UxmlTag, descriptor.UxmlTag);
        }

        [Test]
        public void PresetsKeepTheirOwnTypeIdSoExistingScreensStillLoad()
        {
            // Reclassification is a tag, not a rename: a screen saved with elementType "InventoryGrid"
            // must still resolve to a real descriptor.
            Assert.IsTrue(DesignerComponentRegistry.IsRegistered("InventoryGrid"));
            Assert.IsTrue(DesignerComponentRegistry.IsRegistered("List"));
            Assert.AreEqual("InventoryGrid", DesignerComponentRegistry.Get("InventoryGrid").TypeId);
        }

        [Test]
        public void CollectionSchemaCarriesTheRuntimeOptionKeys()
        {
            var descriptor = DesignerComponentRegistry.Get("CollectionView");
            var keys = new HashSet<string>();
            foreach (var property in descriptor.Properties) keys.Add(property.Key);

            CollectionAssert.IsSubsetOf(
                new[]
                {
                    "items.source", "items.layout", "items.virtualization", "items.selection",
                    "items.itemSize", "items.columns", "items.overscan", "items.paging"
                },
                keys);
        }

        // ---- Option reading -------------------------------------------------------------------

        [Test]
        public void AuthoredPropertiesBecomeRuntimeOptions()
        {
            var element = Collection();
            SetEnum(element, "items.layout", (int)NXCollectionLayout.Grid);
            SetEnum(element, "items.selection", (int)NXSelectionMode.Multiple);
            SetEnum(element, "items.virtualization", (int)NXVirtualizationMode.DynamicSize);
            SetInt(element, "items.columns", 6);
            SetFloat(element, "items.itemSize", 120f);
            SetBool(element, "items.reorderable", true);

            var options = DesignerCollectionOptions.Read(element);

            Assert.AreEqual(NXCollectionLayout.Grid, options.Layout);
            Assert.AreEqual(NXSelectionMode.Multiple, options.Selection);
            Assert.AreEqual(NXVirtualizationMode.DynamicSize, options.Virtualization);
            Assert.AreEqual(6, options.ColumnCount);
            Assert.AreEqual(120f, options.ItemSize);
            Assert.IsTrue((options.Interactions & NXCollectionInteractions.Reorder) != 0);
        }

        [Test]
        public void LegacyVirtualizeBoolStillDecidesVirtualizationWhenTheEnumWasNeverSet()
        {
            var element = Collection();
            SetBool(element, "items.virtualize", false);

            Assert.AreEqual(NXVirtualizationMode.None, DesignerCollectionOptions.Read(element).Virtualization,
                "A screen authored before the enum existed must keep behaving the same way.");
        }

        [Test]
        public void LegacyOrientationStillDecidesLayoutWhenLayoutWasNeverSet()
        {
            var element = Collection();
            SetEnum(element, "items.orientation", 0); // Horizontal

            Assert.AreEqual(NXCollectionLayout.Horizontal, DesignerCollectionOptions.Read(element).Layout);
        }

        [Test]
        public void TheNewEnumWinsOverTheLegacyKey()
        {
            var element = Collection();
            SetBool(element, "items.virtualize", false);
            SetEnum(element, "items.virtualization", (int)NXVirtualizationMode.FixedSize);

            Assert.AreEqual(NXVirtualizationMode.FixedSize, DesignerCollectionOptions.Read(element).Virtualization);
        }

        [Test]
        public void ZeroItemSizeIsPromotedToDynamicMeasurement()
        {
            var element = Collection();
            SetFloat(element, "items.itemSize", 0f);

            var options = DesignerCollectionOptions.Read(element);

            Assert.AreEqual(NXVirtualizationMode.DynamicSize, options.Virtualization,
                "Item size 0 used to mean 'measure every item'.");
            Assert.Greater(options.ItemSize, 0f, "The estimate must stay usable.");
        }

        [Test]
        public void PresetsAreRecognisedAsCollections()
        {
            Assert.IsTrue(DesignerCollectionOptions.IsCollection(Collection("InventoryGrid")));
            Assert.IsTrue(DesignerCollectionOptions.IsCollection(Collection("CollectionView")));
            Assert.IsFalse(DesignerCollectionOptions.IsCollection(Collection("Button")));
        }

        // ---- Validation -----------------------------------------------------------------------

        private static UIScreenDefinition NewScreen(string id)
        {
            var screen = ScriptableObject.CreateInstance<UIScreenDefinition>();
            screen.identity = new UIScreenIdentity { screenId = id };
            screen.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UIToolkit, asset = null };
            return screen;
        }

        private static List<DesignerValidationIssue> Validate(DesignerElementMetadata element,
            params DesignerElementMetadata[] children)
        {
            var screen = NewScreen("screen");
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.screenId = "screen";
            metadata.elements.Add(element);
            foreach (var child in children) metadata.elements.Add(child);
            try
            {
                return DesignerValidationService.Validate(screen, metadata);
            }
            finally
            {
                Object.DestroyImmediate(metadata);
                Object.DestroyImmediate(screen);
            }
        }

        private static bool HasCode(List<DesignerValidationIssue> issues, string code)
            => issues.Exists(issue => issue.Code == code);

        [Test]
        public void MissingItemTemplateIsAnError()
        {
            var issues = Validate(Collection());

            Assert.IsTrue(HasCode(issues, "collection-template-missing"),
                "A collection with no template shows nothing at runtime; that must not reach a build.");
        }

        [Test]
        public void AnItemTemplateChildSatisfiesTheRule()
        {
            var element = Collection();
            var template = new DesignerElementMetadata
            {
                elementId = "row", elementType = "Panel", parentId = element.elementId, parentSlotId = "item"
            };

            var issues = Validate(element, template);

            Assert.IsFalse(HasCode(issues, "collection-template-missing"));
        }

        [Test]
        public void MissingItemsSourceIsAWarning()
        {
            var issues = Validate(Collection());

            Assert.IsTrue(HasCode(issues, "collection-source-missing"));
        }

        [Test]
        public void UnsupportedOptionCombinationIsReported()
        {
            var element = Collection();
            SetEnum(element, "items.layout", (int)NXCollectionLayout.Wrap);
            SetEnum(element, "items.virtualization", (int)NXVirtualizationMode.DynamicSize);

            var issues = Validate(element);

            Assert.IsTrue(HasCode(issues, "collection-options-conflict"),
                "Wrap uses uniform cells, so Dynamic Size cannot be honoured and must be said out loud.");
        }

        [Test]
        public void ReorderWithoutSelectionIsReported()
        {
            var element = Collection();
            SetBool(element, "items.reorderable", true);
            SetEnum(element, "items.selection", (int)NXSelectionMode.None);

            var issues = Validate(element);

            Assert.IsTrue(HasCode(issues, "collection-selection-conflict"));
        }

        [Test]
        public void InfinitePagingWithoutVirtualizationIsReported()
        {
            var element = Collection();
            SetEnum(element, "items.paging", (int)NXPagingMode.Infinite);
            SetEnum(element, "items.virtualization", (int)NXVirtualizationMode.None);

            var issues = Validate(element);

            Assert.IsTrue(HasCode(issues, "collection-virtualization-conflict"));
        }

        [Test]
        public void ANonCollectionElementRaisesNoCollectionIssues()
        {
            var issues = Validate(new DesignerElementMetadata
            {
                stableId = "button-stable", elementId = "ok", elementType = "Button",
                rect = new Rect(0, 0, 100, 40)
            });

            Assert.IsFalse(HasCode(issues, "collection-template-missing"));
            Assert.IsFalse(HasCode(issues, "collection-source-missing"));
        }
    }
}

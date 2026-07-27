using emiteat.NexUI.Designer.Editor;
using emiteat.NexUI.Designer.Editor.Properties;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Coordinate conversion (<see cref="DesignerCoordinateUtility"/>) and schema migration
    /// (<see cref="DesignerHierarchyMigration"/>) tests. Migration runs with recordUndo:false so
    /// no window/editor state is needed.
    /// </summary>
    public sealed class DesignerCoordinateAndMigrationTests
    {
        private static DesignerMetadataAsset NewAsset() => ScriptableObject.CreateInstance<DesignerMetadataAsset>();

        private static DesignerElementMetadata Add(DesignerMetadataAsset asset, string id, Rect rect, string parent = null)
        {
            var e = new DesignerElementMetadata { elementId = id, parentId = parent, rect = rect };
            asset.elements.Add(e);
            return e;
        }

        [Test]
        public void GetLocalPosition_IsCanvasMinusParentOrigin()
        {
            var asset = NewAsset();
            Add(asset, "parent", new Rect(100, 50, 400, 300));
            var child = Add(asset, "child", new Rect(120, 80, 40, 40), "parent");

            var local = DesignerCoordinateUtility.GetLocalPosition(asset, child);
            Assert.AreEqual(new Vector2(20, 30), local);
        }

        [Test]
        public void RootElement_LocalEqualsCanvas()
        {
            var asset = NewAsset();
            var root = Add(asset, "root", new Rect(64, 48, 100, 100));
            Assert.AreEqual(new Vector2(64, 48), DesignerCoordinateUtility.GetLocalPosition(asset, root));
        }

        [Test]
        public void LocalToCanvas_RoundTrips()
        {
            var asset = NewAsset();
            Add(asset, "parent", new Rect(200, 100, 400, 300));
            var child = Add(asset, "child", new Rect(0, 0, 40, 40), "parent");

            var canvas = DesignerCoordinateUtility.LocalToCanvas(asset, child, new Vector2(15, 25));
            Assert.AreEqual(new Vector2(215, 125), canvas);
            var back = DesignerCoordinateUtility.CanvasToLocal(asset, child, canvas);
            Assert.AreEqual(new Vector2(15, 25), back);
        }

        [Test]
        public void ContentPadding_ShiftsLocalOrigin()
        {
            var asset = NewAsset();
            var parent = Add(asset, "parent", new Rect(100, 100, 400, 300));
            parent.contentPadding = new RectOffset(10, 0, 20, 0); // left=10, top=20
            var child = Add(asset, "child", new Rect(120, 140, 40, 40), "parent");

            // origin = (100+10, 100+20) = (110,120); local = (120-110, 140-120) = (10,20)
            Assert.AreEqual(new Vector2(10, 20), DesignerCoordinateUtility.GetLocalPosition(asset, child));
        }

        [Test]
        public void GetCanvasBounds_UnionsRects()
        {
            var asset = NewAsset();
            var a = Add(asset, "a", new Rect(0, 0, 100, 100));
            var b = Add(asset, "b", new Rect(200, 150, 50, 50));
            var bounds = DesignerCoordinateUtility.GetCanvasBounds(new[] { a, b });
            Assert.AreEqual(new Rect(0, 0, 250, 200), bounds);
        }

        [Test]
        public void Migration_AssignsSiblingIndicesAndBumpsSchema()
        {
            var asset = NewAsset();
            Add(asset, "root", new Rect(0, 0, 100, 100));
            Add(asset, "a", new Rect(0, 0, 10, 10), "root");
            Add(asset, "b", new Rect(0, 0, 10, 10), "root");
            Assert.AreEqual(0, asset.schemaVersion);

            Assert.IsTrue(DesignerHierarchyMigration.Migrate(asset, recordUndo: false));
            Assert.AreEqual(DesignerMetadataAsset.CurrentSchemaVersion, asset.schemaVersion);

            var children = DesignerHierarchyUtility.GetOrderedChildren(asset, "root");
            Assert.AreEqual(0, children[0].siblingIndex);
            Assert.AreEqual(1, children[1].siblingIndex);
        }

        [Test]
        public void Migration_IsIdempotent()
        {
            var asset = NewAsset();
            Add(asset, "root", new Rect(0, 0, 100, 100));
            Add(asset, "a", new Rect(0, 0, 10, 10), "root");

            Assert.IsTrue(DesignerHierarchyMigration.Migrate(asset, recordUndo: false), "first run migrates");
            Assert.IsFalse(DesignerHierarchyMigration.Migrate(asset, recordUndo: false), "second run is a no-op");
        }

        [Test]
        public void Migration_V1AssignsStableIdentityAndPreservesLegacyRuntimeVisibility()
        {
            var asset = NewAsset();
            asset.schemaVersion = 1;
            var visible = Add(asset, "visible", new Rect(0, 0, 10, 10));
            var hidden = Add(asset, "hidden", new Rect(0, 0, 10, 10));
            visible.stableId = string.Empty;
            hidden.stableId = string.Empty;
            visible.hiddenInDesigner = false;
            hidden.hiddenInDesigner = true;

            Assert.IsTrue(DesignerHierarchyMigration.Migrate(asset, recordUndo: false));

            Assert.IsNotEmpty(visible.stableId);
            Assert.IsNotEmpty(hidden.stableId);
            Assert.IsTrue(visible.runtimeVisible);
            Assert.IsFalse(hidden.runtimeVisible);
            Assert.IsFalse(DesignerHierarchyMigration.Migrate(asset, recordUndo: false));
        }

        [Test]
        public void Migration_PreservesDrawOrderAsSiblingOrder()
        {
            var asset = NewAsset();
            Add(asset, "root", new Rect(0, 0, 100, 100));
            Add(asset, "first", new Rect(0, 0, 10, 10), "root");
            Add(asset, "second", new Rect(0, 0, 10, 10), "root");
            Add(asset, "third", new Rect(0, 0, 10, 10), "root");

            DesignerHierarchyMigration.Migrate(asset, recordUndo: false);
            var children = DesignerHierarchyUtility.GetOrderedChildren(asset, "root");
            Assert.AreEqual("first", children[0].elementId);
            Assert.AreEqual("second", children[1].elementId);
            Assert.AreEqual("third", children[2].elementId);
        }

        [Test]
        public void Migration_V2SeedsTypedStylesAndConvertsLegacyOverrides()
        {
            var asset = NewAsset();
            asset.schemaVersion = 2;
            var element = Add(asset, "title", new Rect(0, 0, 100, 30));
            element.tint = Color.red;
            element.textColor = Color.green;
            element.fontSize = 24;
            var variant = new DesignerVariantMetadata { variantId = "compact" };
            variant.overrides.Add(new DesignerVariantOverrideMetadata
            {
                targetElementId = "title", propertyPath = "fontSize", value = "18"
            });
            asset.variants.Add(variant);

            Assert.IsTrue(DesignerHierarchyMigration.Migrate(asset, recordUndo: false));
            // Migration always lands on the current schema; this test is about the v2 → v3 *effects*,
            // not the version number, so it must not pin an older version.
            Assert.AreEqual(DesignerMetadataAsset.CurrentSchemaVersion, asset.schemaVersion);
            Assert.IsTrue(element.visualStyle.hasOverrides);
            Assert.AreEqual(Color.red, element.visualStyle.backgroundColor);
            Assert.AreEqual(Color.green, element.typography.color);
            Assert.AreEqual(24f, element.typography.fontSize);
            Assert.AreEqual(DesignerPropertyId.FontSize, variant.overrides[0].propertyId);
            Assert.AreEqual(18f, variant.overrides[0].typedValue.floatValue);
            Assert.AreEqual("fontSize", DesignerPropertyRegistry.PathFor(variant.overrides[0].propertyId));
        }
    }
}

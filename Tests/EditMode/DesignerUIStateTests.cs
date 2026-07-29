using emiteat.NexUI.Designer.Editor;
using emiteat.NexUI.Designer.Editor.Viewport;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    public sealed class DesignerUIStateTests
    {
        // Grid size and snapping live in EditorPrefs, which are shared with whatever the developer
        // last set in the Designer window. Several assertions below depend on them, so pin known
        // values for the run and put the user's settings back afterwards - otherwise the suite
        // passes or fails depending on the machine it runs on.
        //
        // Snapping is pinned OFF: these tests assert exact alignment and smart-guide results, and
        // grid rounding on top of them tests nothing. Grid size still matters because Duplicate
        // offsets copies by two grid cells.
        private float _originalGridSize;
        private bool _originalSnap;

        [SetUp]
        public void PinCanvasPreferences()
        {
            using var probe = new NexUIDesignerContext();
            _originalGridSize = probe.GridSize;
            _originalSnap = probe.SnapEnabled;
            probe.SetGridSize(8f);
            probe.SetSnap(false);
        }

        [TearDown]
        public void RestoreCanvasPreferences()
        {
            using var probe = new NexUIDesignerContext();
            probe.SetGridSize(_originalGridSize);
            probe.SetSnap(_originalSnap);
        }

        [Test]
        public void UIState_ChangesPersistOnContext()
        {
            var context = new NexUIDesignerContext();

            context.SetSidebarTab(DesignerSidebarTab.Components);
            context.SetInspectorTab(DesignerInspectorTab.Motion);
            context.SetBottomTab(DesignerBottomTab.History, true);
            context.SetTool(DesignerTool.Hand);

            Assert.AreEqual(DesignerSidebarTab.Components, context.SidebarTab);
            Assert.AreEqual(DesignerInspectorTab.Motion, context.InspectorTab);
            Assert.AreEqual(DesignerBottomTab.History, context.BottomTab);
            Assert.AreEqual(DesignerTool.Hand, context.CurrentTool);
            Assert.IsTrue(context.BottomDrawerOpen);
        }

        [Test]
        public void KeyObject_BecomesAlignmentBounds()
        {
            var context = new NexUIDesignerContext();
            var asset = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var left = new DesignerElementMetadata { elementId = "left", rect = new Rect(10, 0, 20, 20) };
            var key = new DesignerElementMetadata { elementId = "key", rect = new Rect(100, 0, 50, 20) };
            asset.elements.Add(left);
            asset.elements.Add(key);
            context.SetMetadata(asset);
            context.SelectMany(new[] { left, key });
            context.SetKeyObject(key);

            context.AlignSelection("right");

            Assert.AreEqual(130, left.rect.x);
            Assert.AreEqual(150, key.rect.xMax);
        }

        [Test]
        public void AltDragDuplicateEntryPoint_DuplicatesSelection()
        {
            var context = new NexUIDesignerContext();
            var asset = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var element = new DesignerElementMetadata { elementId = "button", rect = new Rect(0, 0, 20, 20) };
            asset.elements.Add(element);
            context.SetMetadata(asset);
            context.SelectMetadata(element);

            var copies = context.DuplicateSelectionAtDragStart();

            Assert.AreEqual(1, copies.Count);
            Assert.AreEqual(2, asset.elements.Count);
            Assert.AreEqual(copies[0], context.SelectedMetadata);
        }

        [Test]
        public void DuplicateSelection_PreservesSubtreeAndRemapsInternalReferences()
        {
            var context = new NexUIDesignerContext();
            var asset = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var parent = new DesignerElementMetadata { elementId = "panel", rect = new Rect(10, 20, 100, 100) };
            var child = new DesignerElementMetadata { elementId = "button", parentId = "panel", rect = new Rect(20, 30, 30, 20) };
            var grandchild = new DesignerElementMetadata { elementId = "label", parentId = "button", rect = new Rect(22, 32, 20, 10) };
            child.focus.rightElementId = "label";
            asset.elements.Add(parent);
            asset.elements.Add(child);
            asset.elements.Add(grandchild);
            asset.screenMotion.bindings.Add(new DesignerMotionBinding { bindingId = "hover", targetElementId = "button" });
            context.SetMetadata(asset);
            context.SelectMetadata(parent);

            var copies = context.DuplicateSelection();

            Assert.AreEqual(3, copies.Count);
            var parentCopy = copies.Find(e => string.IsNullOrEmpty(e.parentId));
            var childCopy = copies.Find(e => e.parentId == parentCopy.elementId);
            var grandchildCopy = copies.Find(e => e.parentId == childCopy.elementId);
            Assert.NotNull(parentCopy);
            Assert.NotNull(childCopy);
            Assert.NotNull(grandchildCopy);
            Assert.AreEqual(grandchildCopy.elementId, childCopy.focus.rightElementId);
            Assert.AreEqual(parent.rect.position + Vector2.one * 16f, parentCopy.rect.position);
            Assert.AreNotEqual(parent.stableId, parentCopy.stableId);
            Assert.AreNotEqual(child.stableId, childCopy.stableId);
            Assert.AreEqual(2, asset.screenMotion.bindings.Count);
            Assert.AreEqual(childCopy.elementId, asset.screenMotion.bindings[1].targetElementId);
            context.Dispose();
        }

        [Test]
        public void CopySelection_UsesSnapshotRatherThanLiveSource()
        {
            var context = new NexUIDesignerContext();
            var asset = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var source = new DesignerElementMetadata { elementId = "label", text = "Before" };
            source.classes.Add("original");
            asset.elements.Add(source);
            context.SetMetadata(asset);
            context.SelectMetadata(source);
            context.CopySelection();

            source.text = "After";
            source.classes.Add("later");
            var pasted = context.PasteSelection();

            Assert.AreEqual("Before", pasted[0].text);
            CollectionAssert.AreEqual(new[] { "original" }, pasted[0].classes);
            context.Dispose();
        }

        [Test]
        public void LayerOrder_MoveElementChangesMetadataOrder()
        {
            var context = new NexUIDesignerContext();
            var asset = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var a = new DesignerElementMetadata { elementId = "a" };
            var b = new DesignerElementMetadata { elementId = "b" };
            asset.elements.Add(a);
            asset.elements.Add(b);
            context.SetMetadata(asset);

            context.MoveElementInLayerOrder(b, -1);

            Assert.AreEqual(b, asset.elements[0]);
            Assert.AreEqual(a, asset.elements[1]);
        }

        [Test]
        public void SmartGuide_SnapsToNearbyElementEdge()
        {
            var moving = new DesignerElementMetadata { elementId = "moving", rect = new Rect(96, 0, 20, 20) };
            var target = new DesignerElementMetadata { elementId = "target", rect = new Rect(120, 0, 30, 20) };

            var result = NexUISmartGuideUtility.Snap(moving.rect, new[] { moving, target }, moving, 8f);

            Assert.AreEqual(100, result.Rect.x);
            Assert.AreEqual(120, result.VerticalGuide.Value);
        }
    }
}

using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Viewport;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Canvas drag-to-reparent rules. These decide where a dragged element lands in the hierarchy, so
    /// the failure modes are structural (losing a subtree, parenting into something invisible) rather
    /// than cosmetic - worth pinning down precisely.
    /// </summary>
    public sealed class DesignerDropTargetResolverTests
    {
        private static DesignerMetadataAsset NewAsset() => ScriptableObject.CreateInstance<DesignerMetadataAsset>();

        private static DesignerElementMetadata Add(DesignerMetadataAsset asset, string id, Rect rect,
            string parent = null, string type = "Panel")
        {
            var e = new DesignerElementMetadata
            {
                elementId = id,
                stableId = "stable-" + id,
                parentId = parent,
                elementType = type,
                rect = rect
            };
            asset.elements.Add(e);
            return e;
        }

        private static List<DesignerElementMetadata> Dragging(params DesignerElementMetadata[] elements)
            => new List<DesignerElementMetadata>(elements);

        [Test]
        public void Resolve_EmptyCanvasMeansScreenRoot()
        {
            var asset = NewAsset();
            var moving = Add(asset, "box", new Rect(0, 0, 10, 10));

            Assert.IsNull(DesignerDropTargetResolver.Resolve(asset, new Vector2(500, 500), Dragging(moving)));
        }

        [Test]
        public void Resolve_PicksTheContainerUnderThePoint()
        {
            var asset = NewAsset();
            var panel = Add(asset, "panel", new Rect(0, 0, 400, 300));
            var moving = Add(asset, "box", new Rect(600, 600, 40, 40));

            var target = DesignerDropTargetResolver.Resolve(asset, new Vector2(100, 100), Dragging(moving));
            Assert.AreSame(panel, target);
        }

        [Test]
        public void Resolve_PrefersTheDeepestContainer()
        {
            var asset = NewAsset();
            Add(asset, "outer", new Rect(0, 0, 400, 300));
            var inner = Add(asset, "inner", new Rect(50, 50, 100, 100), parent: "outer");
            var moving = Add(asset, "box", new Rect(600, 600, 40, 40));

            var target = DesignerDropTargetResolver.Resolve(asset, new Vector2(60, 60), Dragging(moving));
            Assert.AreSame(inner, target, "Dropping onto a nested card must pick the card, not the panel behind it.");
        }

        [Test]
        public void Resolve_BreaksDepthTiesByDrawOrder()
        {
            var asset = NewAsset();
            Add(asset, "first", new Rect(0, 0, 200, 200));
            var second = Add(asset, "second", new Rect(0, 0, 200, 200));
            var moving = Add(asset, "box", new Rect(600, 600, 40, 40));

            var target = DesignerDropTargetResolver.Resolve(asset, new Vector2(50, 50), Dragging(moving));
            Assert.AreSame(second, target, "The element drawn last is on top, so it receives the drop.");
        }

        // ---- Safety ------------------------------------------------------------------------

        [Test]
        public void Resolve_NeverDropsAnElementIntoItself()
        {
            var asset = NewAsset();
            var moving = Add(asset, "panel", new Rect(0, 0, 400, 300));

            Assert.IsNull(DesignerDropTargetResolver.Resolve(asset, new Vector2(100, 100), Dragging(moving)));
        }

        [Test]
        public void Resolve_NeverDropsAnElementIntoItsOwnDescendant()
        {
            var asset = NewAsset();
            var outer = Add(asset, "outer", new Rect(0, 0, 400, 300));
            Add(asset, "inner", new Rect(50, 50, 100, 100), parent: "outer");

            var target = DesignerDropTargetResolver.Resolve(asset, new Vector2(60, 60), Dragging(outer));
            Assert.IsNull(target, "Dropping a node into its own subtree would detach the branch.");
        }

        [Test]
        public void Resolve_SkipsHiddenAndLockedContainers()
        {
            var asset = NewAsset();
            var hidden = Add(asset, "hidden", new Rect(0, 0, 400, 300));
            hidden.hiddenInDesigner = true;
            var moving = Add(asset, "box", new Rect(600, 600, 40, 40));
            Assert.IsNull(DesignerDropTargetResolver.Resolve(asset, new Vector2(100, 100), Dragging(moving)));

            hidden.hiddenInDesigner = false;
            hidden.locked = true;
            Assert.IsNull(DesignerDropTargetResolver.Resolve(asset, new Vector2(100, 100), Dragging(moving)),
                "Parenting into something the user cannot edit would be a trap.");
        }

        [Test]
        public void Resolve_SkipsLeafTypesThatCannotHoldChildren()
        {
            var asset = NewAsset();
            Add(asset, "label", new Rect(0, 0, 200, 60), type: "Label");
            var moving = Add(asset, "box", new Rect(600, 600, 40, 40));

            Assert.IsNull(DesignerDropTargetResolver.Resolve(asset, new Vector2(50, 20), Dragging(moving)),
                "A Label declares no slots, so it must not accept a drop.");
        }

        [Test]
        public void Resolve_ExcludesEveryDraggedElementInAMultiSelection()
        {
            var asset = NewAsset();
            var a = Add(asset, "a", new Rect(0, 0, 400, 300));
            var b = Add(asset, "b", new Rect(0, 0, 200, 200));

            Assert.IsNull(DesignerDropTargetResolver.Resolve(asset, new Vector2(50, 50), Dragging(a, b)));
        }

        // ---- Change detection ---------------------------------------------------------------

        [Test]
        public void WouldChangeParent_IsFalseWhenTheParentIsAlreadyTheTarget()
        {
            var asset = NewAsset();
            var panel = Add(asset, "panel", new Rect(0, 0, 400, 300));
            var child = Add(asset, "child", new Rect(10, 10, 50, 50), parent: "panel");

            Assert.IsFalse(DesignerDropTargetResolver.WouldChangeParent(Dragging(child), panel),
                "Re-parenting into the current parent must not create an Undo entry.");
            Assert.IsTrue(DesignerDropTargetResolver.WouldChangeParent(Dragging(child), null),
                "Moving a child out to the root is a real change.");
        }

        [Test]
        public void WouldChangeParent_TreatsRootConsistentlyForNullAndEmptyParentIds()
        {
            var asset = NewAsset();
            var loose = Add(asset, "loose", new Rect(0, 0, 40, 40));
            loose.parentId = null;
            Assert.IsFalse(DesignerDropTargetResolver.WouldChangeParent(Dragging(loose), null));

            loose.parentId = string.Empty;
            Assert.IsFalse(DesignerDropTargetResolver.WouldChangeParent(Dragging(loose), null));
        }

        [Test]
        public void WouldChangeParent_IsTrueWhenAnyMemberOfTheSelectionMoves()
        {
            var asset = NewAsset();
            var panel = Add(asset, "panel", new Rect(0, 0, 400, 300));
            var inside = Add(asset, "inside", new Rect(10, 10, 50, 50), parent: "panel");
            var outside = Add(asset, "outside", new Rect(500, 10, 50, 50));

            Assert.IsTrue(DesignerDropTargetResolver.WouldChangeParent(Dragging(inside, outside), panel));
        }

        [Test]
        public void WouldChangeParent_HandlesEmptyInput()
        {
            Assert.IsFalse(DesignerDropTargetResolver.WouldChangeParent(null, null));
            Assert.IsFalse(DesignerDropTargetResolver.WouldChangeParent(new List<DesignerElementMetadata>(), null));
        }

        [Test]
        public void Describe_NamesTheTargetOrTheRoot()
        {
            var asset = NewAsset();
            var panel = Add(asset, "panel", new Rect(0, 0, 10, 10));

            StringAssert.Contains("panel", DesignerDropTargetResolver.Describe(panel));
            StringAssert.Contains("root", DesignerDropTargetResolver.Describe(null));
        }

        [Test]
        public void Resolve_NullAssetIsSafe()
            => Assert.IsNull(DesignerDropTargetResolver.Resolve(null, Vector2.zero, null));
    }
}

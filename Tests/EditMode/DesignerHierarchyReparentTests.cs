using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Reparenting through the context, which is what both the Layers panel drop and the canvas drop
    /// ultimately call. Covers the outcomes a user can see: the child moves, the on-screen position
    /// is kept, illegal moves are refused, and a multi-selection keeps its subtrees intact.
    /// </summary>
    public sealed class DesignerHierarchyReparentTests
    {
        private DesignerMetadataAsset _asset;
        private NexUIDesignerContext _context;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            _context = new NexUIDesignerContext();
            _context.SetMetadata(_asset);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Dispose();
            Object.DestroyImmediate(_asset);
        }

        private DesignerElementMetadata Add(string id, Rect rect, string parent = null, string type = "Panel")
        {
            var e = new DesignerElementMetadata
            {
                elementId = id,
                stableId = "stable-" + id,
                parentId = parent,
                elementType = type,
                rect = rect
            };
            _asset.elements.Add(e);
            return e;
        }

        [Test]
        public void Reparent_MakesTheElementAChildOfTheTarget()
        {
            var panel = Add("panel", new Rect(0, 0, 400, 300));
            var image = Add("image", new Rect(500, 20, 60, 60));

            _context.ReparentElement(image, panel);

            Assert.AreEqual("panel", image.parentId);
            var children = DesignerHierarchyUtility.GetOrderedChildren(_asset, "panel");
            Assert.AreEqual(1, children.Count);
            Assert.AreSame(image, children[0]);
        }

        [Test]
        public void Reparent_KeepsTheOnScreenPositionByDefault()
        {
            var panel = Add("panel", new Rect(100, 100, 400, 300));
            var image = Add("image", new Rect(500, 20, 60, 60));
            var before = image.rect;

            _context.ReparentElement(image, panel);

            Assert.AreEqual(before, image.rect,
                "Dropping an element into a container must not make it jump; only its parent changes.");
        }

        [Test]
        public void Reparent_ToRootDetachesFromTheParent()
        {
            var panel = Add("panel", new Rect(0, 0, 400, 300));
            var image = Add("image", new Rect(10, 10, 60, 60), parent: "panel");

            _context.ReparentElement(image, null);

            Assert.IsTrue(string.IsNullOrEmpty(image.parentId));
            Assert.IsEmpty(DesignerHierarchyUtility.GetOrderedChildren(_asset, "panel"));
        }

        [Test]
        public void Reparent_RefusesToPutAnElementInsideItsOwnDescendant()
        {
            var outer = Add("outer", new Rect(0, 0, 400, 300));
            var inner = Add("inner", new Rect(10, 10, 100, 100), parent: "outer");

            _context.ReparentElement(outer, inner);

            Assert.IsTrue(string.IsNullOrEmpty(outer.parentId), "Outer must stay a root; the move would have detached the branch.");
            Assert.AreEqual("outer", inner.parentId, "Inner must still hang off outer.");
        }

        [Test]
        public void Reparent_RefusesSelfParenting()
        {
            var panel = Add("panel", new Rect(0, 0, 400, 300));
            _context.ReparentElement(panel, panel);
            Assert.IsTrue(string.IsNullOrEmpty(panel.parentId));
        }

        [Test]
        public void Reparent_MultiSelectionKeepsSubtreesIntactAndMovesOnlyTopMostNodes()
        {
            var target = Add("target", new Rect(0, 0, 400, 300));
            var group = Add("group", new Rect(500, 0, 200, 200));
            var child = Add("child", new Rect(510, 10, 50, 50), parent: "group");

            // Selecting both a node and its own child must not move the child independently.
            _context.ReparentElements(new List<DesignerElementMetadata> { group, child }, target);

            Assert.AreEqual("target", group.parentId);
            Assert.AreEqual("group", child.parentId, "The child must stay under its own parent, not be flattened into the target.");
        }

        [Test]
        public void Reparent_InsertIndexPlacesTheElementBetweenSiblings()
        {
            var parent = Add("parent", new Rect(0, 0, 400, 300));
            Add("a", new Rect(0, 0, 10, 10), parent: "parent");
            Add("b", new Rect(0, 20, 10, 10), parent: "parent");
            var moved = Add("moved", new Rect(500, 0, 10, 10));

            _context.ReparentElements(new List<DesignerElementMetadata> { moved }, parent, 1);

            var order = DesignerHierarchyUtility.GetOrderedChildren(_asset, "parent");
            CollectionAssert.AreEqual(new[] { "a", "moved", "b" }, new[] { order[0].elementId, order[1].elementId, order[2].elementId });
        }

        [Test]
        public void CanReparent_MatchesWhatReparentActuallyDoes()
        {
            var outer = Add("outer", new Rect(0, 0, 400, 300));
            var inner = Add("inner", new Rect(10, 10, 100, 100), parent: "outer");
            var loose = Add("loose", new Rect(500, 0, 40, 40));

            Assert.IsTrue(_context.CanReparent(loose, outer));
            Assert.IsFalse(_context.CanReparent(outer, inner));
            Assert.IsFalse(_context.CanReparent(outer, outer));
        }
    }
}

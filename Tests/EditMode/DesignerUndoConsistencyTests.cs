using emiteat.NexUI.Designer.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    public sealed class DesignerUndoConsistencyTests
    {
        private DesignerMetadataAsset _metadata;
        private NexUIDesignerContext _context;
        private float _originalGridSize;
        private bool _originalSnap;

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            _metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            _context = new NexUIDesignerContext();

            // These tests are about Undo granularity, not snapping. Grid settings come from
            // EditorPrefs shared with the Designer window, so disable snapping for the run and
            // restore the user's values afterwards - otherwise an exact-position assertion fails
            // purely because someone changed the grid in the editor.
            _originalGridSize = _context.GridSize;
            _originalSnap = _context.SnapEnabled;
            _context.SetSnap(false);

            _context.SetMetadata(_metadata);
        }

        [TearDown]
        public void TearDown()
        {
            _context.SetGridSize(_originalGridSize);
            _context.SetSnap(_originalSnap);
            _context.Dispose();
            Object.DestroyImmediate(_metadata);
            Undo.ClearAll();
        }

        [Test]
        public void ElementMove_IsOneUndoStep()
        {
            var element = new DesignerElementMetadata { elementId = "item", rect = new Rect(10, 20, 30, 40) };
            _metadata.elements.Add(element);
            Undo.ClearAll();

            _context.UpdateElementRect(element, new Rect(50, 60, 30, 40));
            Assert.That(element.rect.position, Is.EqualTo(new Vector2(50, 60)));
            Undo.PerformUndo();
            Assert.That(_metadata.Find("item").rect.position, Is.EqualTo(new Vector2(10, 20)));
        }

        [Test]
        public void Reparent_IsOneUndoStep()
        {
            var parent = new DesignerElementMetadata { elementId = "panel" };
            var child = new DesignerElementMetadata { elementId = "item" };
            _metadata.elements.Add(parent);
            _metadata.elements.Add(child);
            Undo.ClearAll();

            _context.ReparentElement(child, parent, false);
            Assert.That(child.parentId, Is.EqualTo("panel"));
            Undo.PerformUndo();
            Assert.That(_metadata.Find("item").parentId, Is.Empty);
        }

        [Test]
        public void AddMotionBinding_IsOneUndoStep()
        {
            _context.AddMotionBinding(DesignerMotionTrigger.ScreenEnter);
            Assert.That(_metadata.screenMotion.bindings, Has.Count.EqualTo(1));
            Undo.PerformUndo();
            Assert.That(_metadata.screenMotion.bindings, Is.Empty);
        }

        [Test]
        public void UndoBackToBaseline_ClearsDirty_AndRedoRestoresIt()
        {
            var element = new DesignerElementMetadata { elementId = "title", text = "Before" };
            _metadata.elements.Add(element);
            _context.SetMetadata(null);
            _context.SetMetadata(_metadata);
            Undo.ClearAll();

            _context.UpdateElement(element, item => item.text = "After", "Change title");
            Assert.That(_context.HasUnsavedChanges, Is.True);

            Undo.PerformUndo();
            Assert.That(_metadata.Find("title").text, Is.EqualTo("Before"));
            Assert.That(_context.HasUnsavedChanges, Is.False);

            Undo.PerformRedo();
            Assert.That(_metadata.Find("title").text, Is.EqualTo("After"));
            Assert.That(_context.HasUnsavedChanges, Is.True);
        }
    }
}

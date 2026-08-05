using emiteat.NexUI.Designer.Editor;
using NUnit.Framework;
using UnityEditor;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Locks the notification semantics of the window's view state.
    /// </summary>
    /// <remarks>
    /// These rules are not uniform and that is the point: zoom notifies on every call, the tool
    /// only when it actually changed, and the bottom tab always because selecting it must open the
    /// drawer even if that tab was already selected. Smoothing them into one tidy rule during the
    /// extraction would have compiled fine and produced a canvas that stops repainting mid-drag,
    /// or an inspector that flickers on every click.
    ///
    /// A private pref prefix keeps the run out of the user's real editor settings, and teardown
    /// removes what it wrote.
    /// </remarks>
    public sealed class DesignerViewStateTests
    {
        private const string Prefix = "NexUI.Tests.ViewState.";

        private DesignerViewState _view;

        [SetUp]
        public void SetUp()
        {
            ClearPrefs();
            _view = new DesignerViewState(Prefix);
        }

        [TearDown]
        public void TearDown() => ClearPrefs();

        private static void ClearPrefs()
        {
            foreach (var key in new[]
                     {
                         "Zoom", "Snap", "GridSize", "Tool", "SidebarTab",
                         "InspectorTab", "BottomTab", "BottomOpen", "BottomHeight"
                     })
                EditorPrefs.DeleteKey(Prefix + key);
        }

        // ---- defaults -------------------------------------------------------

        [Test]
        public void New_StartsFromTheDocumentedDefaults()
        {
            Assert.AreEqual(0.5f, _view.Zoom, 0.0001f);
            Assert.IsTrue(_view.SnapEnabled);
            Assert.AreEqual(8f, _view.GridSize, 0.0001f);
            Assert.AreEqual(DesignerTool.Select, _view.CurrentTool);
            Assert.IsFalse(_view.BottomDrawerOpen);
        }

        [Test]
        public void New_RestoresWhatThePreviousSessionLeft()
        {
            _view.SetZoom(1.25f);
            _view.SetTool(DesignerTool.Move);

            var restored = new DesignerViewState(Prefix);

            Assert.AreEqual(1.25f, restored.Zoom, 0.0001f);
            Assert.AreEqual(DesignerTool.Move, restored.CurrentTool);
        }

        // ---- canvas: notifies every time ------------------------------------

        [Test]
        public void SetZoom_ReportsAChangeEvenWhenTheValueIsTheSame()
        {
            _view.SetZoom(1f);

            Assert.IsTrue(_view.SetZoom(1f),
                "A viewport mid-drag repaints on every step, including the steps the clamp flattens.");
        }

        [Test]
        public void SetZoom_ClampsToTheSupportedRange()
        {
            _view.SetZoom(99f);
            Assert.AreEqual(2.0f, _view.Zoom, 0.0001f);

            _view.SetZoom(-5f);
            Assert.AreEqual(0.15f, _view.Zoom, 0.0001f);
        }

        [Test]
        public void SetSnapAndGridSize_AlwaysReportAChange()
        {
            _view.SetSnap(true);
            Assert.IsTrue(_view.SetSnap(true));

            _view.SetGridSize(8f);
            Assert.IsTrue(_view.SetGridSize(8f));
        }

        [Test]
        public void SetGridSize_ClampsToTheSupportedRange()
        {
            _view.SetGridSize(1000f);
            Assert.AreEqual(64f, _view.GridSize, 0.0001f);

            _view.SetGridSize(0f);
            Assert.AreEqual(1f, _view.GridSize, 0.0001f);
        }

        // ---- shell: notifies only on a real change --------------------------

        [Test]
        public void SetTool_ReportsNoChangeWhenTheToolIsAlreadyActive()
        {
            Assert.IsTrue(_view.SetTool(DesignerTool.Move));
            Assert.IsFalse(_view.SetTool(DesignerTool.Move),
                "Rebuilding the shell for a tool that was already active is a visible flicker.");
        }

        [Test]
        public void SetSidebarTab_ReportsNoChangeWhenAlreadySelected()
        {
            _view.SetSidebarTab(DesignerSidebarTab.Layers);
            Assert.IsFalse(_view.SetSidebarTab(DesignerSidebarTab.Layers));
        }

        [Test]
        public void SetInspectorTab_ReportsNoChangeWhenAlreadySelected()
        {
            _view.SetInspectorTab(DesignerInspectorTab.Design);
            Assert.IsFalse(_view.SetInspectorTab(DesignerInspectorTab.Design));
        }

        [Test]
        public void SetBottomDrawerOpen_ReportsNoChangeWhenAlreadyInThatState()
        {
            _view.SetBottomDrawerOpen(true);
            Assert.IsFalse(_view.SetBottomDrawerOpen(true));
        }

        // ---- the exception --------------------------------------------------

        [Test]
        public void SetBottomTab_AlwaysReportsAChangeSoItCanOpenTheDrawer()
        {
            _view.SetBottomTab(DesignerBottomTab.Validation, open: false);

            Assert.IsTrue(_view.SetBottomTab(DesignerBottomTab.Validation, open: true),
                "'Show me validation' must open the drawer even when validation was already the " +
                "selected tab behind a closed drawer.");
            Assert.IsTrue(_view.BottomDrawerOpen);
        }

        [Test]
        public void SetBottomDrawerHeight_ClampsAndAlwaysReportsAChange()
        {
            Assert.IsTrue(_view.SetBottomDrawerHeight(10f));
            Assert.AreEqual(180f, _view.BottomDrawerHeight, 0.0001f);

            _view.SetBottomDrawerHeight(9999f);
            Assert.AreEqual(520f, _view.BottomDrawerHeight, 0.0001f);
        }
    }
}

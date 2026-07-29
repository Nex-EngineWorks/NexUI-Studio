using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.UI.Shell;
using NUnit.Framework;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// The detach/re-dock state behind pulling Designer panes into their own windows. The failure
    /// that matters is a pane going missing - detached in the saved state but with no window open -
    /// so the round trip and the reset escape hatch are pinned here.
    /// </summary>
    public sealed class DesignerPaneLayoutTests
    {
        private readonly List<DesignerPaneKind> _regions = new()
        {
            DesignerPaneKind.Explorer, DesignerPaneKind.Inspector, DesignerPaneKind.Output
        };

        [SetUp]
        [TearDown]
        public void ResetLayout() => DesignerPaneLayout.ResetLayout();

        [Test]
        public void EveryRegionStartsDockedInTheShell()
        {
            foreach (var kind in _regions)
                Assert.IsFalse(DesignerPaneLayout.IsDetached(kind), $"{kind} should start in the Designer window.");
        }

        [Test]
        public void DetachAndRedockRoundTrips()
        {
            DesignerPaneLayout.SetDetached(DesignerPaneKind.Inspector, true);
            Assert.IsTrue(DesignerPaneLayout.IsDetached(DesignerPaneKind.Inspector));
            Assert.IsFalse(DesignerPaneLayout.IsDetached(DesignerPaneKind.Explorer), "Detaching one region must not move the others.");

            DesignerPaneLayout.SetDetached(DesignerPaneKind.Inspector, false);
            Assert.IsFalse(DesignerPaneLayout.IsDetached(DesignerPaneKind.Inspector));
        }

        [Test]
        public void ChangedFiresOnlyWhenTheLayoutActuallyChanges()
        {
            var count = 0;
            void Handler() => count++;

            DesignerPaneLayout.Changed += Handler;
            try
            {
                DesignerPaneLayout.SetDetached(DesignerPaneKind.Output, true);
                Assert.AreEqual(1, count);

                // Re-detaching an already detached pane happens on every domain reload; a shell
                // rebuild for a no-op would throw away the viewport for nothing.
                DesignerPaneLayout.SetDetached(DesignerPaneKind.Output, true);
                Assert.AreEqual(1, count);

                DesignerPaneLayout.SetDetached(DesignerPaneKind.Output, false);
                Assert.AreEqual(2, count);
            }
            finally
            {
                DesignerPaneLayout.Changed -= Handler;
            }
        }

        [Test]
        public void ResetLayout_BringsEverythingBack()
        {
            foreach (var kind in _regions)
                DesignerPaneLayout.SetDetached(kind, true);

            DesignerPaneLayout.ResetLayout();

            foreach (var kind in _regions)
                Assert.IsFalse(DesignerPaneLayout.IsDetached(kind), $"{kind} should be docked again after a reset.");
        }

        [Test]
        public void NonRegionPanesAreNeverReportedAsDetached()
        {
            // Hierarchy/Library/Project open as extra windows; they never leave a hole in the shell,
            // so treating them as detached would blank a region that is still there.
            foreach (var kind in new[] { DesignerPaneKind.Hierarchy, DesignerPaneKind.Library, DesignerPaneKind.Project })
            {
                DesignerPaneLayout.SetDetached(kind, true);
                Assert.IsFalse(DesignerPaneLayout.IsDetached(kind));
            }
        }

        [Test]
        public void EveryKindHasADescriptorThatCanBuildAView()
        {
            foreach (DesignerPaneKind kind in System.Enum.GetValues(typeof(DesignerPaneKind)))
            {
                var descriptor = DesignerPaneLayout.Get(kind);
                Assert.IsNotNull(descriptor, $"{kind} has no descriptor.");
                Assert.IsNotNull(descriptor.Create, $"{kind} cannot build a view.");
                Assert.IsFalse(string.IsNullOrEmpty(DesignerPaneLayout.Title(kind)), $"{kind} has no title.");
            }
        }
    }
}

using System.Linq;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Designer.Editor.Compiler;
using emiteat.NexUI.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Covers the guarantees the runtime builder is allowed to assume: parents precede children,
    /// ids are unique, hierarchy is a tree, and identical input compiles to an identical program.
    /// </summary>
    /// <remarks>
    /// These are the assumptions that let <c>NexUGuiScreenBuilder</c> be a single forward pass
    /// with no defensive branches. If one of them stops holding, the failure shows up on a player
    /// device as a broken screen rather than here as a red test - which is why they are asserted
    /// at the compiler boundary rather than trusted.
    /// </remarks>
    public sealed class NexScreenCompilerTests
    {
        private DesignerMetadataAsset _metadata;

        [TearDown]
        public void TearDown()
        {
            if (_metadata != null) Object.DestroyImmediate(_metadata);
            _metadata = null;
        }

        // ---- helpers --------------------------------------------------------

        private DesignerMetadataAsset NewScreen(string screenId = "TestScreen")
        {
            _metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            _metadata.screenId = screenId;
            return _metadata;
        }

        private static DesignerElementMetadata Element(string id, string type, string parentId = null,
            int siblingIndex = 0)
        {
            return new DesignerElementMetadata
            {
                elementId = id,
                stableId = "stable-" + id,
                elementType = type,
                parentId = parentId,
                siblingIndex = siblingIndex,
                rect = new Rect(10f, 20f, 100f, 40f),
                runtimeVisible = true
            };
        }

        private static bool HasCode(NexCompileResult result, string code)
            => result.Diagnostics.Any(d => d.Code == code);

        // ---- happy path -----------------------------------------------------

        [Test]
        public void Compile_LowersAuthoringTypesToNodeKinds()
        {
            var screen = NewScreen();
            screen.elements.Add(Element("Root", "Panel"));
            screen.elements.Add(Element("Title", "Label", "Root"));
            screen.elements.Add(Element("Start", "Button", "Root", 1));

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsTrue(result.Succeeded, result.Diagnostics.Format());
            Assert.AreEqual(3, result.Program.Nodes.Length);
            Assert.AreEqual(NexNodeKind.Panel, result.Program.Nodes[0].Kind);
            Assert.AreEqual(NexNodeKind.Label, result.Program.Nodes[1].Kind);
            Assert.AreEqual(NexNodeKind.Button, result.Program.Nodes[2].Kind);
        }

        [Test]
        public void Compile_OrdersParentsBeforeChildren()
        {
            var screen = NewScreen();

            // Deliberately authored child-first: the runtime builder relies on the compiler
            // fixing this, not on the authoring list happening to be in a useful order.
            screen.elements.Add(Element("Child", "Label", "Root"));
            screen.elements.Add(Element("Root", "Panel"));

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsTrue(result.Succeeded, result.Diagnostics.Format());
            Assert.AreEqual("Root", result.Program.Nodes[0].Name);
            Assert.AreEqual("Child", result.Program.Nodes[1].Name);
            Assert.AreEqual(-1, result.Program.Nodes[0].ParentIndex);
            Assert.AreEqual(0, result.Program.Nodes[1].ParentIndex);
        }

        [Test]
        public void Compile_BuildsSourceMapBackToAuthoringElements()
        {
            var screen = NewScreen();
            screen.elements.Add(Element("Root", "Panel"));
            screen.elements.Add(Element("Title", "Label", "Root"));

            var result = NexScreenCompiler.Compile(screen);
            var index = result.Program.IndexOfNode("stable-Title");

            Assert.AreEqual(1, index);
            Assert.AreEqual("Root/Title", result.Program.SourceMap.PathOfIndex(index));
        }

        // ---- determinism ----------------------------------------------------

        [Test]
        public void Compile_IsDeterministic_ForTheSameInput()
        {
            var screen = NewScreen();
            screen.elements.Add(Element("Root", "Panel"));
            screen.elements.Add(Element("Start", "Button", "Root"));

            var first = NexScreenCompiler.Compile(screen);
            var second = NexScreenCompiler.Compile(screen);

            Assert.AreEqual(first.Program.ContentHash, second.Program.ContentHash);
            Assert.AreEqual(first.Program.ToCanonicalString(), second.Program.ToCanonicalString());
        }

        [Test]
        public void Compile_IsDeterministic_RegardlessOfAuthoringListOrder()
        {
            var forward = NewScreen();
            forward.elements.Add(Element("Root", "Panel"));
            forward.elements.Add(Element("A", "Label", "Root", 0));
            forward.elements.Add(Element("B", "Label", "Root", 1));
            var forwardHash = NexScreenCompiler.Compile(forward).Program.ContentHash;
            Object.DestroyImmediate(_metadata);

            var reversed = NewScreen();
            reversed.elements.Add(Element("B", "Label", "Root", 1));
            reversed.elements.Add(Element("A", "Label", "Root", 0));
            reversed.elements.Add(Element("Root", "Panel"));
            var reversedHash = NexScreenCompiler.Compile(reversed).Program.ContentHash;

            Assert.AreEqual(forwardHash, reversedHash,
                "Authoring list order must not change the compiled output, or the compile cache is meaningless.");
        }

        // ---- structural validation ------------------------------------------

        [Test]
        public void Compile_ReportsDuplicateElementId()
        {
            var screen = NewScreen();
            screen.elements.Add(Element("Root", "Panel"));
            screen.elements.Add(Element("Root", "Panel"));

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(HasCode(result, NexDiagnosticCodes.DuplicateElementId));
        }

        [Test]
        public void Compile_ReportsMissingScreenId()
        {
            var screen = NewScreen(string.Empty);
            screen.elements.Add(Element("Root", "Panel"));

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(HasCode(result, NexDiagnosticCodes.ScreenIdMissing));
        }

        [Test]
        public void Compile_ReportsParentOutsideScreen()
        {
            var screen = NewScreen();
            screen.elements.Add(Element("Orphan", "Label", "NotHere"));

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(HasCode(result, NexDiagnosticCodes.ParentNotFound));
        }

        [Test]
        public void Compile_ReportsParentCycleWithoutRecursingForever()
        {
            var screen = NewScreen();
            screen.elements.Add(Element("A", "Panel", "B"));
            screen.elements.Add(Element("B", "Panel", "A"));

            var result = NexScreenCompiler.Compile(screen);

            // Neither element is reachable from a root, so nothing is emitted - but the failure
            // has to name the elements involved rather than just reporting an empty screen.
            Assert.AreEqual(0, result.Program.Nodes.Length);
            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(HasCode(result, NexDiagnosticCodes.ParentCycle));
        }

        [Test]
        public void Compile_WarnsOnEmptyScreen()
        {
            var result = NexScreenCompiler.Compile(NewScreen());

            Assert.IsTrue(HasCode(result, NexDiagnosticCodes.EmptyScreen));
            Assert.IsTrue(result.Succeeded, "An empty screen is a warning, not a failure.");
        }

        // ---- binding validation ---------------------------------------------

        [Test]
        public void Compile_StripsCommandBindingFromNonClickableElement()
        {
            var screen = NewScreen();
            var label = Element("Title", "Label");
            label.binding.commandKey = "Game.Start";
            screen.elements.Add(label);

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsTrue(HasCode(result, NexDiagnosticCodes.CommandOnNonClickableNode));
            Assert.AreEqual(string.Empty, result.Program.Nodes[0].CommandId,
                "A command that can never fire must not reach the runtime.");
        }

        [Test]
        public void Compile_KeepsCommandBindingOnButton()
        {
            var screen = NewScreen();
            var button = Element("Start", "Button");
            button.binding.commandKey = "Game.Start";
            screen.elements.Add(button);

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsTrue(result.Succeeded, result.Diagnostics.Format());
            Assert.AreEqual("Game.Start", result.Program.Nodes[0].CommandId);
        }

        // ---- feature manifest ------------------------------------------------

        [Test]
        public void Compile_RecordsWhyEachFeatureIsIncluded()
        {
            var screen = NewScreen();
            var button = Element("Start", "Button");
            button.binding.commandKey = "Game.Start";
            screen.elements.Add(button);

            var features = NexScreenCompiler.Compile(screen).Program.Features;

            Assert.IsTrue(features.Requires(NexFeatures.CommandBinding));
            var requirement = features.Requirements.First(r => r.FeatureId == NexFeatures.CommandBinding);
            Assert.AreEqual("stable-Start", requirement.NodeId);
            StringAssert.Contains("Game.Start", requirement.Reason);
        }

        [Test]
        public void Compile_DoesNotRequireFeaturesTheScreenNeverUses()
        {
            var screen = NewScreen();
            screen.elements.Add(Element("Root", "Panel"));

            var features = NexScreenCompiler.Compile(screen).Program.Features;

            Assert.IsFalse(features.Requires(NexFeatures.CommandBinding));
            Assert.IsFalse(features.Requires(NexFeatures.Text),
                "A panel-only screen must not drag the text subsystem into the build.");
        }
    }
}

using System.Linq;
using emiteat.NexUI.Accessibility;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Designer.Editor.Compiler;
using emiteat.NexUI.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Covers the promise an automation id makes: a test that names one keeps finding the same
    /// element after the screen is renamed and rearranged, and never finds the wrong one.
    /// </summary>
    public sealed class NexAutomationIdTests
    {
        private DesignerMetadataAsset _metadata;

        [TearDown]
        public void TearDown()
        {
            if (_metadata != null) Object.DestroyImmediate(_metadata);
            _metadata = null;
        }

        private DesignerMetadataAsset NewScreen()
        {
            _metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            _metadata.screenId = "TestScreen";
            return _metadata;
        }

        private static DesignerElementMetadata Element(string id, string automationId = null,
            AccessibilityRole role = AccessibilityRole.None, string type = "Button")
            => new DesignerElementMetadata
            {
                elementId = id,
                stableId = "stable-" + id,
                elementType = type,
                automationId = automationId,
                accessibilityRole = role,
                runtimeVisible = true
            };

        // ---- lowering -------------------------------------------------------

        [Test]
        public void Compile_CarriesTheAutomationIdIntoTheProgram()
        {
            var screen = NewScreen();
            screen.elements.Add(Element("Purchase", "store.item.purchase"));

            var program = NexScreenCompiler.Compile(screen).Program;

            Assert.AreEqual("store.item.purchase", program.Nodes[0].AutomationId);
            Assert.AreEqual(0, program.IndexOfAutomationId("store.item.purchase"));
        }

        [Test]
        public void Compile_CarriesTheSemanticRoleIntoTheProgram()
        {
            var screen = NewScreen();
            screen.elements.Add(Element("Purchase", "store.item.purchase", AccessibilityRole.Button));

            var program = NexScreenCompiler.Compile(screen).Program;

            Assert.AreEqual(AccessibilityRole.Button, program.Nodes[0].Role,
                "A test and a screen reader read the same role field.");
        }

        [Test]
        public void IndexOfAutomationId_ReturnsMinusOneForAnUnknownId()
        {
            var screen = NewScreen();
            screen.elements.Add(Element("Purchase", "store.item.purchase"));

            var program = NexScreenCompiler.Compile(screen).Program;

            Assert.AreEqual(-1, program.IndexOfAutomationId("nope"));
            Assert.AreEqual(-1, program.IndexOfAutomationId(null));
            Assert.AreEqual(-1, program.IndexOfAutomationId(string.Empty),
                "Elements without an automation id must never be matched by an empty lookup.");
        }

        [Test]
        public void Compile_LeavesElementsWithoutAnAutomationIdEmpty()
        {
            var screen = NewScreen();
            screen.elements.Add(Element("Plain"));

            Assert.AreEqual(string.Empty, NexScreenCompiler.Compile(screen).Program.Nodes[0].AutomationId);
        }

        // ---- the promise ----------------------------------------------------

        [Test]
        public void AutomationId_SurvivesARename()
        {
            var screen = NewScreen();
            screen.elements.Add(Element("PurchaseButton", "store.item.purchase"));
            var before = NexScreenCompiler.Compile(screen).Program.IndexOfAutomationId("store.item.purchase");

            // The author tidies up the screen; the test suite must not notice.
            screen.elements[0].elementId = "BuyNowButton";
            var after = NexScreenCompiler.Compile(screen).Program.IndexOfAutomationId("store.item.purchase");

            Assert.AreEqual(before, after);
            Assert.AreNotEqual(-1, after);
        }

        [Test]
        public void AutomationId_FollowsTheElementWhenTheScreenIsRearranged()
        {
            var screen = NewScreen();
            screen.elements.Add(Element("Root", type: "Panel"));
            screen.elements.Add(Element("Purchase", "store.item.purchase", type: "Button"));

            var flat = NexScreenCompiler.Compile(screen).Program;
            var flatNode = flat.Nodes[flat.IndexOfAutomationId("store.item.purchase")];

            // Re-parent it under Root - a different node index, the same element.
            screen.elements[1].parentId = "Root";
            var nested = NexScreenCompiler.Compile(screen).Program;
            var nestedNode = nested.Nodes[nested.IndexOfAutomationId("store.item.purchase")];

            Assert.AreEqual(flatNode.NodeId, nestedNode.NodeId);
        }

        // ---- uniqueness -----------------------------------------------------

        [Test]
        public void Compile_FailsWhenTwoElementsShareAnAutomationId()
        {
            var screen = NewScreen();
            screen.elements.Add(Element("A", "store.item.purchase"));
            screen.elements.Add(Element("B", "store.item.purchase"));

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsFalse(result.Succeeded,
                "An ambiguous automation id makes every test that uses it a coin flip.");
            Assert.IsTrue(result.Diagnostics.Any(d => d.Code == NexDiagnosticCodes.DuplicateAutomationId));
        }

        [Test]
        public void Compile_NamesBothOffendersInTheDiagnostic()
        {
            var screen = NewScreen();
            screen.elements.Add(Element("A", "store.item.purchase"));
            screen.elements.Add(Element("B", "store.item.purchase"));

            var diagnostic = NexScreenCompiler.Compile(screen).Diagnostics
                .First(d => d.Code == NexDiagnosticCodes.DuplicateAutomationId);

            StringAssert.Contains("A", diagnostic.Message);
            StringAssert.Contains("B", diagnostic.Message);
        }

        [Test]
        public void Compile_AllowsManyElementsWithoutAnAutomationId()
        {
            var screen = NewScreen();
            screen.elements.Add(Element("A"));
            screen.elements.Add(Element("B"));
            screen.elements.Add(Element("C"));

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsTrue(result.Succeeded, result.Diagnostics.Format());
        }

        // ---- determinism ----------------------------------------------------

        [Test]
        public void Compile_ChangingAnAutomationIdChangesTheContentHash()
        {
            var screen = NewScreen();
            screen.elements.Add(Element("Purchase", "store.item.purchase"));
            var before = NexScreenCompiler.Compile(screen).Program.ContentHash;

            screen.elements[0].automationId = "store.item.buy";
            var after = NexScreenCompiler.Compile(screen).Program.ContentHash;

            Assert.AreNotEqual(before, after,
                "The hash gates republishing, so a changed automation id must reach the build.");
        }
    }
}

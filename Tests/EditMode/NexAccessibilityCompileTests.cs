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
    /// Accessibility from authoring through to the compiled program: the label reaches the node,
    /// the reading order is the document order, and a control nobody can hear about is reported.
    /// </summary>
    public sealed class NexAccessibilityCompileTests
    {
        private DesignerMetadataAsset _metadata;

        [SetUp]
        public void SetUp()
        {
            _metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            _metadata.screenId = "A11yScreen";
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_metadata);

        private DesignerElementMetadata Add(string id, string type, int siblingIndex = 0,
            string parentId = null)
        {
            var element = new DesignerElementMetadata
            {
                elementId = id,
                stableId = id + "-stable",
                displayName = id,
                elementType = type,
                parentId = parentId,
                siblingIndex = siblingIndex,
                rect = new Rect(0, 0, 100, 40),
                runtimeVisible = true
            };
            _metadata.elements.Add(element);
            return element;
        }

        private static NexNodeProgram Node(NexCompileResult result, string name)
            => result.Program.Nodes.First(node => node.Name == name);

        [Test]
        public void AccessibilityLabelReachesTheCompiledNode()
        {
            var button = Add("Close", "Button");
            button.text = "X";
            button.accessibilityLabel = "Close the inventory";
            button.accessibilityRole = AccessibilityRole.Button;

            var result = NexScreenCompiler.Compile(_metadata);
            var node = Node(result, "Close");

            Assert.AreEqual("Close the inventory", node.AccessibilityLabel);
            Assert.AreEqual("Close the inventory", node.AccessibleName,
                "An explicit label wins over the visible text.");
            Assert.AreEqual("X", node.Text, "The visible text is not replaced by the label.");
        }

        [Test]
        public void WithoutALabelTheVisibleTextIsTheAnnouncement()
        {
            var button = Add("Buy", "Button");
            button.text = "Purchase";

            var node = Node(NexScreenCompiler.Compile(_metadata), "Buy");

            Assert.AreEqual(string.Empty, node.AccessibilityLabel);
            Assert.AreEqual("Purchase", node.AccessibleName);
        }

        [Test]
        public void ReadingOrderFollowsTheHierarchy()
        {
            // Deliberately added out of order: the compiled order must come from siblingIndex,
            // not from the order elements happen to sit in the serialized list.
            var second = Add("Second", "Button", siblingIndex: 1);
            second.text = "Second";
            var first = Add("First", "Button", siblingIndex: 0);
            first.text = "First";

            var result = NexScreenCompiler.Compile(_metadata);

            Assert.AreEqual(0, Node(result, "First").FocusOrder);
            Assert.AreEqual(1, Node(result, "Second").FocusOrder);
        }

        [Test]
        public void GroupingPanelsAreSkippedInTheReadingOrder()
        {
            Add("Group", "Panel");
            var button = Add("Inside", "Button", parentId: "Group");
            button.text = "Go";

            var result = NexScreenCompiler.Compile(_metadata);

            Assert.AreEqual(-1, Node(result, "Group").FocusOrder,
                "A container that only groups children must not be stopped on.");
            Assert.IsFalse(Node(result, "Group").IsFocusable);
            Assert.AreEqual(0, Node(result, "Inside").FocusOrder);
        }

        [Test]
        public void InteractiveNodeWithNothingToAnnounceIsReported()
        {
            // The icon-only close button: it draws a glyph, and a screen reader has nothing to say.
            var button = Add("IconOnly", "Button");
            button.text = string.Empty;

            var result = NexScreenCompiler.Compile(_metadata);

            Assert.IsTrue(
                result.Diagnostics.Any(d => d.Code == NexDiagnosticCodes.InteractiveNodeHasNoAccessibleName),
                "An operable node with no accessible name must be reported.");
        }

        [Test]
        public void ANamedControlIsNotReported()
        {
            var button = Add("Named", "Button");
            button.accessibilityLabel = "Confirm";

            var result = NexScreenCompiler.Compile(_metadata);

            Assert.IsFalse(
                result.Diagnostics.Any(d => d.Code == NexDiagnosticCodes.InteractiveNodeHasNoAccessibleName));
        }

        [Test]
        public void DecorativeImagesStaySilentButMeaningfulOnesAreReported()
        {
            var decoration = Add("Ornament", "Image", siblingIndex: 0);
            decoration.accessibilityRole = AccessibilityRole.None;

            var meaningful = Add("Chart", "Image", siblingIndex: 1);
            meaningful.accessibilityRole = AccessibilityRole.Image;

            var result = NexScreenCompiler.Compile(_metadata);
            var reported = result.Diagnostics
                .Where(d => d.Code == NexDiagnosticCodes.ImageRoleWithoutLabel)
                .ToArray();

            Assert.AreEqual(1, reported.Length,
                "Only the image the author marked as meaningful should be reported.");
        }

        [Test]
        public void AccessibilityWarningsDoNotFailTheCompile()
        {
            // A screen still being laid out must stay compilable; the point is to inform, not block.
            Add("IconOnly", "Button");

            var result = NexScreenCompiler.Compile(_metadata);

            Assert.IsNotNull(result.Program);
            Assert.Less((int)result.Diagnostics.MaxSeverity, (int)NexSeverity.Error);
        }
    }
}

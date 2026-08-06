using System.Linq;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Designer.Editor.Compiler;
using emiteat.NexUI.Diagnostics;
using emiteat.NexUI.State;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// That every authored binding reaches the compiled program.
    /// </summary>
    /// <remarks>
    /// The compiler used to lower only the text key and the command id and silently drop the rest -
    /// value, visibility, interactable, class, both modes and both converter keys. The authoring
    /// Inspector still showed them, so a screen could be bound in the editor and inert at runtime
    /// with nothing anywhere saying why. These tests exist to keep that from coming back quietly:
    /// the point is not that each field has a getter, it is that nothing is dropped.
    /// </remarks>
    public sealed class NexBindingLoweringTests
    {
        private DesignerMetadataAsset _metadata;

        [SetUp]
        public void SetUp()
        {
            _metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            _metadata.screenId = "BindingScreen";
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_metadata);

        private DesignerElementMetadata Add(string id, string type)
        {
            var element = new DesignerElementMetadata
            {
                elementId = id,
                stableId = id + "-stable",
                displayName = id,
                elementType = type,
                rect = new Rect(0, 0, 100, 40),
                runtimeVisible = true,
                binding = new DesignerBindingMetadata()
            };
            _metadata.elements.Add(element);
            return element;
        }

        private NexNodeProgram Node(string name)
            => NexScreenCompiler.Compile(_metadata).Program.Nodes.First(n => n.Name == name);

        [Test]
        public void EveryAuthoredBindingFieldReachesTheProgram()
        {
            var button = Add("Buy", "Button");
            button.text = "Buy";
            button.binding.textKey = "shop.label";
            button.binding.valueKey = "shop.price";
            button.binding.visibilityKey = "shop.visible";
            button.binding.interactableKey = "shop.affordable";
            button.binding.classKey = "shop.style";
            button.binding.commandKey = "shop.purchase";
            button.binding.textConverterKey = "currency";
            button.binding.valueConverterKey = "ratio";

            var node = Node("Buy");

            Assert.AreEqual("shop.label", node.TextBindingKey);
            Assert.AreEqual("shop.price", node.ValueBindingKey);
            Assert.AreEqual("shop.visible", node.VisibilityBindingKey);
            Assert.AreEqual("shop.affordable", node.InteractableBindingKey);
            Assert.AreEqual("shop.style", node.ClassBindingKey);
            Assert.AreEqual("shop.purchase", node.CommandId);
            Assert.AreEqual("currency", node.TextConverterKey);
            Assert.AreEqual("ratio", node.ValueConverterKey);
        }

        [Test]
        public void BindingModesSurviveTheCompile()
        {
            var button = Add("Field", "Button");
            button.text = "x";
            button.binding.textKey = "form.name";
            button.binding.textMode = UIBindingMode.TwoWay;
            button.binding.valueKey = "form.amount";
            button.binding.valueMode = UIBindingMode.OneWayToSource;

            var node = Node("Field");

            Assert.AreEqual(UIBindingMode.TwoWay, node.TextBindingMode);
            Assert.AreEqual(UIBindingMode.OneWayToSource, node.ValueBindingMode);
            Assert.IsTrue(node.TextWritesBack);
            Assert.IsTrue(node.ValueWritesBack);
        }

        [Test]
        public void OneWayIsTheDefaultAndDoesNotWriteBack()
        {
            var label = Add("Label", "Label");
            label.binding.textKey = "player.name";

            var node = Node("Label");

            Assert.AreEqual(UIBindingMode.OneWay, node.TextBindingMode);
            Assert.IsFalse(node.TextWritesBack);
        }

        [Test]
        public void HasAnyBindingIsFalseOnlyWhenNothingIsBound()
        {
            Add("Plain", "Panel");
            Assert.IsFalse(Node("Plain").HasAnyBinding);

            Add("Bound", "Panel").binding.visibilityKey = "hud.visible";
            Assert.IsTrue(Node("Bound").HasAnyBinding);
        }

        [Test]
        public void ACommandOnlyNodeIsNotConsideredBound()
        {
            // A command is dispatched, not a value that flows in. Counting it as a binding would
            // make the builder run its binding pass on every button for nothing.
            var button = Add("Go", "Button");
            button.binding.commandKey = "game.start";

            Assert.IsFalse(Node("Go").HasAnyBinding);
        }

        [Test]
        public void AValueBindingIsKeptAndReportedRatherThanDropped()
        {
            // No compiled node kind holds a scalar yet. Dropping the key would leave the author
            // with an Inspector that shows a binding and a screen that ignores it.
            var panel = Add("Bar", "Panel");
            panel.binding.valueKey = "player.health";

            var result = NexScreenCompiler.Compile(_metadata);

            Assert.AreEqual("player.health",
                result.Program.Nodes.First(n => n.Name == "Bar").ValueBindingKey);
            Assert.IsTrue(
                result.Diagnostics.Any(d => d.Code == NexDiagnosticCodes.ValueBindingHasNoBackendTarget));
        }

        [Test]
        public void TwoWayTextOnALabelIsReported()
        {
            var label = Add("Readout", "Label");
            label.binding.textKey = "player.name";
            label.binding.textMode = UIBindingMode.TwoWay;

            var result = NexScreenCompiler.Compile(_metadata);

            Assert.IsTrue(
                result.Diagnostics.Any(d => d.Code == NexDiagnosticCodes.TwoWayBindingOnReadOnlyNode),
                "A label cannot be edited, so its two-way half has no source.");
        }

        [Test]
        public void AConverterWithNoBindingIsReported()
        {
            var label = Add("Orphan", "Label");
            label.binding.textConverterKey = "currency";

            var result = NexScreenCompiler.Compile(_metadata);

            Assert.IsTrue(
                result.Diagnostics.Any(d => d.Code == NexDiagnosticCodes.ConverterKeyWithoutBinding));
        }

        [Test]
        public void BindingDiagnosticsAreAttributedToTheBindingFeature()
        {
            var panel = Add("Bar", "Panel");
            panel.binding.valueKey = "player.health";

            var reported = NexScreenCompiler.Compile(_metadata).Diagnostics
                .First(d => d.Code == NexDiagnosticCodes.ValueBindingHasNoBackendTarget);

            Assert.AreEqual(NexDiagnosticFeatures.Binding, reported.Context.Feature);
            Assert.AreEqual("Bar", reported.Context.Handler);
        }

        [Test]
        public void BindingProblemsDoNotFailTheCompile()
        {
            var panel = Add("Bar", "Panel");
            panel.binding.valueKey = "player.health";
            panel.binding.textMode = UIBindingMode.TwoWay;

            var result = NexScreenCompiler.Compile(_metadata);

            Assert.IsNotNull(result.Program);
            Assert.Less((int)result.Diagnostics.MaxSeverity, (int)NexSeverity.Error);
        }
    }
}

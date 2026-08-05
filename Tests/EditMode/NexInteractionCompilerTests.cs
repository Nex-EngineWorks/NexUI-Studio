using System.Linq;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Designer.Editor.Compiler;
using emiteat.NexUI.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Covers what the interaction runtime is allowed to assume: targets are resolved indices,
    /// values are pre-parsed, and a rule that could never run never reaches the program.
    /// </summary>
    public sealed class NexInteractionCompilerTests
    {
        private DesignerMetadataAsset _metadata;

        [TearDown]
        public void TearDown()
        {
            if (_metadata != null) Object.DestroyImmediate(_metadata);
            _metadata = null;
        }

        // ---- helpers --------------------------------------------------------

        private DesignerMetadataAsset NewScreen()
        {
            _metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            _metadata.screenId = "TestScreen";
            return _metadata;
        }

        private static DesignerElementMetadata Element(string id, string type, string parentId = null)
            => new DesignerElementMetadata
            {
                elementId = id,
                stableId = "stable-" + id,
                elementType = type,
                parentId = parentId,
                runtimeVisible = true,
                rect = new Rect(0f, 0f, 100f, 40f)
            };

        private static DesignerInteractionRule Rule(DesignerInteractionTrigger trigger,
            params DesignerInteractionAction[] actions)
        {
            var rule = new DesignerInteractionRule { trigger = trigger, ruleId = "rule-1" };
            rule.actions.AddRange(actions);
            return rule;
        }

        private static DesignerInteractionAction Command(string commandId)
            => new DesignerInteractionAction
            {
                kind = DesignerInteractionActionKind.ExecuteCommand,
                commandId = commandId
            };

        private static bool HasCode(NexCompileResult result, string code)
            => result.Diagnostics.Any(d => d.Code == code);

        // ---- lowering -------------------------------------------------------

        [Test]
        public void Compile_LowersRuleOntoOwningNode()
        {
            var screen = NewScreen();
            var button = Element("Start", "Button");
            button.interactions.Add(Rule(DesignerInteractionTrigger.OnClick, Command("Game.Start")));
            screen.elements.Add(button);

            var result = NexScreenCompiler.Compile(screen);
            var interactions = result.Program.Interactions;

            Assert.IsTrue(result.Succeeded, result.Diagnostics.Format());
            Assert.AreEqual(1, interactions.Rules.Count);
            Assert.AreEqual(0, interactions.Rules[0].NodeIndex);
            Assert.AreEqual(NexTrigger.OnClick, interactions.Rules[0].Trigger);
            Assert.AreEqual(1, interactions.Rules[0].ActionCount);
            Assert.AreEqual("Game.Start", interactions.Actions[0].CommandId);
        }

        [Test]
        public void Compile_ResolvesTargetElementIdToNodeIndex()
        {
            var screen = NewScreen();
            var button = Element("Start", "Button");
            button.interactions.Add(Rule(DesignerInteractionTrigger.OnClick,
                new DesignerInteractionAction
                {
                    kind = DesignerInteractionActionKind.SetVisible,
                    targetElementId = "Title",
                    boolValue = false
                }));
            screen.elements.Add(button);
            screen.elements.Add(Element("Title", "Label"));

            var result = NexScreenCompiler.Compile(screen);
            var action = result.Program.Interactions.Actions[0];

            Assert.IsTrue(result.Succeeded, result.Diagnostics.Format());
            Assert.AreEqual(result.Program.IndexOfNode("stable-Title"), action.TargetNodeIndex,
                "The runtime must never have to look an element up by name.");
        }

        [Test]
        public void Compile_ParsesNumericValuesOnce()
        {
            var screen = NewScreen();
            var button = Element("Start", "Button");
            var rule = Rule(DesignerInteractionTrigger.OnClick,
                new DesignerInteractionAction
                {
                    kind = DesignerInteractionActionKind.SetState,
                    stateKey = "Player.Gold",
                    value = "125.5"
                });
            rule.conditionKey = "Player.Level";
            rule.conditionValue = "10";
            rule.comparison = DesignerInteractionComparison.GreaterThan;
            button.interactions.Add(rule);
            screen.elements.Add(button);

            var result = NexScreenCompiler.Compile(screen);
            var compiled = result.Program.Interactions.Rules[0];

            Assert.IsTrue(compiled.ConditionIsNumeric);
            Assert.AreEqual(10d, compiled.ConditionNumber, 0.0001d);
            Assert.IsTrue(result.Program.Interactions.Actions[0].IsNumeric);
            Assert.AreEqual(125.5d, result.Program.Interactions.Actions[0].NumberValue, 0.0001d);
        }

        [Test]
        public void Compile_KeepsTextValuesAsText()
        {
            var screen = NewScreen();
            var button = Element("Start", "Button");
            button.interactions.Add(Rule(DesignerInteractionTrigger.OnClick,
                new DesignerInteractionAction
                {
                    kind = DesignerInteractionActionKind.SetState,
                    stateKey = "Menu.Mode",
                    value = "Ready"
                }));
            screen.elements.Add(button);

            var action = NexScreenCompiler.Compile(screen).Program.Interactions.Actions[0];

            Assert.IsFalse(action.IsNumeric);
            Assert.AreEqual("Ready", action.StringValue);
        }

        [Test]
        public void Compile_SkipsDisabledRules()
        {
            var screen = NewScreen();
            var button = Element("Start", "Button");
            var rule = Rule(DesignerInteractionTrigger.OnClick, Command("Game.Start"));
            rule.enabled = false;
            button.interactions.Add(rule);
            screen.elements.Add(button);

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsTrue(result.Program.Interactions.IsEmpty);
            Assert.IsTrue(result.Succeeded, "A disabled rule is a choice, not a problem.");
        }

        // ---- validation -----------------------------------------------------

        [Test]
        public void Compile_DropsClickRuleOnNodeThatCannotBeClicked()
        {
            var screen = NewScreen();
            var label = Element("Title", "Label");
            label.interactions.Add(Rule(DesignerInteractionTrigger.OnClick, Command("Game.Start")));
            screen.elements.Add(label);

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsTrue(HasCode(result, NexDiagnosticCodes.TriggerNotRaisableByNode));
            Assert.IsTrue(result.Program.Interactions.IsEmpty,
                "A rule that can never fire must not reach the runtime.");
        }

        [Test]
        public void Compile_FailsOnTargetThatIsNotOnTheScreen()
        {
            var screen = NewScreen();
            var button = Element("Start", "Button");
            button.interactions.Add(Rule(DesignerInteractionTrigger.OnClick,
                new DesignerInteractionAction
                {
                    kind = DesignerInteractionActionKind.SetText,
                    targetElementId = "Ghost",
                    value = "hi"
                }));
            screen.elements.Add(button);

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(HasCode(result, NexDiagnosticCodes.InteractionTargetNotFound));
        }

        [Test]
        public void Compile_FailsOnActionMissingItsRequiredValue()
        {
            var screen = NewScreen();
            var button = Element("Start", "Button");
            button.interactions.Add(Rule(DesignerInteractionTrigger.OnClick, Command(string.Empty)));
            screen.elements.Add(button);

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(HasCode(result, NexDiagnosticCodes.InteractionActionIncomplete));
        }

        [Test]
        public void Compile_WarnsOnRuleWithNoActions()
        {
            var screen = NewScreen();
            var button = Element("Start", "Button");
            button.interactions.Add(Rule(DesignerInteractionTrigger.OnClick));
            screen.elements.Add(button);

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsTrue(HasCode(result, NexDiagnosticCodes.InteractionHasNoActions));
            Assert.IsTrue(result.Succeeded, "An empty rule is a warning, not a failed screen.");
        }

        [Test]
        public void Compile_DropsWholeRuleWhenOneActionIsInvalid()
        {
            var screen = NewScreen();
            var button = Element("Start", "Button");
            button.interactions.Add(Rule(DesignerInteractionTrigger.OnClick,
                Command("Game.Start"),
                new DesignerInteractionAction
                {
                    kind = DesignerInteractionActionKind.SetText,
                    targetElementId = "Ghost"
                }));
            screen.elements.Add(button);

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsTrue(result.Program.Interactions.IsEmpty,
                "Half a rule is harder to diagnose than none of it.");
        }

        // ---- feature manifest -----------------------------------------------

        [Test]
        public void Compile_RequiresTheInteractionFeatureOnlyWhenRulesExist()
        {
            var without = NewScreen();
            without.elements.Add(Element("Start", "Button"));
            Assert.IsFalse(NexScreenCompiler.Compile(without).Program.Features.Requires(NexFeatures.Interaction));
            Object.DestroyImmediate(_metadata);

            var with = NewScreen();
            var button = Element("Start", "Button");
            button.interactions.Add(Rule(DesignerInteractionTrigger.OnClick, Command("Game.Start")));
            with.elements.Add(button);

            Assert.IsTrue(NexScreenCompiler.Compile(with).Program.Features.Requires(NexFeatures.Interaction));
        }

        [Test]
        public void Compile_IsDeterministicWithInteractions()
        {
            var screen = NewScreen();
            var button = Element("Start", "Button");
            button.interactions.Add(Rule(DesignerInteractionTrigger.OnClick, Command("Game.Start")));
            screen.elements.Add(button);

            var first = NexScreenCompiler.Compile(screen).Program.ContentHash;
            var second = NexScreenCompiler.Compile(screen).Program.ContentHash;

            Assert.AreEqual(first, second);
        }
    }
}

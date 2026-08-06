using System.Linq;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Designer.Editor.Compiler;
using emiteat.NexUI.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Covers the trigger set beyond click / show / hide.
    /// </summary>
    /// <remarks>
    /// The engine already handled conditions, actions, propagation and delays; what was missing was
    /// anything to start them but a button click. These tests pin the two rules that decide whether
    /// a newly authored trigger can actually reach the runtime - which node kinds may raise it, and
    /// which triggers travel through the element tree.
    /// </remarks>
    public sealed class NexInteractionTriggerTests
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
            _metadata.screenId = "TriggerScreen";
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
            DesignerInteractionPhase phase = DesignerInteractionPhase.Target)
        {
            var rule = new DesignerInteractionRule
            {
                trigger = trigger,
                phase = phase,
                ruleId = "rule-" + trigger + "-" + phase
            };
            rule.actions.Add(new DesignerInteractionAction
            {
                kind = DesignerInteractionActionKind.ExecuteCommand,
                commandId = "Game.Ping"
            });
            return rule;
        }

        private static bool HasCode(NexCompileResult result, string code)
            => result.Diagnostics.Any(d => d.Code == code);

        // ---- which nodes may raise what --------------------------------------

        [Test]
        public void PointerTriggersSurviveOnANodeThatIsNotAButton()
        {
            // A hover highlight on a panel is ordinary authoring, and needs no Selectable - the
            // raycaster reaching the graphic is enough.
            var screen = NewScreen();
            var panel = Element("Card", "Panel");
            panel.interactions.Add(Rule(DesignerInteractionTrigger.OnPointerEnter));
            screen.elements.Add(panel);

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsFalse(HasCode(result, NexDiagnosticCodes.TriggerNotRaisableByNode),
                "a pointer trigger must not require the node to be clickable");
            Assert.IsFalse(result.Program.Interactions.IsEmpty, "the rule must reach the runtime");
        }

        [Test]
        public void SubmitOnANodeThatCannotBeFocusedIsDropped()
        {
            // Submit is delivered to whatever holds focus. A Label never does, so the rule would
            // sit there looking authored and never fire.
            var screen = NewScreen();
            var label = Element("Title", "Label");
            label.interactions.Add(Rule(DesignerInteractionTrigger.OnSubmit));
            screen.elements.Add(label);

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsTrue(HasCode(result, NexDiagnosticCodes.TriggerNotRaisableByNode));
            Assert.IsTrue(result.Program.Interactions.IsEmpty);
        }

        [Test]
        public void SubmitOnAButtonIsKept()
        {
            var screen = NewScreen();
            var button = Element("Start", "Button");
            button.interactions.Add(Rule(DesignerInteractionTrigger.OnSubmit));
            screen.elements.Add(button);

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsFalse(result.Program.Interactions.IsEmpty,
                "a focusable node must be able to answer the submit action");
        }

        [Test]
        public void LongPressAndDoubleClickReachTheRuntime()
        {
            var screen = NewScreen();
            var button = Element("Slot", "Button");
            button.interactions.Add(Rule(DesignerInteractionTrigger.OnLongPress));
            button.interactions.Add(Rule(DesignerInteractionTrigger.OnDoubleClick));
            screen.elements.Add(button);

            var result = NexScreenCompiler.Compile(screen);

            Assert.AreEqual(2, result.Program.Interactions.Rules.Count,
                "both timing triggers must survive compilation");
        }

        // ---- propagation ------------------------------------------------------

        [Test]
        public void PointerTriggersPropagateToAnAncestor()
        {
            // The case that motivates phases: a card reacting to a press on anything inside it,
            // without the same rule copied onto every child.
            var screen = NewScreen();
            screen.elements.Add(Element("Card", "Panel"));
            screen.elements.Add(Element("Icon", "Image", "Card"));

            screen.elements[0].interactions.Add(
                Rule(DesignerInteractionTrigger.OnPointerDown, DesignerInteractionPhase.Bubble));

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsFalse(HasCode(result, NexDiagnosticCodes.InteractionPhaseUnreachable),
                "a pointer trigger raised on a child must be allowed to bubble");
            Assert.IsFalse(result.Program.Interactions.IsEmpty);
        }

        [Test]
        public void ShowDoesNotPropagate()
        {
            // Show already reaches every element, so a Bubble rule would fire on the same event the
            // Target rule did - a duplicate wearing propagation's clothes.
            var screen = NewScreen();
            screen.elements.Add(Element("Card", "Panel"));
            screen.elements.Add(Element("Icon", "Image", "Card"));

            screen.elements[0].interactions.Add(
                Rule(DesignerInteractionTrigger.OnShow, DesignerInteractionPhase.Bubble));

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsTrue(HasCode(result, NexDiagnosticCodes.InteractionPhaseUnreachable));
            Assert.IsTrue(result.Program.Interactions.IsEmpty);
        }

        [Test]
        public void BubblingWithNoChildrenIsStillRejected()
        {
            var screen = NewScreen();
            var leaf = Element("Alone", "Panel");
            leaf.interactions.Add(
                Rule(DesignerInteractionTrigger.OnPointerEnter, DesignerInteractionPhase.Bubble));
            screen.elements.Add(leaf);

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsTrue(HasCode(result, NexDiagnosticCodes.InteractionPhaseUnreachable),
                "nothing can bubble up from an element with nothing inside it");
        }

        // ---- drag and drop ----------------------------------------------------

        [Test]
        public void DragTriggersSurviveOnAnyNode()
        {
            // Dragging needs a raycast target, not a Selectable - an inventory icon is an Image.
            var screen = NewScreen();
            var icon = Element("Item", "Image");
            icon.interactions.Add(Rule(DesignerInteractionTrigger.OnDragBegin));
            icon.interactions.Add(Rule(DesignerInteractionTrigger.OnDrag));
            icon.interactions.Add(Rule(DesignerInteractionTrigger.OnDragEnd));
            screen.elements.Add(icon);

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsFalse(HasCode(result, NexDiagnosticCodes.TriggerNotRaisableByNode));
            Assert.AreEqual(3, result.Program.Interactions.Rules.Count);
        }

        [Test]
        public void DropIsAuthoredOnTheReceivingElement()
        {
            // The drop rule belongs to the slot, not to the thing dragged - which is what makes it
            // the one trigger whose subject differs from the element that started the gesture.
            var screen = NewScreen();
            var slot = Element("Slot", "Panel");
            slot.interactions.Add(Rule(DesignerInteractionTrigger.OnDrop));
            screen.elements.Add(slot);

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsFalse(HasCode(result, NexDiagnosticCodes.TriggerNotRaisableByNode));
            Assert.AreEqual(1, result.Program.Interactions.Rules.Count);
        }

        [Test]
        public void ADropRuleCanFilterOnWhatWasDragged()
        {
            // The reason the drag source is published to state: a slot that accepts one kind of
            // item has to be able to refuse the others, using the same condition field every other
            // rule uses rather than a drop-only concept.
            var screen = NewScreen();
            var slot = Element("WeaponSlot", "Panel");

            var rule = Rule(DesignerInteractionTrigger.OnDrop);
            rule.conditionKey = emiteat.NexUI.Interaction.NexInteractionRuntime.DragSourceKey;
            rule.comparison = DesignerInteractionComparison.Equals;
            rule.conditionValue = "inventory.sword";
            slot.interactions.Add(rule);
            screen.elements.Add(slot);

            var result = NexScreenCompiler.Compile(screen);

            Assert.AreEqual(1, result.Program.Interactions.Rules.Count, "the rule must compile");
            Assert.AreEqual(emiteat.NexUI.Interaction.NexInteractionRuntime.DragSourceKey,
                result.Program.Interactions.Rules[0].ConditionKey,
                "the condition must reach the runtime pointing at the drag-source key");
        }

        [Test]
        public void DragTriggersPropagate()
        {
            // A grid reacting to any of its cells being dragged, without the rule on every cell.
            var screen = NewScreen();
            screen.elements.Add(Element("Grid", "Panel"));
            screen.elements.Add(Element("Cell", "Image", "Grid"));

            screen.elements[0].interactions.Add(
                Rule(DesignerInteractionTrigger.OnDragBegin, DesignerInteractionPhase.Bubble));

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsFalse(HasCode(result, NexDiagnosticCodes.InteractionPhaseUnreachable));
            Assert.IsFalse(result.Program.Interactions.IsEmpty);
        }

        // ---- overlays ---------------------------------------------------------

        [Test]
        public void AModalCanInterceptItsOwnClose()
        {
            var screen = NewScreen();
            var modal = Element("Confirm", "Modal");
            modal.interactions.Add(Rule(DesignerInteractionTrigger.OnCloseRequested));
            screen.elements.Add(modal);

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsFalse(HasCode(result, NexDiagnosticCodes.TriggerNotRaisableByNode),
                "an overlay must be able to hear that something asked it to close");
            Assert.AreEqual(1, result.Program.Interactions.Rules.Count);
        }

        [Test]
        public void APlainPanelCannotHearACloseRequest()
        {
            // Nothing would ever raise it, so the rule would sit there looking authored.
            var screen = NewScreen();
            var panel = Element("Card", "Panel");
            panel.interactions.Add(Rule(DesignerInteractionTrigger.OnCloseRequested));
            screen.elements.Add(panel);

            var result = NexScreenCompiler.Compile(screen);

            Assert.IsTrue(HasCode(result, NexDiagnosticCodes.TriggerNotRaisableByNode));
            Assert.IsTrue(result.Program.Interactions.IsEmpty);
        }

        [Test]
        public void OverlaysCarryTheOverlayCapability()
        {
            // What the backend switches on to attach the component that owns open/close. Without
            // it a compiled modal is a panel that sits there: no backdrop dismissal, no timeout.
            var screen = NewScreen();
            foreach (var type in new[] { "Modal", "Popover", "Tooltip", "Toast" })
                screen.elements.Add(Element(type + "Node", type));

            var result = NexScreenCompiler.Compile(screen);

            foreach (var node in result.Program.Nodes)
            {
                Assert.IsTrue(node.IsOverlay, $"'{node.Name}' must report the overlay capability");
                Assert.IsFalse(string.IsNullOrEmpty(node.ControlId),
                    $"'{node.Name}' must say which overlay it is");
            }
        }

        [Test]
        public void AnOverlayKeepsWhateverElseItIs()
        {
            // A modal is still a panel that can carry a binding, so the capability is added rather
            // than replacing what the node already was.
            var screen = NewScreen();
            var modal = Element("Confirm", "Modal");
            screen.elements.Add(modal);

            var result = NexScreenCompiler.Compile(screen);
            var node = result.Program.Nodes[0];

            Assert.IsTrue(node.IsOverlay);
            Assert.AreEqual(NexNodeKind.Panel, node.Kind, "the node kind is unchanged by being an overlay");
        }

        // ---- identity ---------------------------------------------------------

        [Test]
        public void AuthoredTriggerValuesMatchCompiledOnes()
        {
            // The compiler casts the authored enum straight across, so the two must stay aligned.
            // If they ever drift, every authored rule silently repoints to a different trigger -
            // which is exactly the kind of failure that looks like a runtime bug.
            foreach (DesignerInteractionTrigger authored in
                System.Enum.GetValues(typeof(DesignerInteractionTrigger)))
            {
                var name = authored.ToString();
                Assert.IsTrue(System.Enum.IsDefined(typeof(NexTrigger), name),
                    $"compiled NexTrigger is missing '{name}'");
                Assert.AreEqual((int)authored, (int)System.Enum.Parse(typeof(NexTrigger), name),
                    $"'{name}' has a different value on the two sides");
            }
        }

        [Test]
        public void DragVisualNamesMatchTheBackendsOwn()
        {
            // These two are matched by name at build time, across a layer boundary the compiler
            // cannot check: the authoring model must not depend on the uGUI integration, so the
            // only thing keeping them aligned is this test. A renamed member would compile fine
            // and silently degrade every authored drag to "no feedback".
            foreach (DesignerDragVisual authored in System.Enum.GetValues(typeof(DesignerDragVisual)))
            {
                Assert.IsTrue(
                    System.Enum.IsDefined(typeof(Integrations.UGUI.NexDragVisual), authored.ToString()),
                    $"the uGUI backend has no drag visual named '{authored}'");
            }

            foreach (Integrations.UGUI.NexDragVisual backend in
                System.Enum.GetValues(typeof(Integrations.UGUI.NexDragVisual)))
            {
                Assert.IsTrue(
                    System.Enum.IsDefined(typeof(DesignerDragVisual), backend.ToString()),
                    $"the Designer cannot author the backend's '{backend}'");
            }
        }

        [Test]
        public void EveryCompiledTriggerCanBeAuthored()
        {
            // The other direction: a trigger the runtime can raise but nobody can author is dead
            // weight in the engine, and a trigger authored against it would not round-trip.
            foreach (NexTrigger compiled in System.Enum.GetValues(typeof(NexTrigger)))
            {
                Assert.IsTrue(
                    System.Enum.IsDefined(typeof(DesignerInteractionTrigger), compiled.ToString()),
                    $"authored DesignerInteractionTrigger is missing '{compiled}'");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Inspectors
{
    /// <summary>
    /// Authors an element's trigger / condition / action rules.
    /// </summary>
    /// <remarks>
    /// Two kinds of edit, handled differently on purpose. Typing in a field mutates the rule and
    /// leaves the UI alone - rebuilding on every keystroke would steal focus mid-word. Adding or
    /// removing a rule or action changes the shape of the list, so those rebuild it.
    ///
    /// Every edit goes through <c>Context.UpdateSelectedElement</c> rather than touching the
    /// metadata directly, which is what puts it on the undo stack and marks the asset dirty. A
    /// value the user cannot undo is worse than one they cannot set.
    /// </remarks>
    public sealed class InteractionInspector : DesignerInspectorBase
    {
        private readonly VisualElement _rulesRoot;
        private bool _refreshing;

        public InteractionInspector(NexUIDesignerContext context) : base(context, "inspector.interaction")
        {
            _rulesRoot = new VisualElement();
            Add(_rulesRoot);

            var addRule = new Button(AddRule) { text = "Add Rule" };
            Add(addRule);

            Subscriptions.Add<DesignerElementMetadata>(
                h => context.MetadataSelectionChanged += h,
                h => context.MetadataSelectionChanged -= h,
                _ => Refresh());
            Subscriptions.Add(h => context.CanvasChanged += h, h => context.CanvasChanged -= h, Refresh);

            Refresh();
        }

        // ---- structure ------------------------------------------------------

        private void Refresh()
        {
            _refreshing = true;
            _rulesRoot.Clear();

            var element = Context.SelectedMetadata;
            SetEnabled(element != null);

            if (element != null)
            {
                element.interactions ??= new List<DesignerInteractionRule>();
                for (int i = 0; i < element.interactions.Count; i++)
                    _rulesRoot.Add(BuildRule(element.interactions[i], i));
            }

            _refreshing = false;
        }

        private VisualElement BuildRule(DesignerInteractionRule rule, int index)
        {
            var foldout = new Foldout { text = RuleTitle(rule, index), value = true };

            var enabled = new Toggle("Enabled") { value = rule.enabled };
            enabled.RegisterValueChangedCallback(evt => Edit(() => rule.enabled = evt.newValue));
            foldout.Add(enabled);

            var name = new TextField("Name") { value = rule.displayName ?? string.Empty };
            name.RegisterValueChangedCallback(evt =>
            {
                Edit(() => rule.displayName = evt.newValue);
                foldout.text = RuleTitle(rule, index);
            });
            foldout.Add(name);

            var trigger = new EnumField("When", rule.trigger);
            trigger.RegisterValueChangedCallback(evt =>
            {
                Edit(() => rule.trigger = (DesignerInteractionTrigger)evt.newValue);
                foldout.text = RuleTitle(rule, index);
            });
            foldout.Add(trigger);

            var phase = new EnumField("Listen on", rule.phase)
            {
                tooltip = "Target: only when this element itself is clicked.\n" +
                          "Bubble: when anything inside it is clicked, after the target reacts.\n" +
                          "Capture: when anything inside it is clicked, before the target reacts."
            };
            phase.RegisterValueChangedCallback(evt =>
                Edit(() => rule.phase = (DesignerInteractionPhase)evt.newValue));
            foldout.Add(phase);

            var stop = new Toggle("Stop after this")
            {
                value = rule.stopPropagation,
                tooltip = "Nothing further sees the event - no later phase, no ancestor. " +
                          "Use it so a button inside a modal does not also trigger the modal's own rule."
            };
            stop.RegisterValueChangedCallback(evt => Edit(() => rule.stopPropagation = evt.newValue));
            foldout.Add(stop);

            foldout.Add(BuildCondition(rule));

            var actionsRoot = new VisualElement();
            for (int i = 0; i < rule.actions.Count; i++)
                actionsRoot.Add(BuildAction(rule, rule.actions[i]));
            foldout.Add(actionsRoot);

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            buttons.Add(new Button(() => AddAction(rule)) { text = "Add Action" });
            buttons.Add(new Button(() => RemoveRule(rule)) { text = "Remove Rule" });
            foldout.Add(buttons);

            return foldout;
        }

        /// <summary>
        /// The condition row. An empty key means "always", which is stated in the label rather
        /// than hidden behind a separate toggle the user would have to discover.
        /// </summary>
        private VisualElement BuildCondition(DesignerInteractionRule rule)
        {
            var root = new VisualElement();

            var key = new TextField("Only if (state key)") { value = rule.conditionKey ?? string.Empty };
            key.tooltip = "Leave empty to run every time the trigger fires.";
            key.RegisterValueChangedCallback(evt => Edit(() => rule.conditionKey = evt.newValue));
            root.Add(key);

            var comparison = new EnumField("Comparison", rule.comparison);
            comparison.RegisterValueChangedCallback(evt =>
                Edit(() => rule.comparison = (DesignerInteractionComparison)evt.newValue));
            root.Add(comparison);

            var value = new TextField("Compare to") { value = rule.conditionValue ?? string.Empty };
            value.tooltip = "Numbers compare numerically; anything else compares as text.";
            value.RegisterValueChangedCallback(evt => Edit(() => rule.conditionValue = evt.newValue));
            root.Add(value);

            return root;
        }

        /// <summary>
        /// One action row. Only the fields the chosen kind actually uses are shown - a command
        /// action has no business displaying a state key it will never read.
        /// </summary>
        private VisualElement BuildAction(DesignerInteractionRule rule, DesignerInteractionAction action)
        {
            var root = new VisualElement { style = { marginLeft = 12 } };

            var kind = new EnumField("Do", action.kind);
            kind.RegisterValueChangedCallback(evt =>
            {
                Edit(() => action.kind = (DesignerInteractionActionKind)evt.newValue);
                Refresh();
            });
            root.Add(kind);

            switch (action.kind)
            {
                case DesignerInteractionActionKind.ExecuteCommand:
                {
                    var command = new TextField("Command id") { value = action.commandId ?? string.Empty };
                    command.RegisterValueChangedCallback(evt => Edit(() => action.commandId = evt.newValue));
                    root.Add(command);
                    break;
                }

                case DesignerInteractionActionKind.SetState:
                {
                    var stateKey = new TextField("State key") { value = action.stateKey ?? string.Empty };
                    stateKey.RegisterValueChangedCallback(evt => Edit(() => action.stateKey = evt.newValue));
                    root.Add(stateKey);

                    var value = new TextField("Value") { value = action.value ?? string.Empty };
                    value.RegisterValueChangedCallback(evt => Edit(() => action.value = evt.newValue));
                    root.Add(value);
                    break;
                }

                case DesignerInteractionActionKind.SetVisible:
                {
                    root.Add(BuildTargetField(action));

                    var visible = new Toggle("Visible") { value = action.boolValue };
                    visible.RegisterValueChangedCallback(evt => Edit(() => action.boolValue = evt.newValue));
                    root.Add(visible);
                    break;
                }

                case DesignerInteractionActionKind.SetText:
                {
                    root.Add(BuildTargetField(action));

                    var text = new TextField("Text") { value = action.value ?? string.Empty };
                    text.RegisterValueChangedCallback(evt => Edit(() => action.value = evt.newValue));
                    root.Add(text);
                    break;
                }

                case DesignerInteractionActionKind.Delay:
                {
                    var seconds = new FloatField("Seconds")
                    {
                        value = action.seconds,
                        tooltip = "Pauses before the rest of this rule runs. Everything after this " +
                                  "action waits. A delay as the last action does nothing."
                    };
                    seconds.RegisterValueChangedCallback(evt =>
                        Edit(() => action.seconds = evt.newValue < 0f ? 0f : evt.newValue));
                    root.Add(seconds);
                    break;
                }
            }

            root.Add(new Button(() => RemoveAction(rule, action)) { text = "Remove Action" });
            return root;
        }

        /// <summary>
        /// Target picker built from the screen's element ids.
        /// </summary>
        /// <remarks>
        /// A dropdown rather than a text field because a mistyped id is a compile error
        /// (<c>NEX-BND-4003</c>) the author would only find later. An id that no longer exists is
        /// still shown, so opening a stale screen does not silently retarget the action.
        /// </remarks>
        private VisualElement BuildTargetField(DesignerInteractionAction action)
        {
            var ids = Context.Metadata != null
                ? Context.Metadata.elements
                    .Where(e => e != null && !string.IsNullOrEmpty(e.elementId))
                    .Select(e => e.elementId)
                    .Distinct()
                    .ToList()
                : new List<string>();

            var current = action.targetElementId ?? string.Empty;
            if (!string.IsNullOrEmpty(current) && !ids.Contains(current)) ids.Insert(0, current);

            var field = new DropdownField("Target", ids, Math.Max(0, ids.IndexOf(current)));
            field.RegisterValueChangedCallback(evt => Edit(() => action.targetElementId = evt.newValue));
            return field;
        }

        private static string RuleTitle(DesignerInteractionRule rule, int index)
            => !string.IsNullOrEmpty(rule.displayName)
                ? rule.trigger + " — " + rule.displayName
                : rule.trigger + " — Rule " + (index + 1);

        // ---- edits ----------------------------------------------------------

        /// <summary>
        /// Applies a mutation as one undo step. The body edits the rule objects directly; they
        /// already belong to the selected element, so recording that element captures the change.
        /// </summary>
        private void Edit(Action body)
        {
            if (_refreshing || body == null) return;
            Context.UpdateSelectedElement(_ => body(), "Edit NexUI Interaction");
        }

        private void AddRule()
        {
            var element = Context.SelectedMetadata;
            if (element == null) return;

            Context.UpdateSelectedElement(e =>
            {
                e.interactions ??= new List<DesignerInteractionRule>();
                e.interactions.Add(new DesignerInteractionRule());
            }, "Add NexUI Interaction Rule");

            Refresh();
        }

        private void RemoveRule(DesignerInteractionRule rule)
        {
            Context.UpdateSelectedElement(e => e.interactions?.Remove(rule), "Remove NexUI Interaction Rule");
            Refresh();
        }

        private void AddAction(DesignerInteractionRule rule)
        {
            Context.UpdateSelectedElement(_ => rule.actions.Add(new DesignerInteractionAction()),
                "Add NexUI Interaction Action");
            Refresh();
        }

        private void RemoveAction(DesignerInteractionRule rule, DesignerInteractionAction action)
        {
            Context.UpdateSelectedElement(_ => rule.actions.Remove(action), "Remove NexUI Interaction Action");
            Refresh();
        }
    }
}

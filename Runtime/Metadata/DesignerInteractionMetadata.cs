using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Designer
{
    /// <summary>What starts an interaction rule.</summary>
    /// <remarks>
    /// Deliberately three to begin with. The full trigger list in the feature specification is
    /// sixteen, but a trigger is only real once the compiler can validate it and a backend can
    /// actually raise it - and every one added here is one more thing every backend must fire.
    /// These three are the set the uGUI runtime can raise today without new infrastructure:
    /// a click from the Button, and show/hide from the screen's own lifecycle.
    /// </remarks>
    public enum DesignerInteractionTrigger
    {
        OnClick = 0,
        OnShow = 1,
        OnHide = 2
    }

    /// <summary>
    /// Where in the propagation path a rule listens.
    /// </summary>
    /// <remarks>
    /// <see cref="Target"/> is the default and means "only when this element is the one that was
    /// clicked" - the behaviour every rule had before phases existed, so adding this field changed
    /// nothing about screens already authored.
    ///
    /// The phase that earns its keep is <see cref="Bubble"/>: it is how a list reacts to any of its
    /// items being clicked, or a modal closes when anything inside it is pressed, without the
    /// author wiring the same rule onto every child.
    /// </remarks>
    public enum DesignerInteractionPhase
    {
        /// <summary>Fires only when this element is the origin of the event.</summary>
        Target = 0,

        /// <summary>Fires when a descendant is the origin, after the target has been handled.</summary>
        Bubble = 1,

        /// <summary>Fires when a descendant is the origin, before the target sees it.</summary>
        Capture = 2
    }

    /// <summary>How a condition compares the live value against the authored one.</summary>
    public enum DesignerInteractionComparison
    {
        Equals = 0,
        NotEquals = 1,
        GreaterThan = 2,
        LessThan = 3
    }

    /// <summary>What a rule does once its trigger fires and its condition passes.</summary>
    public enum DesignerInteractionActionKind
    {
        /// <summary>Dispatch a command id to game code through the command router.</summary>
        ExecuteCommand = 0,

        /// <summary>Write a value into the state store, which any binding on that key then sees.</summary>
        SetState = 1,

        /// <summary>Show or hide another element on the same screen.</summary>
        SetVisible = 2,

        /// <summary>Replace another element's text.</summary>
        SetText = 3,

        /// <summary>
        /// Pause before the rule's remaining actions run.
        /// </summary>
        /// <remarks>
        /// A rule is already a sequence - its actions run in order - so a delay is all that was
        /// missing to express "flash the button, wait, then open the next screen". There is no
        /// separate Sequence action for that reason.
        /// </remarks>
        Delay = 4
    }

    /// <summary>One step a rule performs.</summary>
    /// <remarks>
    /// A flat record with a kind discriminator rather than a class hierarchy: Unity serializes
    /// <c>[SerializeReference]</c> polymorphism inconsistently across versions, and the authoring
    /// document has to survive a <see cref="UnityEngine.JsonUtility"/> round trip (that is how
    /// <c>DesignerMetadataUtility.Clone</c> works). Unused fields for a given kind stay empty and
    /// cost nothing meaningful.
    /// </remarks>
    [Serializable]
    public sealed class DesignerInteractionAction
    {
        public DesignerInteractionActionKind kind = DesignerInteractionActionKind.ExecuteCommand;

        /// <summary>For <see cref="DesignerInteractionActionKind.ExecuteCommand"/>.</summary>
        public string commandId;

        /// <summary>For <see cref="DesignerInteractionActionKind.SetState"/>.</summary>
        public string stateKey;

        /// <summary>Text / state value. Parsed as a number when it looks like one.</summary>
        public string value;

        /// <summary>Element id this action affects, for SetVisible / SetText.</summary>
        public string targetElementId;

        /// <summary>For <see cref="DesignerInteractionActionKind.SetVisible"/>.</summary>
        public bool boolValue = true;

        /// <summary>Seconds for <see cref="DesignerInteractionActionKind.Delay"/>.</summary>
        public float seconds = 0.25f;
    }

    /// <summary>
    /// One authored interaction: when this happens, and this holds, do these things.
    /// </summary>
    /// <remarks>
    /// The trigger / condition / action shape is the smallest thing that is still honest about
    /// what UI interaction is. Collapsing it to "button calls a command" would have been less
    /// code, but every screen then needs game code for decisions the designer could have made -
    /// which is the exact cost this product exists to remove.
    /// </remarks>
    [Serializable]
    public sealed class DesignerInteractionRule
    {
        /// <summary>Stable identity so diagnostics and traces can name a specific rule after edits.</summary>
        public string ruleId = Guid.NewGuid().ToString("N");

        /// <summary>Author-facing name. Optional; falls back to the trigger name in reports.</summary>
        public string displayName;

        /// <summary>Off rules are kept in the document but not compiled.</summary>
        public bool enabled = true;

        public DesignerInteractionTrigger trigger = DesignerInteractionTrigger.OnClick;

        /// <summary>Where in the propagation path this rule listens. Defaults to the element itself.</summary>
        public DesignerInteractionPhase phase = DesignerInteractionPhase.Target;

        /// <summary>
        /// Stops the event once this rule's actions have run, so no further phase or ancestor
        /// sees it.
        /// </summary>
        /// <remarks>
        /// The escape hatch for the case bubbling creates: a modal that closes on background
        /// clicks must not also close when a button inside it is pressed. The button's rule marks
        /// the event handled and the modal never hears about it.
        /// </remarks>
        public bool stopPropagation;

        /// <summary>State key to test. Empty means the rule is unconditional.</summary>
        public string conditionKey;

        public DesignerInteractionComparison comparison = DesignerInteractionComparison.Equals;

        /// <summary>Value the condition compares against. Parsed as a number when it looks like one.</summary>
        public string conditionValue;

        public List<DesignerInteractionAction> actions = new List<DesignerInteractionAction>();

        public bool HasCondition => !string.IsNullOrEmpty(conditionKey);
    }
}

using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Designer
{
    /// <summary>What starts an interaction rule.</summary>
    /// <remarks>
    /// A trigger is only real once the compiler can validate it and a backend can actually raise
    /// it, so this list grows only alongside the runtime that fires it. Values are appended and
    /// never renumbered - the number is what an authored screen serializes.
    ///
    /// <see cref="OnClick"/> comes from the Button; <see cref="OnShow"/> / <see cref="OnHide"/>
    /// from the screen's own lifecycle. The rest are raised by the uGUI event system through the
    /// interaction relay, which is attached only to nodes whose triggers were actually authored.
    ///
    /// <see cref="OnSubmit"/> and <see cref="OnCancel"/> are not duplicates of a click: they are
    /// what reaches an element focused by keyboard or gamepad, and so are what make a screen
    /// operable without a pointer at all.
    /// </remarks>
    public enum DesignerInteractionTrigger
    {
        OnClick = 0,
        OnShow = 1,
        OnHide = 2,

        OnPointerEnter = 3,
        OnPointerExit = 4,
        OnPointerDown = 5,
        OnPointerUp = 6,

        OnSubmit = 7,
        OnCancel = 8,

        OnLongPress = 9,
        OnDoubleClick = 10,

        OnDragBegin = 11,
        OnDrag = 12,
        OnDragEnd = 13,

        /// <summary>
        /// Raised on the element something was dropped <em>onto</em>, not the one dragged.
        /// </summary>
        /// <remarks>
        /// While a drop rule runs, the element that was dragged is readable from state under
        /// <c>nexui.drag.source</c>, so a rule can accept or refuse based on what it caught.
        /// </remarks>
        OnDrop = 14,

        /// <summary>
        /// Raised on an overlay when something asked it to close - a backdrop click, a dismiss
        /// button, a toast running out of time.
        /// </summary>
        /// <remarks>
        /// A rule here can confirm before closing, save a draft, or refuse. Authoring nothing is
        /// also an answer: the overlay then just closes, because one that ignored every close
        /// request would lock the screen.
        /// </remarks>
        OnCloseRequested = 15
    }

    /// <summary>How a dragged element shows that it is being dragged.</summary>
    /// <remarks>
    /// Declared here rather than reused from the uGUI integration so the authoring model stays
    /// backend-neutral - the Designer must not depend on a backend to describe intent. The two
    /// enums are matched by <em>name</em> when the screen is built, so they can be reordered
    /// independently without repointing authored screens.
    /// </remarks>
    public enum DesignerDragVisual
    {
        /// <summary>Nothing moves. Whatever feedback there is comes from the rules.</summary>
        None = 0,

        /// <summary>The element follows the pointer, and returns if the drop is refused.</summary>
        MoveSelf = 1,

        /// <summary>A translucent copy follows the pointer while the element stays put.</summary>
        Ghost = 2
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

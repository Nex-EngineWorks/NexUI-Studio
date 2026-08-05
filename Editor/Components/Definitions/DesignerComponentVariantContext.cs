using emiteat.NexUI.Abstractions;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Components.Definitions
{
    /// <summary>
    /// The authoring environment a variant rule's resolution / input-mode condition is judged against.
    /// </summary>
    /// <remarks>
    /// This is an <b>authoring-time</b> condition, not a runtime one. A component can say "use the
    /// compact arrangement below 900px", the canvas shows that arrangement at the current canvas
    /// resolution, and the save writes what is currently resolved. Adapting live at runtime is what
    /// the screen's own responsive rules are for - a definition does not own those, and letting two
    /// instances of one component contribute conflicting screen rules is a problem worth not having.
    ///
    /// <see cref="Unknown"/> is what a caller with no canvas passes. A rule that depends on the
    /// environment then does not apply, and the expansion says so rather than guessing a resolution
    /// and silently producing a different tree than the canvas showed.
    /// </remarks>
    public readonly struct DesignerComponentVariantContext
    {
        public readonly bool HasResolution;
        public readonly Vector2Int Resolution;
        public readonly bool HasInputMode;
        public readonly UIInputMode InputMode;

        public DesignerComponentVariantContext(Vector2Int resolution)
        {
            HasResolution = true;
            Resolution = resolution;
            HasInputMode = false;
            InputMode = default;
        }

        public DesignerComponentVariantContext(Vector2Int resolution, UIInputMode inputMode)
        {
            HasResolution = true;
            Resolution = resolution;
            HasInputMode = true;
            InputMode = inputMode;
        }

        /// <summary>No canvas: environment-conditioned rules cannot be evaluated.</summary>
        public static DesignerComponentVariantContext Unknown => default;

        /// <summary>
        /// Whether <paramref name="rule"/>'s environment condition holds. <paramref name="reason"/>
        /// explains a false result, and is null when the rule simply does not match.
        /// </summary>
        public bool Matches(DesignerComponentVariantRule rule, out string reason)
        {
            reason = null;
            if (rule == null || !rule.HasEnvironmentCondition) return true;

            if (rule.constrainResolution)
            {
                if (!HasResolution)
                {
                    reason = "the current canvas resolution is unknown here";
                    return false;
                }
                if (Resolution.x < rule.minResolution.x || Resolution.y < rule.minResolution.y ||
                    Resolution.x > rule.maxResolution.x || Resolution.y > rule.maxResolution.y)
                    return false;
            }

            if (rule.constrainInputMode)
            {
                if (!HasInputMode)
                {
                    reason = "the current input mode is unknown here";
                    return false;
                }
                if (InputMode != rule.inputMode) return false;
            }

            return true;
        }
    }
}

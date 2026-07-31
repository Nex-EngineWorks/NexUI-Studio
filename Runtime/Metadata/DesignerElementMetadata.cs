using System;
using System.Collections.Generic;
using emiteat.NexUI.Accessibility;
using UnityEngine;

namespace emiteat.NexUI.Designer
{
    /// <summary>
    /// uGUI anchor preset for a Designer element. Runtime-safe (no UnityEditor dependency)
    /// so it can live on <see cref="DesignerElementMetadata"/> and be shared by the editor
    /// backend / serializer. TopLeft is value 0 so metadata assets authored before this field
    /// existed deserialize to the historical top-left default.
    /// </summary>
    public enum DesignerAnchorPreset
    {
        TopLeft,
        Top,
        TopRight,
        Left,
        Center,
        Right,
        BottomLeft,
        Bottom,
        BottomRight,
        Stretch
    }

    /// <summary>
    /// Coarse silhouette of an element, used for canvas previews and as the corner-radius
    /// fallback while <see cref="DesignerVisualStyleMetadata.hasOverrides"/> is off. Rounded is
    /// value 0 because it is the historical default for every authored element.
    /// </summary>
    public enum DesignerElementShape
    {
        Rounded,
        Rectangle,
        Pill,
        Circle
    }

    /// <summary>
    /// A MonoBehaviour the user attached to an element through the Designer's Add Component flow.
    /// Only the type identity is stored: the component itself lives on the generated GameObject,
    /// and an unresolvable <see cref="typeName"/> is preserved rather than dropped so a screen
    /// authored against a script that is temporarily missing survives a round trip.
    /// </summary>
    [Serializable]
    public sealed class DesignerAttachedComponentMetadata
    {
        public string typeName;
    }

    /// <summary>
    /// The authoring record for one element on a Designer screen: identity, placement, the
    /// component stack attached to it, and the sparse style / behaviour overrides layered on top.
    /// </summary>
    /// <remarks>
    /// Runtime-safe on purpose (no UnityEditor types), so the same record is shared by the
    /// canvas, the uGUI / UI Toolkit backends and the JSON serializer. Cloning goes through
    /// <c>DesignerMetadataUtility.Clone</c>, which round-trips this type via
    /// <see cref="JsonUtility"/> - every field here must therefore stay Unity-serializable.
    ///
    /// <see cref="elementId"/> is the user-facing, renameable id used by bindings and focus
    /// navigation; <see cref="stableId"/> is the identity that survives a rename and is what
    /// motion clips, variants and responsive overrides key off.
    /// </remarks>
    [Serializable]
    public sealed class DesignerElementMetadata
    {
        /// <summary>Rename-proof identity. Generated once and never reused by a clone.</summary>
        public string stableId = Guid.NewGuid().ToString("N");

        /// <summary>User-facing id referenced by bindings, focus links and generated code.</summary>
        public string elementId;

        /// <summary>Parent element's <see cref="elementId"/>, or null/empty for a root element.</summary>
        public string parentId;

        /// <summary>Draw / hierarchy order inside the parent. Normalized by DesignerHierarchyUtility.</summary>
        public int siblingIndex;

        /// <summary>Slot of the parent component instance this element is placed into, when any.</summary>
        public string parentSlotId;

        public string displayName;
        public string elementType = "Panel";

        /// <summary>Absolute canvas-space rect (top-left origin, y growing downward).</summary>
        public Rect rect = new Rect(64, 64, 240, 96);

        public DesignerAnchorPreset anchorPreset = DesignerAnchorPreset.TopLeft;
        public DesignerElementShape shape = DesignerElementShape.Rounded;

        /// <summary>Design-time value for fill-driven previews (ProgressBar, StatBar, RadialFill...).</summary>
        public float previewValue = 60f;

        /// <summary>Design-time item count for list / grid previews.</summary>
        public int previewItemCount;

        /// <summary>Design-time entries for choice-list previews.</summary>
        public List<string> previewOptions = new List<string>();

        public DesignerFillMetadata fill = new DesignerFillMetadata();

        /// <summary>Design-time sprite for Image / IconButton previews.</summary>
        public Sprite previewImage;

        public string text;
        public Color tint = new Color(0.15f, 0.22f, 0.34f, 1f);
        public Color textColor = Color.white;
        public int fontSize = 14;
        public List<string> classes = new List<string>();

        public DesignerBindingMetadata binding = new DesignerBindingMetadata();
        public DesignerMotionMetadata motion = new DesignerMotionMetadata();
        public DesignerThemeMetadata theme = new DesignerThemeMetadata();
        public DesignerAutoLayoutMetadata autoLayout = new DesignerAutoLayoutMetadata();
        public DesignerConstraintMetadata constraint = new DesignerConstraintMetadata();
        public DesignerFocusMetadata focus = new DesignerFocusMetadata();

        /// <summary>Sparse layout overrides; inert while <c>hasOverrides</c> is false.</summary>
        public DesignerLayoutStyleMetadata layoutStyle = new DesignerLayoutStyleMetadata();

        /// <summary>Sparse visual overrides; inert while <c>hasOverrides</c> is false.</summary>
        public DesignerVisualStyleMetadata visualStyle = new DesignerVisualStyleMetadata();

        /// <summary>Sparse typography overrides; inert while <c>hasOverrides</c> is false.</summary>
        public DesignerTypographyMetadata typography = new DesignerTypographyMetadata();

        /// <summary>Set when this element is an instance of a component definition.</summary>
        public DesignerComponentInstanceMetadata componentInstance = new DesignerComponentInstanceMetadata();

        /// <summary>MonoBehaviours the user attached on top of what the element itself stamps.</summary>
        public List<DesignerAttachedComponentMetadata> attachedComponents =
            new List<DesignerAttachedComponentMetadata>();

        /// <summary>Exposed component-definition property values for this instance.</summary>
        public List<DesignerComponentPropertyEntry> componentProperties =
            new List<DesignerComponentPropertyEntry>();

        /// <summary>The element's own component stack - what the element actually *is*.</summary>
        public List<DesignerElementComponent> components = new List<DesignerElementComponent>();

        /// <summary>Per-part transform deltas from the component library's default hierarchy.</summary>
        public List<DesignerComponentPartOverrideMetadata> componentPartOverrides =
            new List<DesignerComponentPartOverrideMetadata>();

        public bool locked;

        /// <summary>Hidden on the Designer canvas only; has no effect at runtime.</summary>
        public bool hiddenInDesigner;

        /// <summary>Initial active state of the generated GameObject / VisualElement.</summary>
        public bool runtimeVisible = true;

        public bool clipChildren;

        /// <summary>Padding applied to children, independent of auto-layout padding.</summary>
        public RectOffset contentPadding = new RectOffset();

        public string accessibilityLabel;
        public AccessibilityRole accessibilityRole = AccessibilityRole.None;
    }
}

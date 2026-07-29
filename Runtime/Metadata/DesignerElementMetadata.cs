using System;
using System.Collections.Generic;
using emiteat.NexUI.Accessibility;
using UnityEngine;

namespace emiteat.NexUI.Designer
{
    [Serializable]
    public sealed class DesignerAttachedComponentMetadata
    {
        /// <summary>
        /// Assembly-qualified MonoBehaviour type name. The Designer resolves this lazily so screen
        /// metadata can still load when an optional gameplay assembly or package is unavailable.
        /// </summary>
        public string typeName;
    }

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
    /// Designer-canvas corner shape for an element's preview box. Rounded is value 0 so
    /// elements authored before this field existed deserialize to the historical fixed-8px-
    /// radius look. Purely a Designer preview concern - it does not change how the backend
    /// (UXML/prefab) actually renders the element; USS/uGUI sprite radius is still up to the
    /// user's own authored asset.
    /// </summary>
    public enum DesignerElementShape
    {
        Rounded,
        Rectangle,
        Pill,
        Circle
    }

    [Serializable]
    public sealed class DesignerElementMetadata
    {
        /// <summary>
        /// Immutable backend identity. Unlike <see cref="elementId"/>, this value is not renamed
        /// by the user and lets serializers reconnect metadata to the same backend object.
        /// </summary>
        public string stableId = Guid.NewGuid().ToString("N");
        public string elementId;
        public string parentId;
        /// <summary>
        /// Order of this element among its siblings (elements sharing the same
        /// <see cref="parentId"/>). Lower draws/lists first. Defaults to 0 so metadata authored
        /// before this field existed deserializes safely; <see cref="DesignerHierarchyUtility"/>
        /// normalizes ambiguous/duplicate values (falling back to the stable
        /// <see cref="DesignerMetadataAsset.elements"/> order) on load.
        /// </summary>
        public int siblingIndex;
        /// <summary>
        /// Which named slot of the parent this element occupies (e.g. Modal "header"/"content"/
        /// "footer", Button "icon"/"content"). Empty/null is treated as the default
        /// <c>"content"</c> slot, so metadata authored before slots existed loads unchanged.
        /// </summary>
        public string parentSlotId;
        public string displayName;
        public string elementType = "Panel";
        public Rect rect = new Rect(64, 64, 240, 96);
        public DesignerAnchorPreset anchorPreset = DesignerAnchorPreset.TopLeft;
        public DesignerElementShape shape = DesignerElementShape.Rounded;
        /// <summary>
        /// 0-100 preview fill amount for value-driven component types (ProgressBar, StatBar,
        /// RadialFill) so the Designer canvas shows an actual filled bar/ring instead of a bare
        /// box - purely a Designer preview value, distinct from binding.valueKey (the runtime
        /// UIStateStore key that drives the real value in-game).
        /// </summary>
        public float previewValue = 60f;
        /// <summary>
        /// Number of generated preview items a collection component (List/Grid/Hotbar) shows on the
        /// canvas. 0 ⇒ the renderer's default count. Generated items are virtual - never stored as
        /// authored elements. Also doubles as the Hotbar slot count.
        /// </summary>
        public int previewItemCount;
        /// <summary>
        /// Inline option labels for a ChoiceList preview (empty ⇒ placeholder rows). Preview-only;
        /// the real options come from the runtime collection binding at play time.
        /// </summary>
        public List<string> previewOptions = new List<string>();
        public DesignerFillMetadata fill = new DesignerFillMetadata();
        /// <summary>
        /// Sprite used by Image/IconButton elements. The Designer renders it in the canvas and
        /// the uGUI serializer writes it to the generated <c>UnityEngine.UI.Image</c>. Using a
        /// Sprite (instead of an arbitrary Texture2D) prevents editor/theme textures from being
        /// assigned accidentally and preserves the source sprite rect for atlased images.
        /// </summary>
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
        public DesignerLayoutStyleMetadata layoutStyle = new DesignerLayoutStyleMetadata();
        public DesignerVisualStyleMetadata visualStyle = new DesignerVisualStyleMetadata();
        public DesignerTypographyMetadata typography = new DesignerTypographyMetadata();
        /// <summary>
        /// Reusable-component reference. Meaningful only when its <c>definitionGuid</c> is set, in
        /// which case this element is a component instance: authored children below it are slot
        /// content and the definition sub-tree is expanded underneath at preview/serialize time.
        /// Always non-null so schema-v3 metadata (authored before components existed) loads unchanged.
        /// </summary>
        public DesignerComponentInstanceMetadata componentInstance = new DesignerComponentInstanceMetadata();

        /// <summary>
        /// MonoBehaviours requested through Add Component. They are materialized on the matching
        /// generated uGUI GameObject; UI Toolkit keeps the entries and reports them as uGUI-only.
        /// </summary>
        public List<DesignerAttachedComponentMetadata> attachedComponents = new List<DesignerAttachedComponentMetadata>();

        public bool locked;
        /// <summary>Editor-only canvas visibility. Never changes generated/runtime visibility.</summary>
        public bool hiddenInDesigner;
        /// <summary>Visibility written to the backend and used at runtime.</summary>
        public bool runtimeVisible = true;

        /// <summary>
        /// When true, the Designer canvas clips child previews to this element's content bounds
        /// (a container concern - Panel/Card/Modal/List/Grid). Default false preserves the
        /// historical un-clipped preview for elements authored before this field existed.
        /// </summary>
        public bool clipChildren;

        /// <summary>
        /// Inner padding (left, top, right, bottom) reserved inside a container for laying out /
        /// clipping children. Zero by default. Consumed by Auto Layout and clip/overflow preview;
        /// leaf elements ignore it.
        /// </summary>
        // Keep the default null. Constructing RectOffset from a serialized object's field
        // initializer can execute native Unity code while the serializer is running and emits
        // "CreateObject is not allowed during serialization" on domain reload. All consumers
        // treat null as zero padding, preserving existing metadata behaviour.
        public RectOffset contentPadding;

        /// <summary>Screen-reader-facing label. Falls back to <see cref="text"/> or <see cref="displayName"/> at runtime when empty.</summary>
        public string accessibilityLabel;
        public AccessibilityRole accessibilityRole = AccessibilityRole.None;
    }
}

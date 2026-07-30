using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Designer.Editor.Components
{
    /// <summary>Which library a component type comes from. Decides where it can be attached.</summary>
    public enum DesignerUIComponentFamily
    {
        /// <summary>Works on any backend (RectTransform-level concerns the Designer owns itself).</summary>
        Core,
        UGUI,
        UIToolkit,
        /// <summary>NexUI's own components - the things Unity does not ship, available on both backends.</summary>
        NexUIBase
    }

    /// <summary>Add Component menu grouping.</summary>
    public enum DesignerUIComponentCategory
    {
        Core,
        Graphic,
        Text,
        Control,
        Layout,
        Interaction,
        Data,
        Game,
        Effect,
        Other
    }

    /// <summary>
    /// One attachable component type - the Designer's mirror of a Unity component class.
    /// </summary>
    /// <remarks>
    /// For uGUI and NexUI Base components <see cref="BackingType"/> is the real MonoBehaviour, and the
    /// property schema is reflected from its serialized fields, so the Designer shows what Unity shows
    /// without a hand-written list per component (and keeps up when Unity adds a field).
    /// </remarks>
    public sealed class DesignerUIComponentType
    {
        public string TypeId;
        public string DisplayName;
        public string LocalizationKey;
        public string Description;
        public DesignerUIComponentFamily Family = DesignerUIComponentFamily.Core;
        public DesignerUIComponentCategory Category = DesignerUIComponentCategory.Other;

        /// <summary>The real runtime type this maps to (MonoBehaviour for uGUI/NexUI, VisualElement for UI Toolkit).</summary>
        public Type BackingType;

        /// <summary>UXML tag emitted when this component defines the element on a UI Toolkit screen.</summary>
        public string UxmlTag;

        /// <summary>Component types that must be present for this one to work (Unity's RequireComponent).</summary>
        public string[] RequiredComponents = Array.Empty<string>();

        /// <summary>Component types that cannot coexist with this one (two Graphics, two layout groups...).</summary>
        public string[] ConflictsWith = Array.Empty<string>();

        /// <summary>False mirrors Unity's <c>DisallowMultipleComponent</c>.</summary>
        public bool AllowMultiple;

        /// <summary>True when this component draws the element itself (Image, Text, a UI Toolkit control).</summary>
        public bool IsRenderer;

        /// <summary>True when removing it would leave the element unusable; the Inspector protects these.</summary>
        public bool IsEssential;

        public List<DesignerComponentProperty> Properties = new List<DesignerComponentProperty>();

        public bool SupportsBackend(DesignerUIComponentFamily backend)
            => Family == DesignerUIComponentFamily.Core
               || Family == DesignerUIComponentFamily.NexUIBase
               || Family == backend;
    }
}

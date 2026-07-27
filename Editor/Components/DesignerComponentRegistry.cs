using System.Collections.Generic;
using emiteat.NexUI.Accessibility;
using emiteat.NexUI.Designer.Editor.Backend;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Components
{
    /// <summary>
    /// Central registry of <see cref="DesignerComponentDescriptor"/>s - the single source of truth
    /// for every component type's identity, defaults, capabilities, slots, states, bindings and
    /// backend support. Palette, Inspector, Preview, Validation, Hierarchy and the serializers read
    /// descriptors from here instead of maintaining parallel type switch statements.
    ///
    /// Every value of the runtime <see cref="DesignerElementType"/> enum is registered. Unknown /
    /// user-defined type strings resolve to a safe <b>Generic</b> descriptor that keeps the type's
    /// own id and name, so opening a screen that uses a type this Designer build doesn't know never
    /// breaks or deletes data.
    /// </summary>
    public static class DesignerComponentRegistry
    {
        private static readonly Dictionary<string, DesignerComponentDescriptor> _byId =
            new Dictionary<string, DesignerComponentDescriptor>();
        private static bool _built;

        // Common state/binding presets.
        private const DesignerComponentState Interactive =
            DesignerComponentState.Normal | DesignerComponentState.Hover | DesignerComponentState.Pressed |
            DesignerComponentState.Focused | DesignerComponentState.Disabled;
        private const DesignerComponentState ValueStates =
            DesignerComponentState.Normal | DesignerComponentState.Disabled |
            DesignerComponentState.Indeterminate | DesignerComponentState.Error;
        private const DesignerComponentState CollectionStates =
            DesignerComponentState.Normal | DesignerComponentState.Loading |
            DesignerComponentState.Empty | DesignerComponentState.Error;

        private const DesignerBindingChannel B_Text = DesignerBindingChannel.Text;
        private const DesignerBindingChannel B_Value = DesignerBindingChannel.Value;
        private const DesignerBindingChannel B_Vis = DesignerBindingChannel.Visibility;
        private const DesignerBindingChannel B_Class = DesignerBindingChannel.Class;
        private const DesignerBindingChannel B_Cmd = DesignerBindingChannel.Command;
        private const DesignerBindingChannel B_Inter = DesignerBindingChannel.Interactable;

        public static IEnumerable<DesignerComponentDescriptor> All
        {
            get { EnsureBuilt(); return _byId.Values; }
        }

        /// <summary>Descriptor for a type id (enum name or custom string). Never null - unknown ids get a Generic descriptor carrying that id.</summary>
        public static DesignerComponentDescriptor Get(string typeId)
        {
            EnsureBuilt();
            if (string.IsNullOrEmpty(typeId)) typeId = "Panel";
            return _byId.TryGetValue(typeId, out var d) ? d : Generic(typeId);
        }

        public static DesignerComponentDescriptor Get(DesignerElementType type) => Get(type.ToString());

        public static bool IsRegistered(string typeId)
        {
            EnsureBuilt();
            return !string.IsNullOrEmpty(typeId) && _byId.ContainsKey(typeId);
        }

        public static bool IsContainer(string typeId) => Get(typeId).IsContainer;
        public static bool CanHaveChildren(string typeId) => Get(typeId).CanHaveChildren;

        private static DesignerComponentDescriptor Generic(string typeId) => new DesignerComponentDescriptor
        {
            TypeId = typeId,
            DisplayName = string.IsNullOrEmpty(typeId) ? "Component" : typeId,
            LocalizationKey = "component.generic",
            Category = DesignerComponentCategory.Generic,
            Description = "Unknown/custom component type - shown generically so existing screens are preserved.",
            CanHaveChildren = true,           // permissive: never blocks authoring on an unknown type
            IsContainer = false,
            SupportedStates = DesignerComponentState.Normal,
            SupportedBindings = B_Vis | B_Class | B_Text | B_Value | B_Cmd | B_Inter,
            Slots = { Slot(DesignerComponentSlot.Content, "Content") },
            UGUISupport = DesignerBackendSupport.Partial,
            UIToolkitSupport = DesignerBackendSupport.Partial
        };

        private static void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            foreach (var d in Build())
                _byId[d.TypeId] = d;
        }

        private static DesignerComponentSlot Slot(string id, string name, int min = 0, int max = int.MaxValue,
            bool template = false, bool generated = false, string[] accepted = null) => new DesignerComponentSlot
        {
            SlotId = id, DisplayName = name, LocalizationKey = "slot." + id,
            MinimumChildren = min, MaximumChildren = max,
            IsTemplateSlot = template, IsGeneratedContentSlot = generated, AcceptedComponentTypes = accepted
        };

        private static IEnumerable<DesignerComponentDescriptor> Build()
        {
            // ---- Containers ---------------------------------------------------------------
            yield return new DesignerComponentDescriptor
            {
                TypeId = "Panel", DisplayName = "Panel", LocalizationKey = "component.panel",
                Category = DesignerComponentCategory.Container, Icon = "▭",
                Description = "General-purpose visual container.",
                DefaultSize = new Vector2(280, 120), DefaultColor = new Color(0.13f, 0.18f, 0.26f, 1f),
                CanHaveChildren = true, IsContainer = true,
                DefaultAccessibilityRole = AccessibilityRole.Container,
                SupportedStates = DesignerComponentState.Normal,
                SupportedBindings = B_Vis | B_Class,
                Slots = { Slot(DesignerComponentSlot.Content, "Content") }
            };
            yield return new DesignerComponentDescriptor
            {
                TypeId = "Container", DisplayName = "Container", LocalizationKey = "component.container",
                Category = DesignerComponentCategory.Container, Icon = "⧉",
                Description = "Layout-only parent with no default visuals.",
                DefaultSize = new Vector2(280, 120), DefaultColor = new Color(0f, 0f, 0f, 0f),
                CanHaveChildren = true, IsContainer = true,
                DefaultAccessibilityRole = AccessibilityRole.Container,
                SupportedStates = DesignerComponentState.Normal, SupportedBindings = B_Vis | B_Class,
                Slots = { Slot(DesignerComponentSlot.Content, "Content") },
                UGUISupport = DesignerBackendSupport.Partial // no Graphic emitted on purpose
            };
            yield return new DesignerComponentDescriptor
            {
                TypeId = "Card", DisplayName = "Card", LocalizationKey = "component.card",
                Category = DesignerComponentCategory.Container, Icon = "🂠",
                Description = "Grouped content surface with header/content/footer slots; optionally interactive.",
                DefaultSize = new Vector2(320, 200), DefaultColor = new Color(0.15f, 0.2f, 0.29f, 1f),
                CanHaveChildren = true, IsContainer = true, IsInteractive = true,
                DefaultAccessibilityRole = AccessibilityRole.Container,
                SupportedStates = Interactive | DesignerComponentState.Selected,
                SupportedBindings = B_Vis | B_Class | B_Cmd | B_Inter,
                SupportedEvents = { "Click", "Selected" },
                Slots = { Slot("header", "Header", 0, 1), Slot(DesignerComponentSlot.Content, "Content"), Slot("footer", "Footer", 0, 1) }
            };
            yield return new DesignerComponentDescriptor
            {
                TypeId = "Modal", DisplayName = "Modal", LocalizationKey = "component.modal",
                Category = DesignerComponentCategory.Overlay, Icon = "▢",
                Description = "Screen overlay with backdrop and header/content/footer.",
                DefaultSize = new Vector2(640, 360), DefaultColor = new Color(0.08f, 0.1f, 0.14f, 0.96f),
                CanHaveChildren = true, IsContainer = true, IsOverlayComponent = true,
                DefaultAccessibilityRole = AccessibilityRole.Dialog,
                SupportedStates = DesignerComponentState.Normal,
                SupportedBindings = B_Vis | B_Cmd,
                SupportedEvents = { "Opened", "Closing", "Closed", "Dismissed" },
                Slots = { Slot("header", "Header", 0, 1), Slot(DesignerComponentSlot.Content, "Content"), Slot("footer", "Footer", 0, 1) },
                UGUISupport = DesignerBackendSupport.Partial, UIToolkitSupport = DesignerBackendSupport.Partial
            };
            yield return new DesignerComponentDescriptor
            {
                TypeId = "Popover", DisplayName = "Popover", LocalizationKey = "component.popover",
                Category = DesignerComponentCategory.Overlay, Icon = "◱",
                Description = "Anchored overlay that allows interactive content.",
                DefaultSize = new Vector2(280, 200), DefaultShape = DesignerElementShape.Rounded,
                CanHaveChildren = true, IsContainer = true, IsOverlayComponent = true,
                DefaultAccessibilityRole = AccessibilityRole.Dialog,
                SupportedStates = DesignerComponentState.Normal, SupportedBindings = B_Vis | B_Cmd,
                Slots = { Slot("header", "Header", 0, 1), Slot(DesignerComponentSlot.Content, "Content"), Slot("footer", "Footer", 0, 1) },
                UGUISupport = DesignerBackendSupport.Partial, UIToolkitSupport = DesignerBackendSupport.Partial
            };

            // ---- Text & Media -------------------------------------------------------------
            yield return new DesignerComponentDescriptor
            {
                TypeId = "Label", DisplayName = "Text / Label", LocalizationKey = "component.label",
                Category = DesignerComponentCategory.Text, Icon = "T",
                Description = "Rich/plain text with typography, wrapping and localization.",
                DefaultSize = new Vector2(260, 44), DefaultText = "Label",
                DefaultColor = new Color(0.12f, 0.15f, 0.2f, 0.65f),
                CanHaveChildren = false, DefaultAccessibilityRole = AccessibilityRole.Label,
                SupportedStates = DesignerComponentState.Normal, SupportedBindings = B_Text | B_Vis | B_Class
            };
            yield return new DesignerComponentDescriptor
            {
                TypeId = "Image", DisplayName = "Image", LocalizationKey = "component.image",
                Category = DesignerComponentCategory.Media, Icon = "🖼",
                Description = "Sprite/texture with scale mode, nine-slice and fill.",
                DefaultSize = new Vector2(160, 120), DefaultColor = new Color(0.19f, 0.25f, 0.34f, 1f),
                CanHaveChildren = false, DefaultAccessibilityRole = AccessibilityRole.Image,
                SupportedStates = DesignerComponentState.Normal, SupportedBindings = B_Value | B_Vis | B_Class,
                UIToolkitSupport = DesignerBackendSupport.Partial
            };

            // ---- Input & Action -----------------------------------------------------------
            yield return new DesignerComponentDescriptor
            {
                TypeId = "Button", DisplayName = "Button", LocalizationKey = "component.button",
                Category = DesignerComponentCategory.Input, Icon = "⬚",
                Description = "Command-driven button with icon/content slots and interaction states.",
                DefaultSize = new Vector2(220, 56), DefaultText = "Button",
                DefaultColor = new Color(0.12f, 0.36f, 0.85f, 1f),
                CanHaveChildren = true, IsInteractive = true,
                DefaultAccessibilityRole = AccessibilityRole.Button,
                SupportedStates = Interactive | DesignerComponentState.Selected,
                SupportedBindings = B_Text | B_Vis | B_Class | B_Cmd | B_Inter,
                SupportedEvents = { "Click", "DoubleClick", "Hold", "Focus", "Blur" },
                Slots = { Slot("icon", "Icon", 0, 1), Slot(DesignerComponentSlot.Content, "Content", 0, 1) }
            };
            yield return new DesignerComponentDescriptor
            {
                TypeId = "IconButton", DisplayName = "Icon Button", LocalizationKey = "component.iconButton",
                Category = DesignerComponentCategory.Input, Icon = "◉", DefaultShape = DesignerElementShape.Pill,
                Description = "Icon-only button (accessible label required).",
                DefaultSize = new Vector2(56, 56), DefaultColor = new Color(0.12f, 0.36f, 0.85f, 1f),
                CanHaveChildren = true, IsInteractive = true,
                DefaultAccessibilityRole = AccessibilityRole.Button,
                SupportedStates = Interactive | DesignerComponentState.Selected,
                SupportedBindings = B_Vis | B_Class | B_Cmd | B_Inter,
                SupportedEvents = { "Click", "Focus", "Blur" },
                Slots = { Slot("icon", "Icon", 1, 1), Slot("badge", "Badge", 0, 1) }
            };
            yield return new DesignerComponentDescriptor
            {
                TypeId = "ChoiceList", DisplayName = "Choice List", LocalizationKey = "component.choiceList",
                Category = DesignerComponentCategory.Input, Icon = "☰",
                Description = "Single/multi-select option list bound to a collection.",
                DefaultSize = new Vector2(320, 240), DefaultColor = new Color(0.13f, 0.18f, 0.26f, 1f),
                CanHaveChildren = true, IsContainer = true, IsInteractive = true, IsCollectionComponent = true,
                DefaultAccessibilityRole = AccessibilityRole.List,
                SupportedStates = Interactive | DesignerComponentState.Empty,
                SupportedBindings = B_Value | B_Vis | B_Cmd,
                SupportedEvents = { "SelectionChanged", "OptionActivated" },
                Slots = { Slot("option", "Option Template", 0, 1, template: true), Slot("empty", "Empty State", 0, 1) }
            };

            // ---- Feedback -----------------------------------------------------------------
            yield return new DesignerComponentDescriptor
            {
                TypeId = "ProgressBar", DisplayName = "Progress Bar", LocalizationKey = "component.progressBar",
                Category = DesignerComponentCategory.Feedback, Icon = "▬",
                Description = "Linear value indicator (Track/Fill/Label are virtual preview parts).",
                DefaultSize = new Vector2(280, 24), DefaultColor = new Color(0.13f, 0.18f, 0.26f, 1f),
                CanHaveChildren = false, IsValueComponent = true,
                DefaultAccessibilityRole = AccessibilityRole.ProgressIndicator,
                SupportedStates = ValueStates, SupportedBindings = B_Value | B_Vis,
                SupportedEvents = { "ValueChanged" },
                UGUISupport = DesignerBackendSupport.Partial, UIToolkitSupport = DesignerBackendSupport.Partial
            };
            yield return new DesignerComponentDescriptor
            {
                TypeId = "StatBar", DisplayName = "Stat Bar", LocalizationKey = "component.statBar",
                Category = DesignerComponentCategory.Feedback, Icon = "▮",
                Description = "Game stat value bar (HP/Stamina...) built on the value component base.",
                DefaultSize = new Vector2(280, 28), DefaultColor = new Color(0.13f, 0.18f, 0.26f, 1f),
                CanHaveChildren = true, IsValueComponent = true,
                DefaultAccessibilityRole = AccessibilityRole.ProgressIndicator,
                SupportedStates = DesignerComponentState.Normal | DesignerComponentState.Disabled |
                                  DesignerComponentState.Empty | DesignerComponentState.Warning |
                                  DesignerComponentState.Error | DesignerComponentState.Success,
                SupportedBindings = B_Value | B_Vis,
                Slots = { Slot("icon", "Icon", 0, 1) },
                UGUISupport = DesignerBackendSupport.Partial, UIToolkitSupport = DesignerBackendSupport.Partial
            };
            yield return new DesignerComponentDescriptor
            {
                TypeId = "RadialFill", DisplayName = "Radial Fill", LocalizationKey = "component.radialFill",
                Category = DesignerComponentCategory.Feedback, Icon = "◐", DefaultShape = DesignerElementShape.Circle,
                Description = "Radial value ring (background ring / fill arc are virtual parts).",
                DefaultSize = new Vector2(120, 120), DefaultColor = new Color(0.13f, 0.18f, 0.26f, 1f),
                CanHaveChildren = true, IsValueComponent = true,
                DefaultAccessibilityRole = AccessibilityRole.ProgressIndicator,
                SupportedStates = DesignerComponentState.Normal | DesignerComponentState.Indeterminate | DesignerComponentState.Error,
                SupportedBindings = B_Value | B_Vis,
                Slots = { Slot("center", "Center Content", 0, 1) },
                UGUISupport = DesignerBackendSupport.Partial, UIToolkitSupport = DesignerBackendSupport.PreviewOnly
            };
            yield return new DesignerComponentDescriptor
            {
                TypeId = "Spinner", DisplayName = "Spinner", LocalizationKey = "component.spinner",
                Category = DesignerComponentCategory.Feedback, Icon = "◌", DefaultShape = DesignerElementShape.Circle,
                Description = "Indeterminate loading indicator.",
                DefaultSize = new Vector2(48, 48), DefaultColor = new Color(0.13f, 0.18f, 0.26f, 1f),
                CanHaveChildren = false, IsValueComponent = true,
                DefaultAccessibilityRole = AccessibilityRole.ProgressIndicator,
                SupportedStates = DesignerComponentState.Normal | DesignerComponentState.Indeterminate,
                SupportedBindings = B_Vis,
                UGUISupport = DesignerBackendSupport.Partial, UIToolkitSupport = DesignerBackendSupport.PreviewOnly
            };
            yield return new DesignerComponentDescriptor
            {
                TypeId = "Skeleton", DisplayName = "Skeleton", LocalizationKey = "component.skeleton",
                Category = DesignerComponentCategory.Feedback, Icon = "░",
                Description = "Loading placeholder with configurable rows/shapes and shimmer.",
                DefaultSize = new Vector2(280, 120), DefaultColor = new Color(0.2f, 0.24f, 0.3f, 1f),
                CanHaveChildren = false,
                SupportedStates = DesignerComponentState.Normal | DesignerComponentState.Loading,
                SupportedBindings = B_Vis,
                UIToolkitSupport = DesignerBackendSupport.Partial
            };
            yield return new DesignerComponentDescriptor
            {
                TypeId = "Toast", DisplayName = "Toast", LocalizationKey = "component.toast",
                Category = DesignerComponentCategory.Feedback, Icon = "🔔", DefaultShape = DesignerElementShape.Pill,
                Description = "Transient message with severity, placement and auto-dismiss.",
                DefaultSize = new Vector2(320, 64), DefaultText = "Toast message",
                DefaultColor = new Color(0.13f, 0.18f, 0.26f, 1f),
                CanHaveChildren = true, IsOverlayComponent = true,
                DefaultAccessibilityRole = AccessibilityRole.Label,
                SupportedStates = DesignerComponentState.Normal | DesignerComponentState.Success |
                                  DesignerComponentState.Warning | DesignerComponentState.Error,
                SupportedBindings = B_Text | B_Vis,
                Slots = { Slot("action", "Action", 0, 1) },
                UIToolkitSupport = DesignerBackendSupport.Partial
            };
            yield return new DesignerComponentDescriptor
            {
                TypeId = "Tooltip", DisplayName = "Tooltip", LocalizationKey = "component.tooltip",
                Category = DesignerComponentCategory.Overlay, Icon = "▛", DefaultShape = DesignerElementShape.Pill,
                Description = "Anchored, non-interactive hint text.",
                DefaultSize = new Vector2(200, 40), DefaultColor = new Color(0.1f, 0.12f, 0.16f, 0.98f),
                CanHaveChildren = true, IsOverlayComponent = true,
                DefaultAccessibilityRole = AccessibilityRole.Label,
                SupportedStates = DesignerComponentState.Normal, SupportedBindings = B_Text | B_Vis,
                Slots = { Slot(DesignerComponentSlot.Content, "Content", 0, 1) },
                UGUISupport = DesignerBackendSupport.Partial, UIToolkitSupport = DesignerBackendSupport.Partial
            };

            // ---- Data & Collections -------------------------------------------------------
            yield return new DesignerComponentDescriptor
            {
                TypeId = "List", DisplayName = "List", LocalizationKey = "component.list",
                Category = DesignerComponentCategory.Data, Icon = "≡",
                Description = "Collection-bound list with item template and empty/loading/error states.",
                DefaultSize = new Vector2(360, 420), DefaultColor = new Color(0.11f, 0.15f, 0.22f, 1f),
                CanHaveChildren = true, IsContainer = true, IsCollectionComponent = true,
                DefaultAccessibilityRole = AccessibilityRole.List,
                SupportedStates = CollectionStates, SupportedBindings = B_Value | B_Vis,
                SupportedEvents = { "ItemSelected", "ItemActivated", "Reordered", "ScrolledToEnd" },
                Slots =
                {
                    Slot("item", "Item Template", 0, 1, template: true),
                    Slot("header", "Header", 0, 1), Slot("footer", "Footer", 0, 1),
                    Slot("empty", "Empty State", 0, 1), Slot("loading", "Loading State", 0, 1), Slot("error", "Error State", 0, 1)
                },
                UGUISupport = DesignerBackendSupport.Partial
            };
            yield return new DesignerComponentDescriptor
            {
                TypeId = "Grid", DisplayName = "Grid", LocalizationKey = "component.grid",
                Category = DesignerComponentCategory.Data, Icon = "▦",
                Description = "Collection-bound grid sharing List's template/state system.",
                DefaultSize = new Vector2(420, 420), DefaultColor = new Color(0.11f, 0.15f, 0.22f, 1f),
                CanHaveChildren = true, IsContainer = true, IsCollectionComponent = true,
                DefaultAccessibilityRole = AccessibilityRole.List,
                SupportedStates = CollectionStates, SupportedBindings = B_Value | B_Vis,
                SupportedEvents = { "ItemSelected", "ItemActivated" },
                Slots =
                {
                    Slot("item", "Item Template", 0, 1, template: true),
                    Slot("empty", "Empty State", 0, 1), Slot("loading", "Loading State", 0, 1), Slot("error", "Error State", 0, 1)
                },
                UGUISupport = DesignerBackendSupport.Partial, UIToolkitSupport = DesignerBackendSupport.Partial
            };
            yield return new DesignerComponentDescriptor
            {
                TypeId = "Slot", DisplayName = "Slot", LocalizationKey = "component.slot",
                Category = DesignerComponentCategory.Data, Icon = "▣",
                Description = "Single inventory/equipment/hotbar cell with item/count/overlay bindings.",
                DefaultSize = new Vector2(88, 88), DefaultColor = new Color(0.13f, 0.18f, 0.26f, 1f),
                CanHaveChildren = true, IsInteractive = true,
                DefaultAccessibilityRole = AccessibilityRole.Button,
                SupportedStates = DesignerComponentState.Empty | Interactive | DesignerComponentState.Selected | DesignerComponentState.Error,
                SupportedBindings = B_Value | B_Vis | B_Class | B_Cmd | B_Inter,
                SupportedEvents = { "Selected", "Activated", "DragStarted", "Dropped", "ContextRequested" },
                Slots =
                {
                    Slot("icon", "Icon", 0, 1), Slot("label", "Label", 0, 1),
                    Slot("count", "Count", 0, 1), Slot("overlay", "Overlay", 0, 1), Slot("badge", "Badge", 0, 1)
                }
            };
            yield return new DesignerComponentDescriptor
            {
                TypeId = "Hotbar", DisplayName = "Hotbar", LocalizationKey = "component.hotbar",
                Category = DesignerComponentCategory.Data, Icon = "⬓",
                Description = "Row/column of generated slots with an active index (slots are generated preview items).",
                DefaultSize = new Vector2(480, 88), DefaultColor = new Color(0.11f, 0.15f, 0.22f, 1f),
                CanHaveChildren = true, IsContainer = true, IsCollectionComponent = true, IsInteractive = true,
                DefaultAccessibilityRole = AccessibilityRole.List,
                SupportedStates = Interactive | DesignerComponentState.Empty,
                SupportedBindings = B_Value | B_Vis | B_Cmd,
                SupportedEvents = { "ActiveIndexChanged", "ActiveSlotActivated" },
                Slots = { Slot("slot", "Slot Template", 0, 1, template: true, accepted: new[] { "Slot" }) },
                UGUISupport = DesignerBackendSupport.Partial, UIToolkitSupport = DesignerBackendSupport.Partial
            };

            // ---- Reusable components ------------------------------------------------------
            // Placeholder type for an instance whose definition cannot be resolved. A healthy
            // instance takes its definition root's type instead, so this only ever shows when
            // something is broken - which is exactly when the user needs to see it on the canvas.
            yield return new DesignerComponentDescriptor
            {
                TypeId = "ComponentInstance", DisplayName = "Component Instance",
                LocalizationKey = "component.instance",
                Category = DesignerComponentCategory.Generic, Icon = "◈",
                Description = "Instance of a reusable component definition whose asset is not currently resolvable.",
                DefaultSize = new Vector2(240, 96), DefaultColor = new Color(0.20f, 0.15f, 0.28f, 1f),
                CanHaveChildren = true,
                SupportedStates = DesignerComponentState.Normal | DesignerComponentState.Error,
                SupportedBindings = B_Vis | B_Class | B_Text | B_Value | B_Cmd | B_Inter,
                Slots = { Slot(DesignerComponentSlot.Content, "Content") },
                UGUISupport = DesignerBackendSupport.Partial,
                UIToolkitSupport = DesignerBackendSupport.Partial
            };

            // ---- Fallback -----------------------------------------------------------------
            yield return Generic("Custom");
        }
    }
}

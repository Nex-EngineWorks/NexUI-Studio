using System.Collections.Generic;
using emiteat.NexUI.Accessibility;
using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Components.Definitions;
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

        /// <summary>
        /// UXML tag for the runtime collection element. Fully qualified because a custom element is
        /// resolved by type name, not by the <c>ui:</c> engine namespace.
        /// </summary>
        internal const string UIToolkitCollectionTag = "emiteat.NexUI.Integrations.UIToolkit.NXCollectionViewElement";

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

        /// <summary>Descriptors belonging to one component library (NexUI / uGUI / UI Toolkit).</summary>
        public static IEnumerable<DesignerComponentDescriptor> InFamily(DesignerComponentFamily family)
        {
            EnsureBuilt();
            foreach (var d in _byId.Values)
                if (d.Family == family) yield return d;
        }

        private static void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            // NexUI's own library first, then Unity's stock control catalogs. Later entries never
            // overwrite earlier ones silently - the catalogs use their own dotted id namespaces
            // ("UGUI.Button", "UITK.Button"), so a stock control can never shadow a NexUI type.
            foreach (var d in Build())
                _byId[d.TypeId] = d;
            foreach (var d in NexUIComponentCatalog.Build())
                _byId[d.TypeId] = d;
            foreach (var d in NexUILibraryCatalog.Build())
                _byId[d.TypeId] = d;
            foreach (var d in NexUIGameCatalog.Build())
                _byId[d.TypeId] = d;
            foreach (var d in UGUIComponentCatalog.Build())
                _byId[d.TypeId] = d;
            foreach (var d in UIToolkitComponentCatalog.Build())
                _byId[d.TypeId] = d;

            // Backend mappings and property schemas last: both are attached by component shape, so
            // every catalog must be registered before they run. Mappings go first because a schema
            // can depend on the control a component ends up writing.
            foreach (var d in _byId.Values)
            {
                NexUIBackendMappings.Apply(d);
                NexUIComponentPropertySchemas.Apply(d);
                NexUIComponentPartSchemas.Apply(d);
            }
        }

        /// <summary>
        /// Shared slot helper for the catalog files, so every catalog produces slots with the same
        /// localization-key convention as the core registry.
        /// </summary>
        internal static DesignerComponentSlot MakeSlot(string id, string name, int min = 0, int max = int.MaxValue,
            bool template = false, bool generated = false, string[] accepted = null)
            => Slot(id, name, min, max, template, generated, accepted);

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
                PaletteGroup = DesignerPaletteGroup.Containers, PaletteOrder = 0,
                UxmlTag = "ui:VisualElement",
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
                PaletteGroup = DesignerPaletteGroup.Containers, PaletteOrder = 2,
                UxmlTag = "ui:VisualElement",
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
                PaletteGroup = DesignerPaletteGroup.Containers, PaletteOrder = 1,
                UxmlTag = "ui:VisualElement",
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
                PaletteGroup = DesignerPaletteGroup.Overlay, PaletteOrder = 0,
                UxmlTag = "ui:VisualElement",
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
                PaletteGroup = DesignerPaletteGroup.Overlay, PaletteOrder = 1,
                UxmlTag = "ui:VisualElement",
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
                PaletteGroup = DesignerPaletteGroup.TextMedia, PaletteOrder = 0,
                UxmlTag = "ui:Label", UxmlHasText = true,
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
                PaletteGroup = DesignerPaletteGroup.TextMedia, PaletteOrder = 1,
                UxmlTag = "ui:VisualElement",
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
                PaletteGroup = DesignerPaletteGroup.Controls, PaletteOrder = 0,
                UxmlTag = "ui:Button", UxmlHasText = true,
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
                PaletteGroup = DesignerPaletteGroup.Controls, PaletteOrder = 1,
                UxmlTag = "ui:Button", UxmlHasText = true,
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
                PaletteGroup = DesignerPaletteGroup.Selection, PaletteOrder = 9,
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
                PaletteGroup = DesignerPaletteGroup.Feedback, PaletteOrder = 0,
                UxmlTag = "ui:ProgressBar",
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
                PaletteGroup = DesignerPaletteGroup.Feedback, PaletteOrder = 1,
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
                PaletteGroup = DesignerPaletteGroup.Feedback, PaletteOrder = 2,
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
                PaletteGroup = DesignerPaletteGroup.Feedback, PaletteOrder = 3,
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
                PaletteGroup = DesignerPaletteGroup.Feedback, PaletteOrder = 4,
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
                PaletteGroup = DesignerPaletteGroup.Overlay, PaletteOrder = 3,
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
                PaletteGroup = DesignerPaletteGroup.Overlay, PaletteOrder = 2,
                Description = "Anchored, non-interactive hint text.",
                DefaultSize = new Vector2(200, 40), DefaultColor = new Color(0.1f, 0.12f, 0.16f, 0.98f),
                CanHaveChildren = true, IsOverlayComponent = true,
                DefaultAccessibilityRole = AccessibilityRole.Label,
                SupportedStates = DesignerComponentState.Normal, SupportedBindings = B_Text | B_Vis,
                Slots = { Slot(DesignerComponentSlot.Content, "Content", 0, 1) },
                UGUISupport = DesignerBackendSupport.Partial, UIToolkitSupport = DesignerBackendSupport.Partial
            };

            // ---- Data & Collections -------------------------------------------------------
            // The one collection system. List, Grid, InventoryGrid, Carousel, SelectionList and the
            // several dozen game-specific lists are presets of this: same runtime
            // (NXCollectionView / NXCollectionViewElement), different options and item template.
            yield return new DesignerComponentDescriptor
            {
                TypeId = "CollectionView", DisplayName = "Collection View", LocalizationKey = "component.collectionView",
                Category = DesignerComponentCategory.Data, Icon = "▤",
                Kind = DesignerComponentKind.Core,
                PaletteGroup = DesignerPaletteGroup.Data, PaletteOrder = -1,
                UGUIControl = "CollectionView", UxmlTag = UIToolkitCollectionTag,
                Description = "Virtualized, selectable collection with item template and content/loading/empty/error states.",
                ElementIdPrefix = "collection",
                DefaultSize = new Vector2(360, 420), DefaultColor = new Color(0.11f, 0.15f, 0.22f, 1f),
                CanHaveChildren = true, IsContainer = true, IsCollectionComponent = true,
                DefaultAccessibilityRole = AccessibilityRole.List,
                SupportedStates = CollectionStates, SupportedBindings = B_Value | B_Vis | B_Class,
                SupportedEvents = { "ItemSelected", "ItemActivated", "Reordered", "ContextRequested", "ScrolledToEnd" },
                Slots =
                {
                    Slot("item", "Item Template", 0, 1, template: true),
                    Slot("header", "Header", 0, 1), Slot("footer", "Footer", 0, 1),
                    Slot("empty", "Empty State", 0, 1), Slot("loading", "Loading State", 0, 1), Slot("error", "Error State", 0, 1)
                },
                UGUISupport = DesignerBackendSupport.Full, UIToolkitSupport = DesignerBackendSupport.Full
            };
            yield return new DesignerComponentDescriptor
            {
                TypeId = "List", DisplayName = "List", LocalizationKey = "component.list",
                Kind = DesignerComponentKind.Preset, BaseTypeId = "CollectionView",
                Category = DesignerComponentCategory.Data, Icon = "≡",
                PaletteGroup = DesignerPaletteGroup.Data, PaletteOrder = 0,
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
                Kind = DesignerComponentKind.Preset, BaseTypeId = "CollectionView",
                Category = DesignerComponentCategory.Data, Icon = "▦",
                PaletteGroup = DesignerPaletteGroup.Data, PaletteOrder = 1,
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
                PaletteGroup = DesignerPaletteGroup.Game, PaletteOrder = 0,
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
                PaletteGroup = DesignerPaletteGroup.Game, PaletteOrder = 1,
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

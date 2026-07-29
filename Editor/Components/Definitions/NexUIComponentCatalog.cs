using System.Collections.Generic;
using emiteat.NexUI.Accessibility;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Components
{
    /// <summary>
    /// The rest of NexUI's own component library: backend-agnostic types that sit next to the core
    /// set in <see cref="DesignerComponentRegistry"/>. They are authored once and written to whichever
    /// backend the screen targets, so unlike <see cref="UGUIComponentCatalog"/> /
    /// <see cref="UIToolkitComponentCatalog"/> they declare Partial support on both backends: the
    /// Designer writes their rect/text/style faithfully and the Save Report names what a stock
    /// backend cannot express (a Rating's stars, a Minimap's render target, ...) instead of pretending.
    /// </summary>
    public static class NexUIComponentCatalog
    {
        private const DesignerComponentState Interactive =
            DesignerComponentState.Normal | DesignerComponentState.Hover | DesignerComponentState.Pressed |
            DesignerComponentState.Focused | DesignerComponentState.Disabled;
        private const DesignerComponentState FieldStates =
            DesignerComponentState.Normal | DesignerComponentState.Hover | DesignerComponentState.Focused |
            DesignerComponentState.Disabled | DesignerComponentState.Error | DesignerComponentState.Success;
        private const DesignerComponentState CollectionStates =
            DesignerComponentState.Normal | DesignerComponentState.Loading |
            DesignerComponentState.Empty | DesignerComponentState.Error;

        private const DesignerBindingChannel B_Text = DesignerBindingChannel.Text;
        private const DesignerBindingChannel B_Value = DesignerBindingChannel.Value;
        private const DesignerBindingChannel B_Vis = DesignerBindingChannel.Visibility;
        private const DesignerBindingChannel B_Class = DesignerBindingChannel.Class;
        private const DesignerBindingChannel B_Cmd = DesignerBindingChannel.Command;
        private const DesignerBindingChannel B_Inter = DesignerBindingChannel.Interactable;

        private static readonly Color Surface = new Color(0.13f, 0.18f, 0.26f, 1f);
        private static readonly Color Field = new Color(0.10f, 0.14f, 0.20f, 1f);
        private static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);
        private static readonly Color Accent = new Color(0.12f, 0.36f, 0.85f, 1f);

        public static IEnumerable<DesignerComponentDescriptor> Build()
        {
            // ---- Containers ----------------------------------------------------------------
            yield return New("Section", "Section", DesignerComponentCategory.Container,
                DesignerPaletteGroup.Containers, 3, new Vector2(360, 220),
                "Titled content section with an optional header action.",
                text: "Section", container: true, role: AccessibilityRole.Container,
                bindings: B_Text | B_Vis | B_Class, color: Surface,
                uxml: "ui:GroupBox", uxmlText: true,
                slots: new[] { Slot("header", "Header", 0, 1), Slot(DesignerComponentSlot.Content, "Content"), Slot("action", "Header Action", 0, 1) });

            yield return New("Toolbar", "Toolbar", DesignerComponentCategory.Container,
                DesignerPaletteGroup.Containers, 4, new Vector2(480, 48),
                "Horizontal strip of actions, usually pinned to the top of a panel.",
                container: true, role: AccessibilityRole.Container,
                bindings: B_Vis | B_Class, color: Surface,
                slots: new[] { Slot("leading", "Leading"), Slot(DesignerComponentSlot.Content, "Content"), Slot("trailing", "Trailing") });

            yield return New("ScrollArea", "Scroll Area", DesignerComponentCategory.Container,
                DesignerPaletteGroup.Containers, 5, new Vector2(320, 260),
                "Clipped, scrollable region for content taller than its box.",
                container: true, role: AccessibilityRole.Container,
                bindings: B_Vis | B_Class, color: Surface, uxml: "ui:ScrollView",
                events: new[] { "Scrolled", "ScrolledToEnd" });

            yield return New("Splitter", "Splitter", DesignerComponentCategory.Container,
                DesignerPaletteGroup.Containers, 6, new Vector2(420, 240),
                "Two resizable panes with a draggable divider.",
                container: true, role: AccessibilityRole.Container,
                bindings: B_Vis, color: Transparent, uxml: "ui:TwoPaneSplitView",
                slots: new[] { Slot("first", "First Pane", 0, 1), Slot("second", "Second Pane", 0, 1) });

            yield return New("Accordion", "Accordion", DesignerComponentCategory.Container,
                DesignerPaletteGroup.Containers, 7, new Vector2(360, 200),
                "Stack of collapsible sections; expanding one can collapse the others.",
                text: "Section title", container: true, role: AccessibilityRole.Container,
                states: DesignerComponentState.Normal | DesignerComponentState.Selected | DesignerComponentState.Disabled,
                bindings: B_Text | B_Value | B_Vis, color: Surface, uxml: "ui:Foldout", uxmlText: true,
                events: new[] { "Expanded", "Collapsed" },
                slots: new[] { Slot("header", "Header", 0, 1), Slot(DesignerComponentSlot.Content, "Content") });

            // ---- Text & media ----------------------------------------------------------------
            yield return New("Heading", "Heading", DesignerComponentCategory.Text,
                DesignerPaletteGroup.TextMedia, 2, new Vector2(320, 40),
                "Title text with heading-level typography.",
                text: "Heading", role: AccessibilityRole.Header,
                bindings: B_Text | B_Vis | B_Class, color: Transparent, uxml: "ui:Label", uxmlText: true);

            yield return New("RichText", "Rich Text", DesignerComponentCategory.Text,
                DesignerPaletteGroup.TextMedia, 3, new Vector2(320, 120),
                "Multi-line text with inline markup (bold, color, sprites, links).",
                text: "Rich <b>text</b>", role: AccessibilityRole.Label,
                bindings: B_Text | B_Vis | B_Class, color: Transparent, uxml: "ui:Label", uxmlText: true,
                events: new[] { "LinkClicked" });

            yield return New("Link", "Link", DesignerComponentCategory.Text,
                DesignerPaletteGroup.TextMedia, 4, new Vector2(160, 28),
                "Text that behaves like a command (navigation, external URL).",
                text: "Link", interactive: true, role: AccessibilityRole.Button,
                states: Interactive, bindings: B_Text | B_Vis | B_Cmd | B_Inter,
                color: Transparent, uxml: "ui:Button", uxmlText: true, events: new[] { "Click" });

            yield return New("Icon", "Icon", DesignerComponentCategory.Media,
                DesignerPaletteGroup.TextMedia, 5, new Vector2(32, 32),
                "Single glyph/sprite sized by the surrounding text.",
                role: AccessibilityRole.Image, bindings: B_Value | B_Vis | B_Class, color: Transparent);

            yield return New("Avatar", "Avatar", DesignerComponentCategory.Media,
                DesignerPaletteGroup.TextMedia, 6, new Vector2(64, 64),
                "Round portrait with optional status badge and initials fallback.",
                shape: DesignerElementShape.Circle, role: AccessibilityRole.Image,
                bindings: B_Value | B_Text | B_Vis, color: Surface,
                slots: new[] { Slot("badge", "Badge", 0, 1) }, canHaveChildren: true);

            yield return New("Divider", "Divider", DesignerComponentCategory.Media,
                DesignerPaletteGroup.TextMedia, 7, new Vector2(320, 2),
                "Thin separator line between content groups.",
                minSize: new Vector2(2, 1), bindings: B_Vis | B_Class,
                color: new Color(1f, 1f, 1f, 0.16f), shape: DesignerElementShape.Rectangle);

            yield return New("Badge", "Badge", DesignerComponentCategory.Feedback,
                DesignerPaletteGroup.TextMedia, 8, new Vector2(40, 20),
                "Small count/status pill, usually anchored to another element.",
                text: "9", shape: DesignerElementShape.Pill, role: AccessibilityRole.Label,
                states: DesignerComponentState.Normal | DesignerComponentState.Success |
                        DesignerComponentState.Warning | DesignerComponentState.Error,
                bindings: B_Text | B_Value | B_Vis, color: new Color(0.86f, 0.28f, 0.28f, 1f),
                minSize: new Vector2(16, 16), uxml: "ui:Label", uxmlText: true);

            yield return New("Chip", "Chip / Tag", DesignerComponentCategory.Input,
                DesignerPaletteGroup.TextMedia, 9, new Vector2(96, 28),
                "Compact removable tag; optionally selectable as a filter.",
                text: "Tag", shape: DesignerElementShape.Pill, interactive: true, canHaveChildren: true,
                role: AccessibilityRole.Button, states: Interactive | DesignerComponentState.Selected,
                bindings: B_Text | B_Vis | B_Cmd | B_Inter, color: Surface,
                uxml: "ui:Label", uxmlText: true, events: new[] { "Click", "Removed" },
                slots: new[] { Slot("icon", "Icon", 0, 1), Slot("remove", "Remove Button", 0, 1) });

            // ---- Selection --------------------------------------------------------------------
            yield return New("Checkbox", "Checkbox", DesignerComponentCategory.Input,
                DesignerPaletteGroup.Selection, 0, new Vector2(200, 28),
                "Boolean choice with a label; supports an indeterminate state.",
                text: "Checkbox", interactive: true, role: AccessibilityRole.Toggle,
                states: Interactive | DesignerComponentState.Selected | DesignerComponentState.Indeterminate,
                bindings: B_Text | B_Value | B_Vis | B_Class | B_Inter, color: Field,
                uxml: "ui:Toggle", uxmlText: true, uxmlTextAttribute: "label",
                events: new[] { "ValueChanged" });

            yield return New("Switch", "Switch", DesignerComponentCategory.Input,
                DesignerPaletteGroup.Selection, 1, new Vector2(56, 28),
                "On/off toggle drawn as a sliding track.",
                shape: DesignerElementShape.Pill, interactive: true, role: AccessibilityRole.Toggle,
                states: Interactive | DesignerComponentState.Selected,
                bindings: B_Value | B_Vis | B_Class | B_Inter, color: Field,
                uxml: "ui:Toggle", events: new[] { "ValueChanged" });

            yield return New("RadioGroup", "Radio Group", DesignerComponentCategory.Input,
                DesignerPaletteGroup.Selection, 2, new Vector2(240, 120),
                "Mutually exclusive option set.",
                container: true, interactive: true, role: AccessibilityRole.Container,
                states: Interactive, bindings: B_Value | B_Vis | B_Inter, color: Transparent,
                uxml: "ui:RadioButtonGroup", events: new[] { "SelectionChanged" },
                slots: new[] { Slot("option", "Option Template", 0, 1, template: true), Slot(DesignerComponentSlot.Content, "Options") });

            yield return New("SegmentedControl", "Segmented Control", DesignerComponentCategory.Input,
                DesignerPaletteGroup.Selection, 3, new Vector2(280, 32),
                "Row of connected buttons where exactly one is active.",
                container: true, interactive: true, role: AccessibilityRole.Container,
                states: Interactive | DesignerComponentState.Selected,
                bindings: B_Value | B_Vis | B_Inter, color: Field,
                events: new[] { "SelectionChanged" },
                slots: new[] { Slot("segment", "Segment Template", 0, 1, template: true) });

            yield return New("Dropdown", "Dropdown", DesignerComponentCategory.Input,
                DesignerPaletteGroup.Selection, 4, new Vector2(240, 36),
                "Collapsed option picker that opens a list on click.",
                text: "Select...", interactive: true, canHaveChildren: true, role: AccessibilityRole.Button,
                states: Interactive | DesignerComponentState.Error, bindings: B_Text | B_Value | B_Vis | B_Inter,
                color: Field, uxml: "ui:DropdownField", events: new[] { "ValueChanged", "Opened", "Closed" },
                slots: new[] { Slot("option", "Option Template", 0, 1, template: true), Slot("empty", "Empty State", 0, 1) });

            // ---- Controls / fields --------------------------------------------------------------
            yield return New("Slider", "Slider", DesignerComponentCategory.Input,
                DesignerPaletteGroup.Controls, 2, new Vector2(240, 28),
                "Continuous value along a track.",
                interactive: true, valueComponent: true, role: AccessibilityRole.Slider,
                states: Interactive, bindings: B_Value | B_Vis | B_Inter, color: Field,
                uxml: "ui:Slider", events: new[] { "ValueChanged", "DragStarted", "DragEnded" });

            yield return New("RangeSlider", "Range Slider", DesignerComponentCategory.Input,
                DesignerPaletteGroup.Controls, 3, new Vector2(240, 28),
                "Two handles selecting a min/max range.",
                interactive: true, valueComponent: true, role: AccessibilityRole.Slider,
                states: Interactive, bindings: B_Value | B_Vis | B_Inter, color: Field,
                uxml: "ui:MinMaxSlider", events: new[] { "ValueChanged" });

            yield return New("Stepper", "Stepper", DesignerComponentCategory.Input,
                DesignerPaletteGroup.Controls, 4, new Vector2(160, 36),
                "Numeric value with decrement / increment buttons.",
                interactive: true, valueComponent: true, canHaveChildren: true, role: AccessibilityRole.Slider,
                states: Interactive, bindings: B_Value | B_Text | B_Vis | B_Inter, color: Field,
                events: new[] { "ValueChanged", "Incremented", "Decremented" },
                slots: new[] { Slot("decrement", "Decrement", 0, 1), Slot("increment", "Increment", 0, 1) });

            yield return New("TextField", "Text Field", DesignerComponentCategory.Input,
                DesignerPaletteGroup.Controls, 5, new Vector2(280, 36),
                "Single-line text input with placeholder and validation state.",
                text: "", interactive: true, canHaveChildren: true, role: AccessibilityRole.TextField,
                states: FieldStates, bindings: B_Text | B_Value | B_Vis | B_Inter, color: Field,
                uxml: "ui:TextField", events: new[] { "ValueChanged", "Submitted", "Focus", "Blur" },
                slots: new[] { Slot("leading", "Leading Icon", 0, 1), Slot("trailing", "Trailing Icon", 0, 1), Slot("helper", "Helper Text", 0, 1) });

            yield return New("TextArea", "Text Area", DesignerComponentCategory.Input,
                DesignerPaletteGroup.Controls, 6, new Vector2(280, 120),
                "Multi-line text input.",
                interactive: true, role: AccessibilityRole.TextField,
                states: FieldStates, bindings: B_Text | B_Value | B_Vis | B_Inter, color: Field,
                uxml: "ui:TextField", events: new[] { "ValueChanged", "Focus", "Blur" });

            yield return New("SearchField", "Search Field", DesignerComponentCategory.Input,
                DesignerPaletteGroup.Controls, 7, new Vector2(280, 36),
                "Text input specialized for search, with clear and submit affordances.",
                shape: DesignerElementShape.Pill, interactive: true, canHaveChildren: true,
                role: AccessibilityRole.TextField, states: FieldStates,
                bindings: B_Text | B_Value | B_Vis | B_Inter, color: Field, uxml: "ui:TextField",
                events: new[] { "ValueChanged", "Submitted", "Cleared" },
                slots: new[] { Slot("leading", "Search Icon", 0, 1), Slot("clear", "Clear Button", 0, 1) });

            yield return New("Rating", "Rating", DesignerComponentCategory.Input,
                DesignerPaletteGroup.Controls, 8, new Vector2(160, 28),
                "Star/point rating, read-only or interactive.",
                interactive: true, valueComponent: true, role: AccessibilityRole.Slider,
                states: Interactive, bindings: B_Value | B_Vis | B_Inter, color: Transparent,
                events: new[] { "ValueChanged" });

            // ---- Navigation ---------------------------------------------------------------------
            yield return New("Tabs", "Tabs", DesignerComponentCategory.Navigation,
                DesignerPaletteGroup.Navigation, 0, new Vector2(420, 44),
                "Tab strip that switches the visible page.",
                container: true, interactive: true, role: AccessibilityRole.Container,
                states: Interactive | DesignerComponentState.Selected,
                bindings: B_Value | B_Vis | B_Inter, color: Surface, uxml: "ui:TabView",
                events: new[] { "TabChanged" },
                slots: new[] { Slot("tab", "Tab Template", 0, 1, template: true), Slot(DesignerComponentSlot.Content, "Tabs") });

            yield return New("TabItem", "Tab Item", DesignerComponentCategory.Navigation,
                DesignerPaletteGroup.Navigation, 1, new Vector2(120, 40),
                "One tab of a Tabs strip, holding its page content.",
                text: "Tab", container: true, interactive: true, role: AccessibilityRole.Button,
                states: Interactive | DesignerComponentState.Selected,
                bindings: B_Text | B_Vis | B_Cmd | B_Inter, color: Surface,
                uxml: "ui:Tab", uxmlText: true, uxmlTextAttribute: "label",
                events: new[] { "Selected" },
                slots: new[] { Slot("icon", "Icon", 0, 1), Slot(DesignerComponentSlot.Content, "Page") });

            yield return New("AppBar", "App Bar", DesignerComponentCategory.Navigation,
                DesignerPaletteGroup.Navigation, 2, new Vector2(720, 64),
                "Screen header with title, back action and trailing actions.",
                text: "Title", container: true, role: AccessibilityRole.Header,
                bindings: B_Text | B_Vis, color: Surface,
                slots: new[] { Slot("leading", "Leading", 0, 1), Slot(DesignerComponentSlot.Content, "Title"), Slot("actions", "Actions") });

            yield return New("SideNav", "Side Navigation", DesignerComponentCategory.Navigation,
                DesignerPaletteGroup.Navigation, 3, new Vector2(240, 480),
                "Vertical navigation rail / drawer list.",
                container: true, interactive: true, collection: true, role: AccessibilityRole.List,
                states: Interactive | DesignerComponentState.Selected,
                bindings: B_Value | B_Vis, color: Surface,
                events: new[] { "SelectionChanged" },
                slots: new[] { Slot("item", "Item Template", 0, 1, template: true), Slot("header", "Header", 0, 1), Slot("footer", "Footer", 0, 1) });

            yield return New("Breadcrumb", "Breadcrumb", DesignerComponentCategory.Navigation,
                DesignerPaletteGroup.Navigation, 4, new Vector2(360, 28),
                "Path of ancestor screens, each segment navigable.",
                container: true, collection: true, role: AccessibilityRole.List,
                bindings: B_Value | B_Vis | B_Cmd, color: Transparent,
                events: new[] { "SegmentActivated" },
                slots: new[] { Slot("segment", "Segment Template", 0, 1, template: true) });

            yield return New("Pagination", "Pagination", DesignerComponentCategory.Navigation,
                DesignerPaletteGroup.Navigation, 5, new Vector2(280, 36),
                "Page selector with previous / next controls.",
                interactive: true, valueComponent: true, canHaveChildren: true, role: AccessibilityRole.List,
                states: Interactive, bindings: B_Value | B_Vis | B_Inter, color: Transparent,
                events: new[] { "PageChanged" },
                slots: new[] { Slot("previous", "Previous", 0, 1), Slot("next", "Next", 0, 1) });

            yield return New("Menu", "Menu", DesignerComponentCategory.Navigation,
                DesignerPaletteGroup.Navigation, 6, new Vector2(240, 200),
                "Vertical command list opened from a trigger.",
                container: true, interactive: true, collection: true, overlay: true, role: AccessibilityRole.List,
                states: Interactive, bindings: B_Value | B_Vis | B_Cmd, color: Surface,
                events: new[] { "ItemActivated", "Closed" },
                slots: new[] { Slot("item", "Item Template", 0, 1, template: true), Slot("separator", "Separator", 0, 1) });

            yield return New("StepIndicator", "Step Indicator", DesignerComponentCategory.Navigation,
                DesignerPaletteGroup.Navigation, 7, new Vector2(360, 40),
                "Progress through a multi-step flow (wizard/checkout).",
                valueComponent: true, canHaveChildren: true, role: AccessibilityRole.ProgressIndicator,
                states: DesignerComponentState.Normal | DesignerComponentState.Success | DesignerComponentState.Error,
                bindings: B_Value | B_Vis, color: Transparent,
                slots: new[] { Slot("step", "Step Template", 0, 1, template: true) });

            // ---- Feedback ------------------------------------------------------------------------
            yield return New("Alert", "Alert", DesignerComponentCategory.Feedback,
                DesignerPaletteGroup.Feedback, 5, new Vector2(420, 72),
                "Inline banner carrying an informational, warning or error message.",
                text: "Message", container: true, role: AccessibilityRole.Label,
                states: DesignerComponentState.Normal | DesignerComponentState.Success |
                        DesignerComponentState.Warning | DesignerComponentState.Error,
                bindings: B_Text | B_Vis, color: Surface, uxml: "ui:HelpBox", uxmlText: true,
                slots: new[] { Slot("icon", "Icon", 0, 1), Slot(DesignerComponentSlot.Content, "Content"), Slot("action", "Action", 0, 1) });

            yield return New("EmptyState", "Empty State", DesignerComponentCategory.Feedback,
                DesignerPaletteGroup.Feedback, 6, new Vector2(360, 220),
                "Placeholder shown when a collection has nothing to display.",
                text: "Nothing here yet", container: true, role: AccessibilityRole.Container,
                states: DesignerComponentState.Normal | DesignerComponentState.Empty | DesignerComponentState.Error,
                bindings: B_Text | B_Vis, color: Transparent,
                slots: new[] { Slot("icon", "Icon", 0, 1), Slot(DesignerComponentSlot.Content, "Content"), Slot("action", "Action", 0, 1) });

            // ---- Overlay --------------------------------------------------------------------------
            yield return New("Drawer", "Drawer", DesignerComponentCategory.Overlay,
                DesignerPaletteGroup.Overlay, 4, new Vector2(320, 640),
                "Panel that slides in from a screen edge over the content.",
                container: true, overlay: true, role: AccessibilityRole.Dialog,
                bindings: B_Vis | B_Cmd, color: Surface,
                events: new[] { "Opened", "Closed" },
                slots: new[] { Slot("header", "Header", 0, 1), Slot(DesignerComponentSlot.Content, "Content"), Slot("footer", "Footer", 0, 1) });

            yield return New("ContextMenu", "Context Menu", DesignerComponentCategory.Overlay,
                DesignerPaletteGroup.Overlay, 5, new Vector2(220, 180),
                "Command list opened at the pointer / focused element.",
                container: true, overlay: true, collection: true, interactive: true, role: AccessibilityRole.List,
                states: Interactive, bindings: B_Vis | B_Cmd, color: Surface,
                events: new[] { "ItemActivated", "Dismissed" },
                slots: new[] { Slot("item", "Item Template", 0, 1, template: true) });

            yield return New("LoadingOverlay", "Loading Overlay", DesignerComponentCategory.Overlay,
                DesignerPaletteGroup.Overlay, 6, new Vector2(640, 360),
                "Blocking veil with a progress indicator while work is in flight.",
                text: "Loading...", container: true, overlay: true, role: AccessibilityRole.ProgressIndicator,
                states: DesignerComponentState.Normal | DesignerComponentState.Loading | DesignerComponentState.Error,
                bindings: B_Text | B_Value | B_Vis, color: new Color(0.05f, 0.07f, 0.10f, 0.72f),
                slots: new[] { Slot(DesignerComponentSlot.Content, "Content", 0, 1) });

            // ---- Data ------------------------------------------------------------------------------
            yield return New("Table", "Table", DesignerComponentCategory.Data,
                DesignerPaletteGroup.Data, 2, new Vector2(520, 320),
                "Column-based data grid with header, sorting and row template.",
                container: true, collection: true, role: AccessibilityRole.List,
                states: CollectionStates, bindings: B_Value | B_Vis, color: Surface,
                uxml: "ui:MultiColumnListView",
                events: new[] { "RowSelected", "RowActivated", "SortChanged" },
                slots: new[]
                {
                    Slot("header", "Header Row", 0, 1), Slot("row", "Row Template", 0, 1, template: true),
                    Slot("empty", "Empty State", 0, 1), Slot("loading", "Loading State", 0, 1)
                });

            yield return New("TreeView", "Tree View", DesignerComponentCategory.Data,
                DesignerPaletteGroup.Data, 3, new Vector2(320, 360),
                "Hierarchical list with expandable nodes.",
                container: true, collection: true, role: AccessibilityRole.List,
                states: CollectionStates, bindings: B_Value | B_Vis, color: Surface, uxml: "ui:TreeView",
                events: new[] { "SelectionChanged", "Expanded", "Collapsed" },
                slots: new[] { Slot("node", "Node Template", 0, 1, template: true), Slot("empty", "Empty State", 0, 1) });

            yield return New("Carousel", "Carousel", DesignerComponentCategory.Data,
                DesignerPaletteGroup.Data, 4, new Vector2(480, 260),
                "Paged horizontal viewer with page indicators.",
                container: true, collection: true, interactive: true, role: AccessibilityRole.List,
                states: Interactive | DesignerComponentState.Empty, bindings: B_Value | B_Vis,
                color: Surface, events: new[] { "PageChanged" },
                slots: new[] { Slot("page", "Page Template", 0, 1, template: true), Slot("indicator", "Indicator", 0, 1) });

            // ---- Game HUD -----------------------------------------------------------------------------
            yield return New("Minimap", "Minimap", DesignerComponentCategory.Game,
                DesignerPaletteGroup.Game, 2, new Vector2(200, 200),
                "Render-target map view with markers. The render target itself is a runtime concern.",
                shape: DesignerElementShape.Circle, canHaveChildren: true, role: AccessibilityRole.Image,
                bindings: B_Value | B_Vis, color: Surface,
                slots: new[] { Slot("marker", "Marker Template", 0, 1, template: true), Slot("frame", "Frame", 0, 1) });

            yield return New("Compass", "Compass", DesignerComponentCategory.Game,
                DesignerPaletteGroup.Game, 3, new Vector2(360, 48),
                "Heading strip with cardinal ticks and objective markers.",
                canHaveChildren: true, valueComponent: true, role: AccessibilityRole.Image,
                bindings: B_Value | B_Vis, color: Surface,
                slots: new[] { Slot("marker", "Marker Template", 0, 1, template: true) });

            yield return New("Crosshair", "Crosshair", DesignerComponentCategory.Game,
                DesignerPaletteGroup.Game, 4, new Vector2(48, 48),
                "Center reticle with spread/hit-marker states.",
                role: AccessibilityRole.Image,
                states: DesignerComponentState.Normal | DesignerComponentState.Success | DesignerComponentState.Error,
                bindings: B_Value | B_Vis, color: Transparent);

            yield return New("QuestTracker", "Quest Tracker", DesignerComponentCategory.Game,
                DesignerPaletteGroup.Game, 5, new Vector2(320, 220),
                "Objective list with progress per entry.",
                text: "Objectives", container: true, collection: true, role: AccessibilityRole.List,
                states: CollectionStates, bindings: B_Value | B_Text | B_Vis, color: Surface,
                slots: new[] { Slot("header", "Header", 0, 1), Slot("objective", "Objective Template", 0, 1, template: true), Slot("empty", "Empty State", 0, 1) });

            yield return New("DialogueBox", "Dialogue Box", DesignerComponentCategory.Game,
                DesignerPaletteGroup.Game, 6, new Vector2(720, 200),
                "Speaker portrait, name and typewriter body text with choice buttons.",
                text: "...", container: true, role: AccessibilityRole.Dialog,
                states: DesignerComponentState.Normal | DesignerComponentState.Loading,
                bindings: B_Text | B_Vis | B_Cmd, color: Surface,
                events: new[] { "LineCompleted", "Advanced", "ChoiceSelected" },
                slots: new[]
                {
                    Slot("portrait", "Portrait", 0, 1), Slot("speaker", "Speaker Name", 0, 1),
                    Slot(DesignerComponentSlot.Content, "Body", 0, 1), Slot("choices", "Choices")
                });

            yield return New("ChatPanel", "Chat Panel", DesignerComponentCategory.Game,
                DesignerPaletteGroup.Game, 7, new Vector2(420, 300),
                "Scrolling message log with channel tabs and an input row.",
                container: true, collection: true, role: AccessibilityRole.List,
                states: CollectionStates, bindings: B_Value | B_Vis, color: Surface,
                events: new[] { "MessageSubmitted", "ChannelChanged" },
                slots: new[] { Slot("message", "Message Template", 0, 1, template: true), Slot("input", "Input Row", 0, 1), Slot("tabs", "Channel Tabs", 0, 1) });

            yield return New("Leaderboard", "Leaderboard", DesignerComponentCategory.Game,
                DesignerPaletteGroup.Game, 8, new Vector2(420, 360),
                "Ranked score table with a highlighted local player row.",
                container: true, collection: true, role: AccessibilityRole.List,
                states: CollectionStates, bindings: B_Value | B_Vis, color: Surface,
                events: new[] { "RowSelected" },
                slots: new[] { Slot("row", "Row Template", 0, 1, template: true), Slot("self", "Local Player Row", 0, 1), Slot("empty", "Empty State", 0, 1) });

            yield return New("CurrencyDisplay", "Currency Display", DesignerComponentCategory.Game,
                DesignerPaletteGroup.Game, 9, new Vector2(160, 36),
                "Icon plus animated amount for a soft/hard currency.",
                text: "0", shape: DesignerElementShape.Pill, canHaveChildren: true, role: AccessibilityRole.Label,
                bindings: B_Value | B_Text | B_Vis, color: Surface,
                events: new[] { "ValueChanged" },
                slots: new[] { Slot("icon", "Icon", 0, 1), Slot("action", "Add Button", 0, 1) });

            yield return New("BuffBar", "Buff Bar", DesignerComponentCategory.Game,
                DesignerPaletteGroup.Game, 10, new Vector2(320, 48),
                "Row of active buff/debuff icons with duration overlays.",
                container: true, collection: true, role: AccessibilityRole.List,
                states: DesignerComponentState.Normal | DesignerComponentState.Empty,
                bindings: B_Value | B_Vis, color: Transparent,
                slots: new[] { Slot("buff", "Buff Template", 0, 1, template: true) });

            yield return New("CooldownIcon", "Cooldown Icon", DesignerComponentCategory.Game,
                DesignerPaletteGroup.Game, 11, new Vector2(64, 64),
                "Ability icon with a radial cooldown sweep and charge count.",
                canHaveChildren: true, valueComponent: true, interactive: true, role: AccessibilityRole.Button,
                states: Interactive | DesignerComponentState.Loading | DesignerComponentState.Disabled,
                bindings: B_Value | B_Vis | B_Cmd | B_Inter, color: Surface,
                events: new[] { "Activated" },
                slots: new[] { Slot("icon", "Icon", 0, 1), Slot("charges", "Charges", 0, 1), Slot("key", "Key Hint", 0, 1) });

            yield return New("KeyPrompt", "Key Prompt", DesignerComponentCategory.Game,
                DesignerPaletteGroup.Game, 12, new Vector2(120, 32),
                "Input hint that swaps glyphs per device (keyboard / gamepad).",
                text: "E", canHaveChildren: true, role: AccessibilityRole.Label,
                bindings: B_Text | B_Value | B_Vis, color: Field,
                slots: new[] { Slot("glyph", "Glyph", 0, 1), Slot("label", "Label", 0, 1) });
        }

        private static DesignerComponentSlot Slot(string id, string name, int min = 0, int max = int.MaxValue,
            bool template = false, string[] accepted = null)
            => DesignerComponentRegistry.MakeSlot(id, name, min, max, template, false, accepted);

        private static DesignerComponentDescriptor New(
            string typeId, string displayName,
            DesignerComponentCategory category, string paletteGroup, int paletteOrder,
            Vector2 size, string description,
            string text = null,
            bool container = false, bool canHaveChildren = false, bool interactive = false,
            bool valueComponent = false, bool collection = false, bool overlay = false,
            AccessibilityRole role = AccessibilityRole.None,
            DesignerElementShape shape = DesignerElementShape.Rounded,
            DesignerComponentState states = DesignerComponentState.Normal,
            DesignerBindingChannel bindings = DesignerBindingChannel.Visibility,
            string[] events = null,
            DesignerComponentSlot[] slots = null,
            Color? color = null,
            Vector2? minSize = null,
            string uxml = null, bool uxmlText = false, string uxmlTextAttribute = null)
        {
            var descriptor = new DesignerComponentDescriptor
            {
                TypeId = typeId,
                DisplayName = displayName,
                LocalizationKey = "component." + char.ToLowerInvariant(typeId[0]) + typeId.Substring(1),
                Category = category,
                Family = DesignerComponentFamily.NexUI,
                Description = description,
                PaletteGroup = paletteGroup,
                PaletteOrder = paletteOrder,
                DefaultSize = size,
                MinimumSize = minSize ?? new Vector2(24, 24),
                DefaultShape = shape,
                DefaultColor = color ?? Surface,
                DefaultText = text,
                CanHaveChildren = container || canHaveChildren || (slots != null && slots.Length > 0),
                IsContainer = container,
                IsInteractive = interactive,
                IsValueComponent = valueComponent,
                IsCollectionComponent = collection,
                IsOverlayComponent = overlay,
                DefaultAccessibilityRole = role,
                SupportedStates = states,
                SupportedBindings = bindings,
                UxmlTag = uxml,
                UxmlHasText = uxmlText,
                UxmlTextAttribute = uxmlTextAttribute,
                // Honest defaults: the Designer writes rect/text/style for these on both backends, but
                // their behaviour (a Rating's stars, a Carousel's paging) is the runtime's job.
                UGUISupport = DesignerBackendSupport.Partial,
                UIToolkitSupport = DesignerBackendSupport.Partial
            };

            if (events != null)
                descriptor.SupportedEvents.AddRange(events);
            if (slots != null)
                descriptor.Slots.AddRange(slots);
            else if (descriptor.CanHaveChildren)
                descriptor.Slots.Add(DesignerComponentRegistry.MakeSlot(DesignerComponentSlot.Content, "Content"));

            return descriptor;
        }
    }
}

using System.Collections.Generic;
using emiteat.NexUI.Accessibility;
using G = emiteat.NexUI.Designer.Editor.Components.DesignerPaletteGroup;
using static emiteat.NexUI.Designer.Editor.Components.NexUIComponentArchetypes;

namespace emiteat.NexUI.Designer.Editor.Components
{
    /// <summary>
    /// The bulk of NexUI's own component library. <see cref="NexUIComponentCatalog"/> holds the
    /// foundational set; this catalog is the long tail a real product needs - layout scaffolding,
    /// text/media variants, pickers, charts, commerce/social/settings rows and game HUD parts - so a
    /// screen can usually be assembled from the palette instead of being drawn from empty boxes.
    ///
    /// These are <b>components</b>, not recipes: each entry is a first-class type in
    /// <see cref="DesignerComponentRegistry"/> with its own defaults, slots, states, bindings and
    /// backend mapping, and is created directly from the palette. (Recipes, by contrast, are
    /// pre-composed element trees living in <c>DesignerBuiltInComponentCatalog</c>.)
    ///
    /// Entries are written through the archetype helpers at the bottom of the file, which is what
    /// keeps hundreds of descriptors consistent: an archetype fixes the states, binding channels and
    /// accessibility role that every component of that shape should declare, so a new entry is one
    /// line and cannot forget that, say, an input needs Error/Focused states.
    /// </summary>
    public static class NexUILibraryCatalog
    {
        public static IEnumerable<DesignerComponentDescriptor> Build()
        {
            // ---- Layout scaffolding -------------------------------------------------------
            yield return Container("Spacer", "Spacer", G.Layout, 80, 80, "Empty flexible gap used to push siblings apart in an Auto Layout container.", color: Transparent, children: false);
            yield return Container("FlowContainer", "Flow Container", G.Layout, 360, 160, "Wraps children onto the next line when the row runs out of space.");
            yield return Collection("MasonryGrid", "Masonry Grid", G.Layout, 400, 320, "Column grid where items keep their own height, like a pin board.", "item", "Item Template");
            yield return Container("AspectBox", "Aspect Box", G.Layout, 320, 180, "Keeps a fixed aspect ratio while its width follows the parent.");
            yield return Container("SafeArea", "Safe Area", G.Layout, 720, 400, "Insets its content by the device safe area (notch, home indicator).");
            yield return Container("StickyHeader", "Sticky Header", G.Layout, 480, 56, "Header that stays pinned while the content behind it scrolls.", slots: new[] { Slot("content", "Content"), Slot("shadow", "Scrolled Shadow", 0, 1) });
            yield return Container("SidebarLayout", "Sidebar Layout", G.Layout, 720, 420, "Fixed side rail plus a flexible content pane.", slots: new[] { Slot("sidebar", "Sidebar", 0, 1), Slot("content", "Content") });
            yield return Container("TwoColumn", "Two Column", G.Layout, 720, 360, "Two side-by-side content columns.", slots: new[] { Slot("left", "Left", 0, 1), Slot("right", "Right", 0, 1) });
            yield return Container("ThreeColumn", "Three Column", G.Layout, 900, 360, "Three side-by-side content columns.", slots: new[] { Slot("left", "Left", 0, 1), Slot("center", "Center", 0, 1), Slot("right", "Right", 0, 1) });
            yield return Container("CenterBox", "Center Box", G.Layout, 320, 200, "Centers a single child on both axes.", slots: new[] { Slot("content", "Content", 0, 1) });
            yield return Container("DockPanel", "Dock Panel", G.Layout, 640, 400, "Docks children to the edges and fills the remainder with the center slot.", slots: new[] { Slot("top", "Top", 0, 1), Slot("bottom", "Bottom", 0, 1), Slot("left", "Left", 0, 1), Slot("right", "Right", 0, 1), Slot("content", "Center", 0, 1) });
            yield return Container("ResizablePanel", "Resizable Panel", G.Layout, 320, 240, "Panel the player can resize by dragging its edge.", slots: new[] { Slot("content", "Content"), Slot("handle", "Handle", 0, 1) });
            yield return Collection("ScrollSnap", "Scroll Snap Area", G.Layout, 480, 260, "Scroll area that settles on whole pages instead of arbitrary offsets.", "page", "Page Template");
            yield return Container("PageContainer", "Page Container", G.Layout, 720, 480, "Screen-level scaffold with header, scrollable body and footer.", slots: new[] { Slot("header", "Header", 0, 1), Slot("content", "Content"), Slot("footer", "Footer", 0, 1) });
            yield return Container("FieldRow", "Field Row", G.Layout, 420, 40, "Label, control and helper text on one form line.", slots: new[] { Slot("label", "Label", 0, 1), Slot("content", "Control", 0, 1), Slot("helper", "Helper", 0, 1) });
            yield return Container("Form", "Form", G.Layout, 480, 400, "Groups fields with validation summary and submit actions.", slots: new[] { Slot("content", "Fields"), Slot("summary", "Validation Summary", 0, 1), Slot("actions", "Actions", 0, 1) }, states: FieldStates);
            yield return Container("FormSection", "Form Section", G.Layout, 480, 200, "Titled group of related fields.", text: "Section", slots: new[] { Slot("header", "Header", 0, 1), Slot("content", "Fields") }, uxml: "ui:GroupBox", uxmlText: true);
            yield return Container("ListItem", "List Item", G.Layout, 400, 56, "One row: leading visual, primary/secondary text, trailing control.", slots: new[] { Slot("leading", "Leading", 0, 1), Slot("content", "Content"), Slot("trailing", "Trailing", 0, 1) }, interactive: true, role: AccessibilityRole.ListItem);
            yield return Container("PanelHeader", "Panel Header", G.Layout, 480, 44, "Title bar for a panel, with optional actions.", text: "Panel", slots: new[] { Slot("content", "Title", 0, 1), Slot("actions", "Actions", 0, 1) }, role: AccessibilityRole.Header);

            // ---- Text --------------------------------------------------------------------
            yield return Text("Caption", "Caption", 240, 20, "Small secondary text under a primary label.", "Caption");
            yield return Text("Subtitle", "Subtitle", 320, 26, "Secondary heading under a title.", "Subtitle");
            yield return Text("Overline", "Overline", 240, 18, "Small uppercase label above a heading.", "OVERLINE");
            yield return Text("CodeBlock", "Code Block", 400, 140, "Monospaced, scrollable code or log excerpt.", "code();");
            yield return Text("Quote", "Quote", 400, 100, "Pull quote with an accent rule.", "\"Quote\"");
            yield return Text("MarkdownText", "Markdown Text", 400, 160, "Body text authored in a lightweight markup subset.", "**Markdown**");
            yield return Text("NumberTicker", "Number Ticker", 160, 40, "Number that animates from its previous value to the new one.", "1,234", value: true);
            yield return Text("CountdownText", "Countdown Text", 160, 32, "Remaining time rendered as text.", "00:30", value: true);
            yield return Text("TypewriterText", "Typewriter Text", 400, 80, "Body text revealed character by character.", "Typing...");
            yield return Text("Marquee", "Marquee", 320, 28, "Text that scrolls horizontally when it overflows.", "Scrolling notice");
            yield return Text("HighlightText", "Highlight Text", 320, 28, "Text with a highlighted search-match span.", "match");
            yield return Text("TruncatedText", "Truncated Text", 240, 24, "Single-line text that ellipsizes and reveals the rest on hover.", "Truncated text...");
            yield return Text("LabelValue", "Label / Value", 280, 24, "Label on the left, value on the right, aligned as a pair.", "Label", children: true);
            yield return Text("Timestamp", "Timestamp", 180, 22, "Absolute or relative time text ('3분 전').", "3분 전");
            yield return Text("CurrencyText", "Currency Text", 160, 26, "Amount formatted with a currency symbol and grouping.", "₩1,200", value: true);
            yield return Text("PercentText", "Percent Text", 120, 26, "Ratio rendered as a percentage.", "72%", value: true);
            yield return Text("GradientText", "Gradient Text", 280, 36, "Text filled with a gradient (backend support varies).", "Gradient");
            yield return Text("OutlineText", "Outline Text", 280, 36, "Text with an outline/shadow for readability over art.", "Outlined");

            // ---- Media -------------------------------------------------------------------
            yield return Media("VideoView", "Video View", 400, 225, "Video playback surface with optional controls.", children: true);
            yield return Media("RenderTextureView", "Render Texture View", 320, 240, "Draws a camera render target inside the UI.");
            yield return Collection("ImageGallery", "Image Gallery", G.Media, 480, 300, "Paged image viewer with thumbnails.", "image", "Image Template");
            yield return Media("Thumbnail", "Thumbnail", 96, 96, "Small preview image, usually clickable.", interactive: true);
            yield return Media("CoverImage", "Cover Image", 480, 200, "Wide banner image that crops to fill.");
            yield return Media("Logo", "Logo", 160, 48, "Product or team logo lockup.");
            yield return Media("PlaceholderImage", "Placeholder Image", 200, 140, "Empty image slot shown until art is assigned.");
            yield return Media("QRCode", "QR Code", 160, 160, "Generated QR/invite code image.");
            yield return Media("PortraitFrame", "Portrait Frame", 120, 140, "Framed character portrait with rarity/border art.", children: true);
            yield return Media("NineSliceImage", "Nine Slice Image", 240, 160, "Sprite stretched with nine-slice borders.");
            yield return Media("MaskedImage", "Masked Image", 160, 160, "Image clipped by a mask shape.", shape: DesignerElementShape.Circle);
            yield return Media("ParallaxLayer", "Parallax Layer", 480, 240, "Background layer that offsets with pointer or scroll.");
            yield return Media("ParticleHost", "Particle Host", 200, 200, "Anchor for a UI particle effect.");
            yield return Media("ModelView", "3D Model View", 240, 240, "Renders a 3D preview (render texture) inside the UI.");

            // ---- Controls ----------------------------------------------------------------
            yield return Field("NumberField", "Number Field", 200, 36, "Numeric input with optional min/max and step.", uxml: "ui:FloatField");
            yield return Field("PasswordField", "Password Field", 280, 36, "Masked text input with a reveal toggle.", uxml: "ui:TextField");
            yield return Control("Knob", "Knob", 64, 64, "Rotary value control.", value: true, shape: DesignerElementShape.Circle, role: AccessibilityRole.Slider);
            yield return Control("ColorPicker", "Color Picker", 280, 220, "Hue/saturation picker with alpha and hex entry.", value: true, children: true);
            yield return Control("ColorSwatch", "Color Swatch", 40, 40, "Single selectable color chip.", value: true);
            yield return Control("GradientPicker", "Gradient Picker", 280, 60, "Gradient editor with color/alpha stops.", value: true);
            yield return Control("DatePicker", "Date Picker", 240, 36, "Date selection field opening a calendar.", value: true);
            yield return Control("TimePicker", "Time Picker", 200, 36, "Hour/minute selection field.", value: true);
            yield return Control("DateRangePicker", "Date Range Picker", 320, 36, "Start and end date selection.", value: true);
            yield return Collection("Calendar", "Calendar", G.Controls, 320, 300, "Month grid with selectable days and event markers.", "day", "Day Template");
            yield return Field("KeybindField", "Keybind Field", 240, 36, "Captures a key or gamepad button as a binding.");
            yield return Field("VectorField", "Vector Field", 280, 36, "Two to four numeric components on one row.", uxml: "ui:Vector3Field");
            yield return Control("CurveField", "Curve Field", 240, 60, "Animation curve editor field.", value: true);
            yield return Field("FilePicker", "File Picker", 320, 36, "Path field with a browse button.");
            yield return Control("VolumeSlider", "Volume Slider", 240, 32, "Slider with a mute toggle and level readout.", value: true, children: true, uxml: "ui:Slider");
            yield return Control("ToggleButton", "Toggle Button", 120, 36, "Button that stays pressed while its state is on.", text: "Toggle", selectable: true);
            yield return Control("SplitButton", "Split Button", 180, 36, "Primary action plus a dropdown of related actions.", text: "Action", children: true);
            yield return Container("ButtonGroup", "Button Group", G.Controls, 280, 36, "Row of connected buttons acting as one control.", slots: new[] { Slot("content", "Buttons") }, interactive: true);
            yield return Control("FloatingActionButton", "Floating Action Button", 56, 56, "Primary action button floating above the content.", shape: DesignerElementShape.Circle, children: true);
            yield return Control("HoldButton", "Hold Button", 180, 44, "Confirms only after the press is held; shows fill progress.", text: "Hold", value: true);
            yield return Control("RepeatButton", "Repeat Button", 44, 44, "Keeps firing its command while held.", text: "+");
            yield return Collection("RadialMenu", "Radial Menu", G.Controls, 240, 240, "Wheel of choices selected by direction.", "item", "Item Template", shape: DesignerElementShape.Circle);
            yield return Control("VirtualJoystick", "Virtual Joystick", 140, 140, "Touch stick reporting a direction vector.", value: true, shape: DesignerElementShape.Circle);
            yield return Control("DPad", "D-Pad", 140, 140, "Four-direction touch pad.", value: true);
            yield return Control("SwipeArea", "Swipe Area", 320, 200, "Region that reports swipe direction and distance.", children: true);
            yield return Control("DragHandle", "Drag Handle", 40, 24, "Grip that starts a drag on its parent.");
            yield return Control("ResizeHandle", "Resize Handle", 20, 20, "Corner/edge grip that resizes its parent.");
            yield return Control("Scrubber", "Scrubber", 320, 28, "Timeline scrub bar with buffered and played ranges.", value: true, uxml: "ui:Slider");

            // ---- Selection ---------------------------------------------------------------
            yield return Collection("MultiSelect", "Multi Select", G.Selection, 280, 200, "Picks several options at once, showing them as chips.", "option", "Option Template");
            yield return Control("ComboBox", "Combo Box", 260, 36, "Editable text field combined with a dropdown list.", children: true, uxml: "ui:DropdownField");
            yield return Collection("Autocomplete", "Autocomplete", G.Selection, 300, 200, "Text input that suggests matching entries as you type.", "suggestion", "Suggestion Template");
            yield return Collection("TagInput", "Tag Input", G.Selection, 320, 80, "Free-text entry that turns submitted values into removable tags.", "tag", "Tag Template");
            yield return Collection("FilterBar", "Filter Bar", G.Selection, 480, 44, "Row of filter chips with a clear-all action.", "filter", "Filter Template");
            yield return Control("SortSelector", "Sort Selector", 200, 36, "Field plus ascending/descending direction.", children: true);
            yield return Control("OptionCard", "Option Card", 220, 140, "Large selectable card used instead of a radio row.", selectable: true, children: true);
            yield return Collection("TransferList", "Transfer List", G.Selection, 480, 300, "Two lists with move-between actions.", "item", "Item Template");
            yield return Collection("CheckboxGroup", "Checkbox Group", G.Selection, 260, 160, "Several independent checkboxes sharing one binding group.", "option", "Option Template");
            yield return Collection("SelectionList", "Selection List", G.Selection, 300, 260, "Single-select list with a highlighted current row.", "item", "Item Template");

            // ---- Navigation --------------------------------------------------------------
            yield return Collection("NavRail", "Navigation Rail", G.Navigation, 88, 480, "Narrow vertical rail of icon destinations.", "item", "Item Template");
            yield return Collection("BottomNav", "Bottom Navigation", G.Navigation, 480, 64, "Bottom bar of primary destinations.", "item", "Item Template");
            yield return Control("NavItem", "Navigation Item", 200, 44, "One destination row/button inside a navigation surface.", text: "Item", selectable: true, children: true, group: G.Navigation);
            yield return Collection("MenuBar", "Menu Bar", G.Navigation, 480, 32, "Horizontal bar of top-level menus.", "menu", "Menu Template");
            yield return Control("MenuItem", "Menu Item", 200, 32, "One command row inside a menu, with shortcut and submenu arrow.", text: "Menu item", children: true, group: G.Navigation);
            yield return Collection("SubMenu", "Sub Menu", G.Navigation, 200, 160, "Nested menu opened from a parent item.", "item", "Item Template", overlay: true);
            yield return Collection("CommandPalette", "Command Palette", G.Navigation, 520, 360, "Search-driven command launcher overlay.", "command", "Command Template", overlay: true);
            yield return Collection("SearchOverlay", "Search Overlay", G.Navigation, 520, 400, "Full-surface search with recent and suggested results.", "result", "Result Template", overlay: true);
            yield return Control("BackButton", "Back Button", 44, 44, "Navigates to the previous screen.", text: "‹", group: G.Navigation);
            yield return Control("CloseButton", "Close Button", 40, 40, "Dismisses the current overlay or screen.", text: "×", group: G.Navigation);
            yield return Container("Wizard", "Wizard", G.Navigation, 640, 440, "Multi-step flow with progress and next/back actions.", slots: new[] { Slot("progress", "Progress", 0, 1), Slot("content", "Step Content"), Slot("actions", "Actions", 0, 1) });
            yield return Container("WizardStep", "Wizard Step", G.Navigation, 600, 320, "One page of a wizard.", text: "Step", slots: new[] { Slot("content", "Content") });
            yield return Collection("AnchorNav", "Anchor Navigation", G.Navigation, 200, 280, "In-page section links that track the scroll position.", "anchor", "Anchor Template");
            yield return Collection("QuickAccessBar", "Quick Access Bar", G.Navigation, 360, 48, "Small bar of frequently used shortcuts.", "shortcut", "Shortcut Template");

            // ---- Feedback ----------------------------------------------------------------
            yield return Status("StatusDot", "Status Dot", 12, 12, "Tiny colored dot showing online/busy/offline.", shape: DesignerElementShape.Circle);
            yield return Status("StatusPill", "Status Pill", 96, 24, "Labelled status badge.", text: "Active", shape: DesignerElementShape.Pill);
            yield return Meter("LoadingBar", "Loading Bar", 320, 8, "Thin determinate/indeterminate loading strip.");
            yield return Meter("Meter", "Meter", 240, 20, "Generic labelled measurement bar with threshold colors.");
            yield return Meter("Gauge", "Gauge", 160, 100, "Half-circle needle gauge.");
            yield return Meter("Sparkline", "Sparkline", 160, 40, "Compact trend line without axes.");
            yield return Status("TrendIndicator", "Trend Indicator", 80, 24, "Up/down arrow with a delta value.", text: "+12%", value: true);
            yield return Collection("ToastStack", "Toast Stack", G.Feedback, 320, 200, "Stacks queued toasts and expires them in order.", "toast", "Toast Template", overlay: true);
            yield return Status("Snackbar", "Snackbar", 400, 48, "Bottom message strip with a single action.", text: "Saved", children: true, overlay: true);
            yield return Status("InlineError", "Inline Error", 280, 20, "Field-level error message.", text: "Required");
            yield return Status("SuccessCheck", "Success Check", 64, 64, "Animated confirmation checkmark.", shape: DesignerElementShape.Circle);
            yield return Meter("CountdownTimer", "Countdown Timer", 120, 120, "Circular countdown with remaining time.", shape: DesignerElementShape.Circle);
            yield return Meter("CooldownBar", "Cooldown Bar", 200, 12, "Linear ability cooldown.");
            yield return Meter("ComboMeter", "Combo Meter", 200, 48, "Combo counter with a decaying timer bar.");
            yield return Status("ScorePopup", "Score Popup", 120, 40, "Floating score gain that fades out.", text: "+250", value: true);
            yield return Status("DamageNumber", "Damage Number", 80, 32, "World-anchored damage/heal number.", text: "128", value: true);
            yield return Status("FloatingText", "Floating Text", 160, 32, "Generic floating notification text.", text: "Notice");
            yield return Status("ActivityIndicator", "Activity Indicator", 32, 32, "Small busy spinner for inline use.", shape: DesignerElementShape.Circle);
            yield return Status("ConnectionStatus", "Connection Status", 140, 24, "Network/ping state with severity color.", text: "24 ms");
            yield return Status("FpsCounter", "FPS Counter", 100, 24, "Debug frame-rate readout.", text: "60 FPS", value: true);

            // ---- Overlay -----------------------------------------------------------------
            yield return Dialog("ConfirmDialog", "Confirm Dialog", 480, 220, "Yes/no confirmation with a destructive-action warning.", "Are you sure?");
            yield return Dialog("AlertDialog", "Alert Dialog", 440, 200, "Single-acknowledge message dialog.", "Notice");
            yield return Dialog("PromptDialog", "Prompt Dialog", 480, 240, "Dialog asking for one text value.", "Enter a name");
            yield return Dialog("BottomSheet", "Bottom Sheet", 720, 320, "Panel that slides up from the bottom edge.", null);
            yield return Dialog("SideSheet", "Side Sheet", 360, 640, "Panel that slides in from the side, over the content.", null);
            yield return Dialog("Lightbox", "Lightbox", 720, 480, "Full-surface media viewer with a dimmed backdrop.", null);
            yield return Dialog("CoachMark", "Coach Mark", 280, 140, "Anchored tip pointing at a feature during onboarding.", "Tap here");
            yield return Dialog("TutorialOverlay", "Tutorial Overlay", 720, 480, "Step-by-step overlay that gates input to one target.", null);
            yield return Dialog("Spotlight", "Spotlight", 720, 480, "Dim everything except a cut-out around the target.", null);
            yield return Dialog("Backdrop", "Backdrop", 720, 480, "Plain dimming layer behind an overlay; click to dismiss.", null);
            yield return Dialog("HoverCard", "Hover Card", 280, 180, "Rich preview card shown on hover/focus.", null);
            yield return Dialog("ContextPanel", "Context Panel", 320, 480, "Docked detail panel for the current selection.", null);

            // ---- Data --------------------------------------------------------------------
            yield return Container("TableRow", "Table Row", G.Data, 480, 36, "One row of a table, with selection and hover states.", slots: new[] { Slot("content", "Cells") }, interactive: true, role: AccessibilityRole.ListItem);
            yield return Container("TableHeader", "Table Header", G.Data, 480, 36, "Header band holding column headers.", slots: new[] { Slot("content", "Columns") }, role: AccessibilityRole.Header);
            yield return Control("ColumnHeader", "Column Header", 120, 32, "Sortable, resizable column title.", text: "Column", selectable: true, group: G.Data);
            yield return Collection("InfiniteList", "Infinite List", G.Data, 360, 420, "List that loads more items as it reaches the end.", "item", "Item Template");
            yield return Collection("VirtualGrid", "Virtual Grid", G.Data, 420, 420, "Virtualized grid for large item counts.", "cell", "Cell Template");
            yield return Container("TreeNode", "Tree Node", G.Data, 280, 28, "One expandable node row of a tree.", slots: new[] { Slot("content", "Content"), Slot("children", "Children") }, interactive: true);
            yield return Collection("KanbanBoard", "Kanban Board", G.Data, 720, 420, "Columns of draggable cards.", "column", "Column Template");
            yield return Collection("KanbanColumn", "Kanban Column", G.Data, 240, 420, "One status column holding cards.", "card", "Card Template");
            yield return Collection("TimelineList", "Timeline", G.Data, 360, 400, "Chronological entries along a vertical rail.", "entry", "Entry Template");
            yield return Collection("Feed", "Feed", G.Data, 420, 480, "Scrolling stream of posts/events.", "post", "Post Template");
            yield return Collection("CommentThread", "Comment Thread", G.Data, 420, 360, "Nested comments with reply affordances.", "comment", "Comment Template");
            yield return Container("StatCard", "Stat Card", G.Data, 220, 120, "Headline number with label and trend.", text: "1,240", slots: new[] { Slot("content", "Content"), Slot("trend", "Trend", 0, 1) }, value: true);
            yield return Container("KpiTile", "KPI Tile", G.Data, 200, 100, "Compact metric tile for a dashboard row.", text: "KPI", slots: new[] { Slot("content", "Content") }, value: true);
            yield return Container("MetricRow", "Metric Row", G.Data, 360, 32, "Metric name, value and delta on one line.", text: "Metric", slots: new[] { Slot("content", "Content") }, value: true);
            yield return Text("SectionHeader", "Section Header", 360, 28, "Group title inside a long list.", "Section", role: AccessibilityRole.Header, group: G.Data);
            yield return Container("PropertyRow", "Property Row", G.Data, 360, 32, "Inspector-style name/value editing row.", slots: new[] { Slot("label", "Label", 0, 1), Slot("content", "Editor", 0, 1) });
            yield return Collection("KeyValueTable", "Key / Value Table", G.Data, 360, 260, "Two-column table of properties.", "row", "Row Template");
            yield return Collection("LogView", "Log View", G.Data, 480, 300, "Scrolling log with severity filtering.", "line", "Line Template");

            // ---- Charts ------------------------------------------------------------------
            yield return Chart("BarChart", "Bar Chart", 360, 240, "Categorical values as vertical bars.");
            yield return Chart("LineChart", "Line Chart", 360, 240, "Series over time as a line.");
            yield return Chart("AreaChart", "Area Chart", 360, 240, "Line chart with the area under it filled.");
            yield return Chart("PieChart", "Pie Chart", 240, 240, "Parts of a whole as slices.", shape: DesignerElementShape.Circle);
            yield return Chart("DonutChart", "Donut Chart", 240, 240, "Pie chart with a center hole for a total.", shape: DesignerElementShape.Circle);
            yield return Chart("RadarChart", "Radar Chart", 260, 260, "Multi-axis stat comparison polygon.");
            yield return Chart("ScatterPlot", "Scatter Plot", 360, 240, "Point cloud over two axes.");
            yield return Chart("Heatmap", "Heatmap", 320, 240, "Matrix of values shown as color intensity.");
            yield return Chart("Histogram", "Histogram", 360, 240, "Distribution of values across buckets.");
            yield return Chart("GaugeChart", "Gauge Chart", 220, 140, "Single value against a range arc.");
            yield return Chart("FunnelChart", "Funnel Chart", 320, 240, "Stage-by-stage drop-off.");
            yield return Chart("StackedBarChart", "Stacked Bar Chart", 360, 240, "Bars split into stacked segments.");
            yield return Collection("ChartLegend", "Chart Legend", G.Charts, 200, 120, "Series color keys for a chart.", "series", "Series Template");

            // ---- Social ------------------------------------------------------------------
            yield return Container("ProfileCard", "Profile Card", G.Social, 300, 180, "Avatar, name, tagline and quick actions.", text: "Player", slots: new[] { Slot("avatar", "Avatar", 0, 1), Slot("content", "Content"), Slot("actions", "Actions", 0, 1) });
            yield return Container("UserRow", "User Row", G.Social, 360, 56, "Avatar plus name and status on one row.", slots: new[] { Slot("avatar", "Avatar", 0, 1), Slot("content", "Content"), Slot("trailing", "Trailing", 0, 1) }, interactive: true, role: AccessibilityRole.ListItem);
            yield return Container("FriendListItem", "Friend List Item", G.Social, 360, 56, "Friend row with presence and invite/join actions.", slots: new[] { Slot("avatar", "Avatar", 0, 1), Slot("content", "Content"), Slot("actions", "Actions", 0, 1) }, interactive: true, role: AccessibilityRole.ListItem);
            yield return Status("PresenceDot", "Presence Dot", 12, 12, "Online/away/offline indicator, usually on an avatar.", shape: DesignerElementShape.Circle, group: G.Social);
            yield return Control("FollowButton", "Follow Button", 100, 32, "Follow/unfollow toggle with pending state.", text: "Follow", selectable: true, group: G.Social);
            yield return Dialog("ShareSheet", "Share Sheet", 480, 320, "Target picker for sharing a link or result.", null);
            yield return Container("ReviewCard", "Review Card", G.Social, 400, 160, "Rating, author and review body.", slots: new[] { Slot("header", "Header", 0, 1), Slot("content", "Body") });
            yield return Container("RatingSummary", "Rating Summary", G.Social, 320, 160, "Average score with a per-star distribution.", slots: new[] { Slot("content", "Bars") }, value: true);
            yield return Container("CommentInput", "Comment Input", G.Social, 420, 64, "Avatar, text field and submit action.", slots: new[] { Slot("avatar", "Avatar", 0, 1), Slot("content", "Input", 0, 1), Slot("actions", "Actions", 0, 1) }, states: FieldStates, interactive: true);
            yield return Collection("MentionList", "Mention List", G.Social, 280, 200, "@-mention suggestions while typing.", "user", "User Template", overlay: true);

            // ---- Commerce ----------------------------------------------------------------
            yield return Container("ProductCard", "Product Card", G.Commerce, 240, 300, "Image, name, price and buy action.", slots: new[] { Slot("image", "Image", 0, 1), Slot("content", "Content"), Slot("actions", "Actions", 0, 1) }, interactive: true);
            yield return Status("PriceTag", "Price Tag", 120, 32, "Current price with an optional struck-through original.", text: "₩9,900", value: true, group: G.Commerce);
            yield return Status("DiscountBadge", "Discount Badge", 64, 24, "Percentage-off badge.", text: "-30%", shape: DesignerElementShape.Pill, group: G.Commerce);
            yield return Container("CartItem", "Cart Item", G.Commerce, 420, 88, "Line item with quantity stepper and remove action.", slots: new[] { Slot("image", "Image", 0, 1), Slot("content", "Content"), Slot("actions", "Actions", 0, 1) });
            yield return Container("CheckoutSummary", "Checkout Summary", G.Commerce, 360, 220, "Subtotal, discounts, tax and total.", slots: new[] { Slot("content", "Lines"), Slot("total", "Total", 0, 1) });
            yield return Container("PaymentMethodRow", "Payment Method Row", G.Commerce, 400, 56, "Selectable saved payment method.", slots: new[] { Slot("icon", "Icon", 0, 1), Slot("content", "Content") }, interactive: true, selectable: true);
            yield return Field("CouponField", "Coupon Field", 320, 40, "Promo code entry with validation feedback.", group: G.Commerce);
            yield return Control("PurchaseButton", "Purchase Button", 200, 48, "Primary buy action showing price and busy state.", text: "Buy", children: true, group: G.Commerce);
            yield return Container("SubscriptionCard", "Subscription Card", G.Commerce, 260, 320, "Tier name, price, features and select action.", slots: new[] { Slot("header", "Header", 0, 1), Slot("content", "Features"), Slot("actions", "Actions", 0, 1) }, selectable: true, interactive: true);
            yield return Container("ShopItemCard", "Shop Item Card", G.Commerce, 200, 260, "In-game shop entry with cost and owned state.", slots: new[] { Slot("image", "Image", 0, 1), Slot("content", "Content"), Slot("cost", "Cost", 0, 1) }, interactive: true);
            yield return Container("CurrencyPack", "Currency Pack", G.Commerce, 200, 220, "Purchasable currency bundle with bonus badge.", slots: new[] { Slot("image", "Image", 0, 1), Slot("content", "Content"), Slot("badge", "Bonus Badge", 0, 1) }, interactive: true);
            yield return Container("RewardCard", "Reward Card", G.Commerce, 200, 240, "Claimable reward with claim/claimed state.", slots: new[] { Slot("image", "Image", 0, 1), Slot("content", "Content"), Slot("actions", "Actions", 0, 1) }, interactive: true, selectable: true);

            // ---- Settings ----------------------------------------------------------------
            yield return Container("SettingsRow", "Settings Row", G.Settings, 480, 48, "Setting name, description and its control.", slots: new[] { Slot("content", "Label"), Slot("control", "Control", 0, 1) }, interactive: true);
            yield return Container("SettingsToggleRow", "Settings Toggle Row", G.Settings, 480, 48, "Setting row whose control is a switch.", slots: new[] { Slot("content", "Label"), Slot("control", "Toggle", 0, 1) }, interactive: true, selectable: true);
            yield return Container("SettingsSliderRow", "Settings Slider Row", G.Settings, 480, 56, "Setting row whose control is a slider with a value readout.", slots: new[] { Slot("content", "Label"), Slot("control", "Slider", 0, 1) }, interactive: true, value: true);
            yield return Container("SettingsSection", "Settings Section", G.Settings, 480, 200, "Titled group of setting rows.", text: "Section", slots: new[] { Slot("header", "Header", 0, 1), Slot("content", "Rows") });
            yield return Control("LanguageSelector", "Language Selector", 240, 40, "Language picker with native names.", children: true, group: G.Settings);
            yield return Container("VolumeRow", "Volume Row", G.Settings, 480, 48, "Audio channel level with mute.", slots: new[] { Slot("content", "Label"), Slot("control", "Slider", 0, 1) }, value: true, interactive: true);
            yield return Container("KeybindRow", "Keybind Row", G.Settings, 480, 44, "Action name with its current binding and rebind action.", slots: new[] { Slot("content", "Action"), Slot("control", "Binding", 0, 1) }, interactive: true);
            yield return Container("AccountRow", "Account Row", G.Settings, 480, 64, "Linked account with connect/disconnect action.", slots: new[] { Slot("icon", "Icon", 0, 1), Slot("content", "Content"), Slot("actions", "Actions", 0, 1) }, interactive: true);
            yield return Container("LegalRow", "Legal Row", G.Settings, 480, 44, "Terms/privacy link row.", slots: new[] { Slot("content", "Label") }, interactive: true);
            yield return Container("AboutRow", "About Row", G.Settings, 480, 44, "Version/build information row.", slots: new[] { Slot("content", "Label"), Slot("control", "Value", 0, 1) });

            // ---- Game HUD ----------------------------------------------------------------
            yield return Collection("AbilityBar", "Ability Bar", G.Game, 400, 72, "Row of ability slots with cooldowns and key hints.", "ability", "Ability Template");
            yield return Container("SkillSlot", "Skill Slot", G.Game, 64, 64, "One ability cell with cooldown, charges and key hint.", slots: new[] { Slot("icon", "Icon", 0, 1), Slot("key", "Key Hint", 0, 1), Slot("charges", "Charges", 0, 1) }, interactive: true, value: true);
            yield return Container("SkillTreeNode", "Skill Tree Node", G.Game, 72, 72, "Talent node with locked/available/learned states.", slots: new[] { Slot("icon", "Icon", 0, 1), Slot("rank", "Rank", 0, 1) }, interactive: true, selectable: true, shape: DesignerElementShape.Circle);
            yield return Collection("InventoryGrid", "Inventory Grid", G.Game, 400, 320, "Grid of item slots with drag and drop.", "slot", "Slot Template");
            yield return Container("EquipmentSlot", "Equipment Slot", G.Game, 72, 72, "Typed equipment cell that only accepts matching items.", slots: new[] { Slot("icon", "Icon", 0, 1), Slot("badge", "Badge", 0, 1) }, interactive: true);
            yield return Container("CharacterPortrait", "Character Portrait", G.Game, 120, 140, "Portrait with level, class and status effects.", slots: new[] { Slot("image", "Image", 0, 1), Slot("content", "Content"), Slot("effects", "Status Effects", 0, 1) });
            yield return Container("PartyFrame", "Party Frame", G.Game, 220, 72, "Ally frame with health, resource and role icon.", slots: new[] { Slot("portrait", "Portrait", 0, 1), Slot("content", "Bars"), Slot("effects", "Buffs", 0, 1) }, value: true);
            yield return Container("TargetFrame", "Target Frame", G.Game, 260, 80, "Current target's name, health and cast bar.", slots: new[] { Slot("portrait", "Portrait", 0, 1), Slot("content", "Bars"), Slot("cast", "Cast Bar", 0, 1) }, value: true);
            yield return Meter("CastBar", "Cast Bar", 240, 20, "Spell cast progress with interrupt state.", group: G.Game);
            yield return Status("ComboCounter", "Combo Counter", 120, 56, "Hit-combo count with a decay timer.", text: "x12", value: true, group: G.Game);
            yield return Collection("KillFeed", "Kill Feed", G.Game, 320, 160, "Recent elimination events that fade out.", "event", "Event Template");
            yield return Status("ObjectiveMarker", "Objective Marker", 48, 48, "World-anchored objective icon with distance.", text: "120m", group: G.Game);
            yield return Status("Waypoint", "Waypoint", 40, 40, "Directional waypoint pip clamped to the screen edge.", group: G.Game);
            yield return Status("AmmoCounter", "Ammo Counter", 140, 48, "Magazine and reserve ammunition readout.", text: "30 / 120", value: true, group: G.Game);
            yield return Collection("WeaponWheel", "Weapon Wheel", G.Game, 280, 280, "Radial weapon selector.", "weapon", "Weapon Template", shape: DesignerElementShape.Circle);
            yield return Container("InteractionPrompt", "Interaction Prompt", G.Game, 200, 40, "Key hint plus verb shown near an interactable.", text: "Open", slots: new[] { Slot("key", "Key", 0, 1), Slot("content", "Label", 0, 1) });
            yield return Text("SubtitleBar", "Subtitle Bar", 720, 64, "Dialogue subtitles with speaker name.", "Subtitle line", group: G.Game);
            yield return Meter("BossHealthBar", "Boss Health Bar", 640, 40, "Boss health with phase segments and name.", group: G.Game);
            yield return Meter("ResourceOrb", "Resource Orb", 96, 96, "Orb-shaped resource (health/mana) fill.", shape: DesignerElementShape.Circle, group: G.Game);
            yield return Meter("XpBar", "XP Bar", 480, 16, "Experience progress toward the next level.", group: G.Game);
            yield return Status("LevelBadge", "Level Badge", 40, 40, "Current level number badge.", text: "12", shape: DesignerElementShape.Circle, group: G.Game);
            yield return Status("RankEmblem", "Rank Emblem", 72, 72, "Competitive rank emblem with tier.", text: "Gold", group: G.Game);
            yield return Status("AchievementToast", "Achievement Toast", 340, 72, "Unlock notification with icon and title.", text: "Achievement unlocked", children: true, overlay: true, group: G.Game);
            yield return Status("LootPopup", "Loot Popup", 260, 56, "Item acquired notification with rarity color.", text: "+ Item", children: true, overlay: true, group: G.Game);
            yield return Container("Nameplate", "Nameplate", G.Game, 160, 44, "World-space name, level and health above a character.", slots: new[] { Slot("content", "Name"), Slot("bar", "Health Bar", 0, 1) }, value: true);
            yield return Status("ChatBubble", "Chat Bubble", 220, 56, "Speech bubble anchored to a character.", text: "Hello!", shape: DesignerElementShape.Pill, group: G.Game);
            yield return Collection("EmoteWheel", "Emote Wheel", G.Game, 260, 260, "Radial emote picker.", "emote", "Emote Template", shape: DesignerElementShape.Circle);
            yield return Collection("PingWheel", "Ping Wheel", G.Game, 240, 240, "Radial callout/ping picker.", "ping", "Ping Template", shape: DesignerElementShape.Circle);
            yield return Meter("Speedometer", "Speedometer", 180, 120, "Vehicle speed dial.", group: G.Game);
            yield return Status("LapTimer", "Lap Timer", 180, 60, "Current, best and delta lap times.", text: "01:23.45", value: true, group: G.Game);
            yield return Container("MapLegend", "Map Legend", G.Game, 200, 160, "Key for minimap marker types.", slots: new[] { Slot("content", "Entries") });
            yield return Container("CraftingSlot", "Crafting Slot", G.Game, 72, 72, "Ingredient cell showing required and owned counts.", slots: new[] { Slot("icon", "Icon", 0, 1), Slot("count", "Count", 0, 1) }, interactive: true);
            yield return Container("QuestCard", "Quest Card", G.Game, 320, 160, "Quest title, objectives and reward preview.", slots: new[] { Slot("header", "Header", 0, 1), Slot("content", "Objectives"), Slot("reward", "Reward", 0, 1) }, interactive: true);
            yield return Container("TradePanel", "Trade Panel", G.Game, 640, 360, "Two-sided trade offer with ready states.", slots: new[] { Slot("left", "Your Offer", 0, 1), Slot("right", "Their Offer", 0, 1), Slot("actions", "Actions", 0, 1) });
        }
    }
}

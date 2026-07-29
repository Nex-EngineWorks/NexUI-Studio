using System.Collections.Generic;
using UnityEngine;
using static emiteat.NexUI.Designer.Editor.Components.DesignerComponentPropertyBuilder;
using Group = emiteat.NexUI.Designer.Editor.Components.DesignerComponentPropertyGroup;
using Exposure = emiteat.NexUI.Designer.Editor.Components.DesignerComponentPropertyExposure;

namespace emiteat.NexUI.Designer.Editor.Components
{
    /// <summary>
    /// Gives every registered component its property schema - the Designer's equivalent of a Unity
    /// component's serialized fields.
    /// </summary>
    /// <remarks>
    /// Schemas are attached by <b>shape</b> first (value components get range/direction, collections
    /// get selection and virtualization, inputs get placeholder and validation…), then refined per
    /// type. That is what makes hundreds of components feel like real Unity components without
    /// hand-writing hundreds of schemas: a Health Bar, an XP Bar and a Cast Bar are all value
    /// components, so they all get the same well-considered set, and only the handful of types with
    /// genuinely unique behaviour need their own entries.
    ///
    /// Only values the user changes are stored on the element, so adding a property here never
    /// migrates existing screens.
    /// </remarks>
    internal static class NexUIComponentPropertySchemas
    {
        private static readonly string[] Directions = { "LeftToRight", "RightToLeft", "BottomToTop", "TopToBottom" };
        private static readonly string[] SelectionModes = { "None", "Single", "Multiple" };
        private static readonly string[] Orientations = { "Horizontal", "Vertical" };
        private static readonly string[] ScrollModes = { "Auto", "AlwaysVisible", "Hidden" };
        private static readonly string[] MovementTypes = { "Unrestricted", "Elastic", "Clamped" };
        private static readonly string[] ContentTypes = { "Standard", "IntegerNumber", "DecimalNumber", "Alphanumeric", "Name", "EmailAddress", "Password", "Pin", "Custom" };
        private static readonly string[] LineTypes = { "SingleLine", "MultiLineSubmit", "MultiLineNewline" };
        private static readonly string[] Transitions = { "None", "ColorTint", "SpriteSwap", "Animation" };
        private static readonly string[] Severities = { "Info", "Success", "Warning", "Error" };
        private static readonly string[] Placements = { "Top", "Bottom", "Left", "Right", "Center" };
        private static readonly string[] Sizes = { "Small", "Medium", "Large" };
        private static readonly string[] Emphasis = { "Primary", "Secondary", "Tertiary", "Danger" };

        public static void Apply(DesignerComponentDescriptor descriptor)
        {
            if (descriptor == null || descriptor.Properties.Count > 0) return;
            var properties = new List<DesignerComponentProperty>();

            AddCommon(descriptor, properties);
            AddByShape(descriptor, properties);
            AddByType(descriptor, properties);

            descriptor.Properties.AddRange(properties);
        }

        // ---- Shape-driven schemas ---------------------------------------------------------

        /// <summary>Properties every component carries, mirroring what Unity puts on every control.</summary>
        private static void AddCommon(DesignerComponentDescriptor descriptor, List<DesignerComponentProperty> properties)
        {
            if (descriptor.IsInteractive)
            {
                properties.Add(Bool("interactable", "Interactable", true, Group.Interaction,
                    description: "Whether the control accepts input at runtime.", uxml: "enabled"));
                properties.Add(Enum("transition", "Transition", Transitions, 1, Group.Interaction, Exposure.Advanced,
                    "How the control reacts visually to hover/press/disable."));
                properties.Add(Bool("navigation.enabled", "Keyboard / Gamepad Navigation", true, Group.Interaction, Exposure.Advanced,
                    "Include this control in focus navigation."));
                properties.Add(Enum("emphasis", "Emphasis", Emphasis, 0, Group.Appearance, Exposure.Advanced,
                    "Visual weight of the control in its context."));
            }

            if (descriptor.CanHaveChildren)
                properties.Add(Bool("clipContent", "Clip Content", false, Group.Layout, Exposure.Advanced,
                    "Clip children to this element's bounds."));

            properties.Add(Enum("size", "Size", Sizes, 1, Group.Appearance, Exposure.Advanced,
                "Density preset applied to padding and text size."));
        }

        private static void AddByShape(DesignerComponentDescriptor descriptor, List<DesignerComponentProperty> properties)
        {
            if (descriptor.IsValueComponent) AddValue(properties);
            if (descriptor.IsCollectionComponent) AddCollection(properties);
            if (descriptor.Category == DesignerComponentCategory.Text) AddText(properties);
            if (descriptor.Category == DesignerComponentCategory.Media) AddMedia(properties);
            if (descriptor.IsOverlayComponent) AddOverlay(properties);
            if (descriptor.IsContainer) AddContainer(properties);
        }

        private static void AddValue(List<DesignerComponentProperty> properties)
        {
            properties.Add(Float("value.min", "Min Value", 0f, Group.Value, uxml: "low-value"));
            properties.Add(Float("value.max", "Max Value", 100f, Group.Value, uxml: "high-value"));
            properties.Add(Bool("value.wholeNumbers", "Whole Numbers", false, Group.Value,
                description: "Round the value to integers."));
            properties.Add(Enum("value.direction", "Fill Direction", Directions, 0, Group.Value, Exposure.Advanced));
            properties.Add(Int("value.segments", "Segments", 0, Group.Value, Exposure.Advanced, 0f, 64f,
                "Split the fill into discrete chunks. 0 keeps it continuous."));
            properties.Add(Bool("value.showLabel", "Show Value Label", false, Group.Value, Exposure.Advanced));
            properties.Add(Text("value.format", "Value Format", "{0:0}", Group.Value, Exposure.Advanced,
                "Composite format applied to the value label."));
            properties.Add(Float("value.animationDuration", "Change Animation (s)", 0.2f, Group.Behavior, Exposure.Advanced, 0f, 2f,
                "Seconds the fill takes to catch up to a new value."));
        }

        private static void AddCollection(List<DesignerComponentProperty> properties)
        {
            properties.Add(Text("items.source", "Items Source Key", "", Group.Data,
                description: "Runtime state key that supplies the items."));
            properties.Add(Enum("items.selection", "Selection Mode", SelectionModes, 1, Group.Data));
            properties.Add(Bool("items.virtualize", "Virtualize", true, Group.Data, Exposure.Advanced,
                "Only build views for visible items."));
            properties.Add(Float("items.itemSize", "Item Size", 0f, Group.Data, Exposure.Advanced, 0f, 512f,
                "Fixed item height/width used by virtualization. 0 measures each item."));
            properties.Add(Float("items.spacing", "Item Spacing", 4f, Group.Layout, Exposure.Advanced, 0f, 64f));
            properties.Add(Enum("items.orientation", "Orientation", Orientations, 1, Group.Layout, Exposure.Advanced));
            properties.Add(Bool("items.reorderable", "Reorderable", false, Group.Data, Exposure.Advanced));
            properties.Add(Bool("items.showEmptyState", "Show Empty State", true, Group.Data, Exposure.Advanced));
            properties.Add(Int("items.previewCount", "Preview Item Count", 0, Group.Data, Exposure.Advanced, 0f, 64f,
                "Generated items shown on the canvas only."));
        }

        private static void AddText(List<DesignerComponentProperty> properties)
        {
            properties.Add(Bool("text.richText", "Rich Text", true, Group.Content, Exposure.Advanced));
            properties.Add(Bool("text.autoSize", "Auto Size", false, Group.Content, Exposure.Advanced));
            properties.Add(Int("text.maxLines", "Max Lines", 0, Group.Content, Exposure.Advanced, 0f, 32f,
                "0 keeps the text unbounded."));
            properties.Add(Bool("text.selectable", "Selectable", false, Group.Interaction, Exposure.Advanced));
            properties.Add(Text("text.localizationKey", "Localization Key", "", Group.Content, Exposure.Advanced,
                "Runtime localization table key. Overrides the authored text at play time."));
        }

        private static void AddMedia(List<DesignerComponentProperty> properties)
        {
            properties.Add(Asset("media.sprite", "Sprite", typeof(Sprite), Group.Content));
            properties.Add(Bool("media.preserveAspect", "Preserve Aspect", true, Group.Appearance));
            properties.Add(Bool("media.raycastTarget", "Raycast Target", true, Group.Interaction, Exposure.Advanced,
                "Whether the graphic blocks pointer input."));
            properties.Add(Bool("media.maskable", "Maskable", true, Group.Appearance, Exposure.Advanced));
            properties.Add(Color("media.tint", "Tint", UnityEngine.Color.white, Group.Appearance, Exposure.Advanced));
        }

        private static void AddOverlay(List<DesignerComponentProperty> properties)
        {
            properties.Add(Bool("overlay.modal", "Modal", true, Group.Behavior,
                description: "Block input to everything behind this surface."));
            properties.Add(Bool("overlay.dismissOnBackdrop", "Dismiss On Backdrop Click", true, Group.Behavior));
            properties.Add(Bool("overlay.dismissOnEscape", "Dismiss On Cancel Input", true, Group.Behavior, Exposure.Advanced));
            properties.Add(Enum("overlay.placement", "Placement", Placements, 4, Group.Layout, Exposure.Advanced));
            properties.Add(Float("overlay.backdropOpacity", "Backdrop Opacity", 0.6f, Group.Appearance, Exposure.Advanced, 0f, 1f));
            properties.Add(Bool("overlay.trapFocus", "Trap Focus", true, Group.Interaction, Exposure.Advanced));
            properties.Add(Float("overlay.autoDismissSeconds", "Auto Dismiss (s)", 0f, Group.Behavior, Exposure.Advanced, 0f, 30f,
                "0 keeps the surface open until dismissed."));
        }

        private static void AddContainer(List<DesignerComponentProperty> properties)
        {
            properties.Add(Bool("container.scrollable", "Scrollable", false, Group.Layout, Exposure.Advanced));
            properties.Add(Bool("container.blocksRaycast", "Blocks Raycast", true, Group.Interaction, Exposure.Advanced));
        }

        // ---- Type-specific schemas --------------------------------------------------------

        private static void AddByType(DesignerComponentDescriptor descriptor, List<DesignerComponentProperty> properties)
        {
            switch (descriptor.TypeId)
            {
                case "Slider":
                case "VolumeSlider":
                case "Scrubber":
                case "UGUI.Slider":
                case "UITK.Slider":
                case "UITK.SliderInt":
                    properties.Add(Float("slider.step", "Step", 0f, Group.Value, Exposure.Advanced, 0f, 100f,
                        "Snap increment. 0 keeps the value continuous."));
                    properties.Add(Bool("slider.showTicks", "Show Ticks", false, Group.Appearance, Exposure.Advanced));
                    properties.Add(Bool("slider.fillHandle", "Show Handle", true, Group.Appearance, Exposure.Advanced));
                    break;

                case "RangeSlider":
                case "UITK.MinMaxSlider":
                    properties.Add(Float("range.low", "Low Value", 25f, Group.Value));
                    properties.Add(Float("range.high", "High Value", 75f, Group.Value));
                    properties.Add(Float("range.minGap", "Minimum Gap", 0f, Group.Value, Exposure.Advanced));
                    break;

                case "Checkbox":
                case "Switch":
                case "ToggleButton":
                case "UGUI.Toggle":
                case "UITK.Toggle":
                case "UITK.RadioButton":
                    properties.Add(Bool("toggle.isOn", "Is On", false, Group.Value, uxml: "value"));
                    properties.Add(Bool("toggle.allowIndeterminate", "Allow Indeterminate", false, Group.Value, Exposure.Advanced));
                    properties.Add(Text("toggle.group", "Toggle Group", "", Group.Interaction, Exposure.Advanced,
                        "Name of the group that allows only one member to be on."));
                    break;

                case "Dropdown":
                case "ComboBox":
                case "UGUI.Dropdown":
                case "UGUI.DropdownTMP":
                case "UITK.DropdownField":
                    properties.Add(Text("choice.options", "Options", "", Group.Data,
                        description: "Comma-separated options used for preview and for the generated control."));
                    properties.Add(Int("choice.value", "Selected Index", 0, Group.Value));
                    properties.Add(Text("choice.placeholder", "Placeholder", "Select...", Group.Content));
                    properties.Add(Bool("choice.searchable", "Searchable", false, Group.Interaction, Exposure.Advanced));
                    properties.Add(Int("choice.maxVisible", "Max Visible Options", 8, Group.Layout, Exposure.Advanced, 1f, 32f));
                    break;

                case "TextField":
                case "TextArea":
                case "SearchField":
                case "NumberField":
                case "PasswordField":
                case "CouponField":
                case "UGUI.InputField":
                case "UGUI.InputFieldTMP":
                case "UITK.TextField":
                    properties.Add(Text("input.placeholder", "Placeholder", "Enter text...", Group.Content, uxml: "placeholder-text"));
                    properties.Add(Int("input.maxLength", "Character Limit", 0, Group.Content, Exposure.Advanced, 0f, 4096f,
                        "0 means unlimited."));
                    properties.Add(Enum("input.contentType", "Content Type", ContentTypes, 0, Group.Content, Exposure.Advanced));
                    properties.Add(Enum("input.lineType", "Line Type", LineTypes, 0, Group.Content, Exposure.Advanced));
                    properties.Add(Bool("input.readOnly", "Read Only", false, Group.Interaction, Exposure.Advanced, uxml: "readonly"));
                    properties.Add(Bool("input.selectAllOnFocus", "Select All On Focus", false, Group.Interaction, Exposure.Advanced));
                    properties.Add(Bool("input.submitOnEnter", "Submit On Enter", true, Group.Behavior, Exposure.Advanced));
                    properties.Add(Bool("input.clearButton", "Clear Button", false, Group.Appearance, Exposure.Advanced));
                    break;

                case "ScrollArea":
                case "UGUI.ScrollView":
                case "UITK.ScrollView":
                    properties.Add(Bool("scroll.horizontal", "Horizontal", false, Group.Layout));
                    properties.Add(Bool("scroll.vertical", "Vertical", true, Group.Layout));
                    properties.Add(Enum("scroll.movement", "Movement Type", MovementTypes, 1, Group.Behavior, Exposure.Advanced));
                    properties.Add(Float("scroll.elasticity", "Elasticity", 0.1f, Group.Behavior, Exposure.Advanced, 0f, 1f));
                    properties.Add(Bool("scroll.inertia", "Inertia", true, Group.Behavior, Exposure.Advanced));
                    properties.Add(Float("scroll.decelerationRate", "Deceleration Rate", 0.135f, Group.Behavior, Exposure.Advanced, 0f, 1f));
                    properties.Add(Float("scroll.sensitivity", "Scroll Sensitivity", 1f, Group.Behavior, Exposure.Advanced, 0f, 10f));
                    properties.Add(Enum("scroll.verticalBar", "Vertical Scrollbar", ScrollModes, 0, Group.Appearance, Exposure.Advanced));
                    properties.Add(Enum("scroll.horizontalBar", "Horizontal Scrollbar", ScrollModes, 2, Group.Appearance, Exposure.Advanced));
                    break;

                case "Tabs":
                case "SegmentedControl":
                case "UITK.TabView":
                    properties.Add(Int("tabs.activeIndex", "Active Tab", 0, Group.Value));
                    properties.Add(Enum("tabs.orientation", "Orientation", Orientations, 0, Group.Layout, Exposure.Advanced));
                    properties.Add(Bool("tabs.closable", "Closable Tabs", false, Group.Interaction, Exposure.Advanced));
                    properties.Add(Bool("tabs.scrollable", "Scrollable Strip", false, Group.Layout, Exposure.Advanced));
                    break;

                case "Accordion":
                case "UITK.Foldout":
                    properties.Add(Bool("foldout.expanded", "Expanded", true, Group.Value, uxml: "value"));
                    properties.Add(Bool("foldout.singleOpen", "Only One Section Open", false, Group.Behavior, Exposure.Advanced));
                    break;

                case "Button":
                case "IconButton":
                case "Link":
                case "PurchaseButton":
                case "FloatingActionButton":
                case "UGUI.Button":
                case "UGUI.ButtonTMP":
                case "UITK.Button":
                    properties.Add(Bool("button.submitOnHold", "Repeat While Held", false, Group.Behavior, Exposure.Advanced));
                    properties.Add(Float("button.holdSeconds", "Hold Duration (s)", 0f, Group.Behavior, Exposure.Advanced, 0f, 5f,
                        "Require the button to be held this long before it fires. 0 fires on release."));
                    properties.Add(Bool("button.busyState", "Has Busy State", false, Group.Behavior, Exposure.Advanced,
                        "Show a spinner and block repeat presses while the command runs."));
                    break;

                case "Modal":
                case "ConfirmDialog":
                case "AlertDialog":
                case "PromptDialog":
                    properties.Add(Text("dialog.confirmLabel", "Confirm Label", "OK", Group.Content));
                    properties.Add(Text("dialog.cancelLabel", "Cancel Label", "Cancel", Group.Content));
                    properties.Add(Bool("dialog.destructive", "Destructive Action", false, Group.Appearance, Exposure.Advanced));
                    break;

                case "Alert":
                case "Toast":
                case "Snackbar":
                case "UITK.HelpBox":
                    properties.Add(Enum("message.severity", "Severity", Severities, 0, Group.Appearance));
                    properties.Add(Bool("message.dismissible", "Dismissible", true, Group.Interaction, Exposure.Advanced));
                    properties.Add(Bool("message.showIcon", "Show Icon", true, Group.Appearance, Exposure.Advanced));
                    break;

                case "Table":
                case "UITK.MultiColumnListView":
                    properties.Add(Int("table.columns", "Columns", 3, Group.Data, min: 1f, max: 24f));
                    properties.Add(Bool("table.sortable", "Sortable", true, Group.Interaction, Exposure.Advanced));
                    properties.Add(Bool("table.resizableColumns", "Resizable Columns", true, Group.Interaction, Exposure.Advanced));
                    properties.Add(Bool("table.stripedRows", "Striped Rows", true, Group.Appearance, Exposure.Advanced));
                    properties.Add(Bool("table.stickyHeader", "Sticky Header", true, Group.Layout, Exposure.Advanced));
                    break;

                case "Pagination":
                case "Stepper":
                    properties.Add(Float("stepper.step", "Step", 1f, Group.Value));
                    properties.Add(Bool("stepper.wrap", "Wrap Around", false, Group.Behavior, Exposure.Advanced));
                    properties.Add(Bool("stepper.repeatOnHold", "Repeat While Held", true, Group.Behavior, Exposure.Advanced));
                    break;

                case "Rating":
                case "StarRatingResult":
                    properties.Add(Int("rating.max", "Star Count", 5, Group.Value, min: 1f, max: 10f));
                    properties.Add(Bool("rating.allowHalf", "Allow Half Steps", false, Group.Value, Exposure.Advanced));
                    properties.Add(Bool("rating.readOnly", "Read Only", false, Group.Interaction, Exposure.Advanced));
                    break;

                case "Carousel":
                case "ScrollSnap":
                case "ImageGallery":
                    properties.Add(Bool("carousel.loop", "Loop", true, Group.Behavior, Exposure.Advanced));
                    properties.Add(Float("carousel.autoAdvance", "Auto Advance (s)", 0f, Group.Behavior, Exposure.Advanced, 0f, 30f,
                        "0 disables automatic paging."));
                    properties.Add(Bool("carousel.showIndicators", "Show Page Indicators", true, Group.Appearance, Exposure.Advanced));
                    break;

                case "CooldownIcon":
                case "SkillSlot":
                    properties.Add(Float("cooldown.duration", "Cooldown (s)", 5f, Group.Value, min: 0f, max: 300f));
                    properties.Add(Int("cooldown.charges", "Charges", 1, Group.Value, Exposure.Advanced, 1f, 10f));
                    properties.Add(Bool("cooldown.showSweep", "Radial Sweep", true, Group.Appearance, Exposure.Advanced));
                    properties.Add(Bool("cooldown.showKeyHint", "Show Key Hint", true, Group.Appearance, Exposure.Advanced));
                    break;

                case "Slot":
                case "EquipmentSlot":
                case "CraftingSlot":
                    properties.Add(Bool("slot.acceptsDrop", "Accepts Drop", true, Group.Interaction));
                    properties.Add(Text("slot.acceptedTypes", "Accepted Item Types", "", Group.Interaction, Exposure.Advanced,
                        "Comma-separated item categories this slot accepts."));
                    properties.Add(Bool("slot.showCount", "Show Stack Count", true, Group.Appearance, Exposure.Advanced));
                    properties.Add(Bool("slot.showRarity", "Show Rarity Frame", true, Group.Appearance, Exposure.Advanced));
                    break;

                case "HealthBar":
                case "BossHealthBar":
                case "StatBar":
                    properties.Add(Bool("health.damageTrail", "Damage Delay Trail", true, Group.Behavior, Exposure.Advanced,
                        "Show a fading trail behind the fill when the value drops."));
                    properties.Add(Float("health.lowThreshold", "Low Threshold", 0.25f, Group.Appearance, Exposure.Advanced, 0f, 1f,
                        "Fraction under which the bar switches to its low-health colour."));
                    break;

                case "Minimap":
                case "Radar":
                    properties.Add(Float("map.zoom", "Zoom", 1f, Group.Value, min: 0.1f, max: 8f));
                    properties.Add(Bool("map.rotateWithPlayer", "Rotate With Player", true, Group.Behavior, Exposure.Advanced));
                    properties.Add(Bool("map.clampMarkers", "Clamp Markers To Edge", true, Group.Behavior, Exposure.Advanced));
                    properties.Add(Float("map.range", "Range", 100f, Group.Value, Exposure.Advanced, 1f, 5000f));
                    break;
            }
        }
    }
}

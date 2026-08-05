using System;
using System.Collections.Generic;
using emiteat.NexUI.Integrations.UGUI;
using emiteat.NexUI.Integrations.UIToolkit;

namespace emiteat.NexUI.Designer.Editor.Components
{
    /// <summary>
    /// NexUI's own base components: the pieces Unity does not ship, available on both backends.
    /// </summary>
    /// <remarks>
    /// Each entry names a real runtime type on each backend - a MonoBehaviour for uGUI, a
    /// VisualElement for UI Toolkit - so attaching one in the Designer produces something that
    /// actually runs, not a marker the project still has to implement. Property schemas are reflected
    /// from those types, so a field added to the runtime component shows up here automatically.
    ///
    /// Where UI Toolkit already covers a capability natively (border-radius, flex-wrap), the entry is
    /// uGUI-only on purpose: shipping a redundant NexUI wrapper would be worse than using the platform.
    /// </remarks>
    internal static class NexUIBaseComponentCatalog
    {
        public static IEnumerable<DesignerUIComponentType> Build()
        {
            // ---- Graphics -----------------------------------------------------------------
            yield return Make("NX.RoundedRect", "Rounded Rect", DesignerUIComponentCategory.Graphic,
                "Rounded rectangle with per-corner radius and an optional border - no sprite needed.",
                ugui: typeof(NXRoundedRect), renderer: true,
                conflicts: new[] { "UGUI.Image", "UGUI.RawImage", "UGUI.Text", "UGUI.TextMeshProUGUI", "NX.Gradient" },
                uitkNote: "UI Toolkit does rounded corners in USS, so no separate element is needed there.");

            yield return Make("NX.Gradient", "Gradient", DesignerUIComponentCategory.Graphic,
                "Linear or four-corner gradient fill. uGUI has vertex colours but no gradient authoring.",
                ugui: typeof(NXGradient), uitk: typeof(NXGradientElement));

            yield return Make("NX.SoftShadow", "Soft Shadow", DesignerUIComponentCategory.Effect,
                "Blurred drop shadow. uGUI's own Shadow draws one hard copy.",
                ugui: typeof(NXSoftShadow), allowMultiple: true,
                uitkNote: "Use a USS box-shadow style on UI Toolkit screens.");

            yield return Make("NX.SegmentedBar", "Segmented Bar", DesignerUIComponentCategory.Game,
                "Bar split into discrete chunks - health pips, ammo, shields.",
                ugui: typeof(NXSegmentedBar), uitk: typeof(NXSegmentedBarElement), renderer: true);

            yield return Make("NX.CooldownOverlay", "Cooldown Overlay", DesignerUIComponentCategory.Game,
                "Radial cooldown sweep over an icon, without a second filled Image child.",
                ugui: typeof(NXCooldownOverlay), uitk: typeof(NXCooldownElement));

            // ---- Layout --------------------------------------------------------------------
            yield return Make("NX.SafeArea", "Safe Area", DesignerUIComponentCategory.Layout,
                "Insets the element by the device safe area (notch, home indicator).",
                ugui: typeof(NXSafeArea), uitk: typeof(NXSafeAreaElement));

            yield return Make("NX.FlowLayout", "Flow Layout", DesignerUIComponentCategory.Layout,
                "Row layout that wraps onto the next line - what uGUI's layout groups cannot do.",
                ugui: typeof(NXFlowLayoutGroup),
                conflicts: new[] { "UGUI.HorizontalLayoutGroup", "UGUI.VerticalLayoutGroup", "UGUI.GridLayoutGroup", "NX.RadialLayout" },
                uitkNote: "UI Toolkit wraps natively with flex-wrap.");

            yield return Make("NX.RadialLayout", "Radial Layout", DesignerUIComponentCategory.Layout,
                "Places children around a circle or arc - radial menus, ability wheels.",
                ugui: typeof(NXRadialLayoutGroup), uitk: typeof(NXRadialContainer),
                conflicts: new[] { "UGUI.HorizontalLayoutGroup", "UGUI.VerticalLayoutGroup", "UGUI.GridLayoutGroup", "NX.FlowLayout" });

            yield return Make("NX.AutoGrid", "Auto Grid", DesignerUIComponentCategory.Layout,
                "Grid that keeps a column count and derives cell size from the available width.",
                ugui: typeof(NXAutoGridLayout),
                conflicts: new[] { "UGUI.HorizontalLayoutGroup", "UGUI.VerticalLayoutGroup", "UGUI.GridLayoutGroup" },
                uitkNote: "UI Toolkit approximates this with flex-wrap and percentage widths.");

            // ---- Text ----------------------------------------------------------------------
            yield return Make("NX.MarqueeText", "Marquee Text", DesignerUIComponentCategory.Text,
                "Scrolls text that is longer than its box instead of clipping it.",
                ugui: typeof(NXMarqueeText), uitk: typeof(NXMarqueeLabel));

            yield return Make("NX.TypewriterText", "Typewriter Text", DesignerUIComponentCategory.Text,
                "Reveals text character by character, with punctuation pauses and a skip.",
                ugui: typeof(NXTypewriterText), uitk: typeof(NXTypewriterLabel));

            yield return Make("NX.NumberTicker", "Number Ticker", DesignerUIComponentCategory.Text,
                "Counts toward the new value instead of snapping - score, currency, XP.",
                ugui: typeof(NXNumberTicker), uitk: typeof(NXNumberTickerLabel));

            // ---- Interaction ----------------------------------------------------------------
            yield return Make("NX.HoldButton", "Hold Button", DesignerUIComponentCategory.Interaction,
                "Fires only after the press is held, reporting progress meanwhile.",
                // Both backends name this type the same; qualify so the catalog stays unambiguous.
                ugui: typeof(emiteat.NexUI.Integrations.UGUI.NXHoldButton),
                uitk: typeof(emiteat.NexUI.Integrations.UIToolkit.NXHoldButton),
                conflicts: new[] { "UGUI.Button" });

            yield return Make("NX.SwipeArea", "Swipe Area", DesignerUIComponentCategory.Interaction,
                "Reports swipe direction and distance over a region.",
                ugui: typeof(NXSwipeArea), uitk: typeof(NXSwipeManipulator));

            // ---- Data ------------------------------------------------------------------------
            yield return Make("NX.VirtualList", "Virtual List", DesignerUIComponentCategory.Data,
                "Builds views only for visible rows and recycles them. uGUI has no ListView equivalent.",
                ugui: typeof(NXVirtualList), requires: new[] { "UGUI.ScrollRect" },
                uitkNote: "UI Toolkit's ListView already virtualizes, so use that on a UI Toolkit screen.");

            yield return Make("NX.Carousel", "Carousel", DesignerUIComponentCategory.Data,
                "Paged scrolling with snapping, looping and auto-advance.",
                ugui: typeof(NXCarousel), requires: new[] { "UGUI.ScrollRect" },
                uitkNote: "UI Toolkit has no paging either; a ScrollView plus this pattern is the equivalent.");

            yield return Make("NX.TabGroup", "Tab Group", DesignerUIComponentCategory.Data,
                "Switches which page is visible when its tab is selected.",
                ugui: typeof(NXTabGroup),
                uitkNote: "UI Toolkit's TabView covers this natively.");

            yield return Make("NX.TooltipTrigger", "Tooltip Trigger", DesignerUIComponentCategory.Interaction,
                "Show/hide a tooltip with proper delays. Unity ships no runtime tooltip system.",
                ugui: typeof(NXTooltipTrigger),
                uitkNote: "UI Toolkit has a basic tooltip attribute; richer tooltips still need this pattern.");

            // ---- Feedback --------------------------------------------------------------------
            yield return Make("NX.RadialFill", "Radial Fill", DesignerUIComponentCategory.Game,
                "Ring that fills by value - cast bars, charge meters. A filled Image needs a ring sprite per size.",
                ugui: typeof(NXRadialFill), renderer: true,
                conflicts: new[] { "UGUI.Image", "UGUI.RawImage", "NX.RoundedRect" });

            yield return Make("NX.Spinner", "Spinner", DesignerUIComponentCategory.Game,
                "Indeterminate loading indicator. Runs on unscaled time so it keeps moving while paused.",
                ugui: typeof(NXSpinner));

            yield return Make("NX.Skeleton", "Skeleton", DesignerUIComponentCategory.Data,
                "Loading placeholder that swaps itself for the real content, with an optional shimmer.",
                ugui: typeof(NXSkeleton));

            yield return Make("NX.Toast", "Toast", DesignerUIComponentCategory.Interaction,
                "Transient message with severity and auto-dismiss. The countdown pauses while hovered.",
                ugui: typeof(NXToast));

            yield return Make("NX.Modal", "Modal", DesignerUIComponentCategory.Interaction,
                "Reports that the player asked to leave rather than closing itself, so a confirmation can intervene.",
                ugui: typeof(NXModal));

            yield return Make("NX.Popover", "Popover", DesignerUIComponentCategory.Interaction,
                "Panel anchored to another element, flipping sides when it would leave the canvas.",
                ugui: typeof(NXPopover));

            yield return Make("NX.TooltipPanel", "Tooltip Panel", DesignerUIComponentCategory.Interaction,
                "The panel a Tooltip Trigger shows. One panel serves many triggers.",
                ugui: typeof(NXTooltipPanel));

            yield return Make("NX.Slot", "Slot", DesignerUIComponentCategory.Layout,
                "Named mount point a reusable component leaves for its caller's content.",
                ugui: typeof(NXSlot));

            yield return Make("NX.ChoiceList", "Choice List", DesignerUIComponentCategory.Data,
                "Single or multiple selection from a list of options. uGUI's ToggleGroup enforces exactly one.",
                ugui: typeof(NXChoiceList));
        }

        private static DesignerUIComponentType Make(string typeId, string displayName,
            DesignerUIComponentCategory category, string description,
            Type ugui = null, Type uitk = null, bool renderer = false, bool allowMultiple = false,
            string[] conflicts = null, string[] requires = null, string uitkNote = null)
        {
            var component = new DesignerUIComponentType
            {
                TypeId = typeId,
                DisplayName = displayName,
                LocalizationKey = "component.type.nx." + char.ToLowerInvariant(typeId[3]) + typeId.Substring(4),
                Description = uitkNote == null ? description : description + "\n" + uitkNote,
                Family = DesignerUIComponentFamily.NexUIBase,
                Category = category,
                BackingType = ugui,
                IsRenderer = renderer,
                AllowMultiple = allowMultiple,
                ConflictsWith = conflicts ?? Array.Empty<string>(),
                RequiredComponents = requires ?? Array.Empty<string>()
            };

            // The schema comes from whichever runtime type exists; the uGUI MonoBehaviour is the
            // reference implementation, and its fields are what both backends expose.
            component.Properties.AddRange(DesignerReflectedSchema.Build(ugui ?? uitk));
            return component;
        }
    }
}

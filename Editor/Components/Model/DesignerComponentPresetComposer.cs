using System.Collections.Generic;

namespace emiteat.NexUI.Designer.Editor.Components
{
    /// <summary>
    /// Turns a palette entry into the components it is made of - the same way Unity's
    /// <c>GameObject &gt; UI &gt; Slider</c> is a GameObject carrying an Image, a Slider and children,
    /// not a "Slider type".
    /// </summary>
    /// <remarks>
    /// This is what makes the built-in library honest: nothing in the palette is a special case the
    /// user cannot take apart. Everything a preset stamps is marked <c>fromPreset</c> purely as a
    /// label, and "decompose" clears the preset name from the element so it stops pretending to be
    /// anything other than the components it holds.
    ///
    /// The table starts with the types people place most; anything not listed falls back to a
    /// composition derived from the descriptor's own shape, so every one of the palette's entries
    /// produces real components from day one.
    /// </remarks>
    public static class DesignerComponentPresetComposer
    {
        /// <summary>Hand-tuned compositions for the common palette entries, by NexUI type id.</summary>
        private static readonly Dictionary<string, string[]> UGUIPresets = new Dictionary<string, string[]>
        {
            { "Panel", new[] { "UGUI.Image" } },
            { "Container", new[] { "UGUI.RectMask2D" } },
            { "Card", new[] { "NX.RoundedRect", "NX.SoftShadow" } },
            { "Label", new[] { "UGUI.TextMeshProUGUI" } },
            { "Heading", new[] { "UGUI.TextMeshProUGUI" } },
            { "Image", new[] { "UGUI.Image" } },
            { "Icon", new[] { "UGUI.Image" } },
            { "Button", new[] { "UGUI.Image", "UGUI.Button" } },
            { "IconButton", new[] { "UGUI.Image", "UGUI.Button" } },
            { "HoldButton", new[] { "UGUI.Image", "NX.HoldButton" } },
            { "Checkbox", new[] { "UGUI.Image", "UGUI.Toggle" } },
            { "Switch", new[] { "NX.RoundedRect", "UGUI.Toggle" } },
            { "Slider", new[] { "UGUI.Slider" } },
            { "VolumeSlider", new[] { "UGUI.Slider" } },
            { "Dropdown", new[] { "UGUI.Image", "UGUI.TMP_Dropdown" } },
            { "TextField", new[] { "UGUI.Image", "UGUI.TMP_InputField" } },
            { "SearchField", new[] { "NX.RoundedRect", "UGUI.TMP_InputField" } },
            { "TextArea", new[] { "UGUI.Image", "UGUI.TMP_InputField" } },
            { "ScrollArea", new[] { "UGUI.ScrollRect", "UGUI.RectMask2D" } },
            { "List", new[] { "UGUI.ScrollRect", "UGUI.RectMask2D", "UGUI.VerticalLayoutGroup" } },
            { "Grid", new[] { "UGUI.ScrollRect", "UGUI.RectMask2D", "NX.AutoGrid" } },
            { "Toolbar", new[] { "UGUI.HorizontalLayoutGroup" } },
            { "FlowContainer", new[] { "NX.FlowLayout" } },
            { "SafeArea", new[] { "NX.SafeArea" } },
            { "RadialMenu", new[] { "NX.RadialLayout" } },
            { "Modal", new[] { "UGUI.Image", "UGUI.CanvasGroup", "NX.Modal" } },
            { "Popover", new[] { "NX.RoundedRect", "NX.Popover" } },
            { "Toast", new[] { "NX.RoundedRect", "NX.Toast" } },
            { "Spinner", new[] { "NX.RadialFill", "NX.Spinner" } },
            { "RadialFill", new[] { "NX.RadialFill" } },
            { "Skeleton", new[] { "NX.RoundedRect", "NX.Skeleton" } },
            { "Slot", new[] { "NX.Slot" } },
            { "ChoiceList", new[] { "UGUI.VerticalLayoutGroup", "NX.ChoiceList" } },
            { "Drawer", new[] { "UGUI.Image", "UGUI.CanvasGroup" } },
            { "LoadingOverlay", new[] { "UGUI.Image", "UGUI.CanvasGroup" } },
            { "ProgressBar", new[] { "UGUI.Image" } },
            { "HealthBar", new[] { "NX.SegmentedBar" } },
            { "StatBar", new[] { "NX.SegmentedBar" } },
            { "HealthPips", new[] { "NX.SegmentedBar" } },
            { "AmmoPips", new[] { "NX.SegmentedBar" } },
            { "CooldownIcon", new[] { "UGUI.Image", "NX.CooldownOverlay" } },
            { "SkillSlot", new[] { "UGUI.Image", "NX.CooldownOverlay" } },
            { "Marquee", new[] { "UGUI.TextMeshProUGUI", "NX.MarqueeText" } },
            { "TypewriterText", new[] { "UGUI.TextMeshProUGUI", "NX.TypewriterText" } },
            { "NumberTicker", new[] { "UGUI.TextMeshProUGUI", "NX.NumberTicker" } },
            { "CurrencyText", new[] { "UGUI.TextMeshProUGUI", "NX.NumberTicker" } },
            { "SwipeArea", new[] { "NX.SwipeArea" } },
            { "Tooltip", new[] { "NX.RoundedRect", "UGUI.TextMeshProUGUI", "NX.TooltipPanel" } },
        };

        /// <summary>Composition for a palette entry on the given backend. Never empty.</summary>
        public static IReadOnlyList<string> Compose(string paletteTypeId, DesignerUIComponentFamily backend)
        {
            var result = new List<string> { DesignerElementComponentAccess.CoreElement };

            if (backend == DesignerUIComponentFamily.UIToolkit)
            {
                result.AddRange(ComposeUIToolkit(paletteTypeId));
                return result;
            }

            if (UGUIPresets.TryGetValue(paletteTypeId, out var preset))
            {
                result.AddRange(preset);
                return result;
            }

            result.AddRange(ComposeFromShape(paletteTypeId));
            return result;
        }

        /// <summary>
        /// On UI Toolkit the control is the element, so a preset resolves to the matching stock control
        /// plus any NexUI base component that adds something the control lacks.
        /// </summary>
        private static IEnumerable<string> ComposeUIToolkit(string paletteTypeId)
        {
            var descriptor = DesignerComponentRegistry.Get(paletteTypeId);
            var tag = descriptor.UxmlTag;
            if (!string.IsNullOrEmpty(tag))
            {
                var control = tag.Substring(tag.IndexOf(':') + 1);
                var typeId = "UITK." + control;
                if (DesignerUIComponentRegistry.IsRegistered(typeId))
                    yield return typeId;
            }

            switch (paletteTypeId)
            {
                case "Marquee": yield return "NX.MarqueeText"; break;
                case "TypewriterText": yield return "NX.TypewriterText"; break;
                case "NumberTicker":
                case "CurrencyText": yield return "NX.NumberTicker"; break;
                case "HealthBar":
                case "StatBar":
                case "HealthPips":
                case "AmmoPips": yield return "NX.SegmentedBar"; break;
                case "CooldownIcon":
                case "SkillSlot": yield return "NX.CooldownOverlay"; break;
                case "SafeArea": yield return "NX.SafeArea"; break;
                case "RadialMenu": yield return "NX.RadialLayout"; break;
                case "SwipeArea": yield return "NX.SwipeArea"; break;
            }
        }

        /// <summary>
        /// Fallback for palette entries without a hand-tuned recipe: derive a sensible uGUI composition
        /// from what the descriptor says the component is.
        /// </summary>
        private static IEnumerable<string> ComposeFromShape(string paletteTypeId)
        {
            var descriptor = DesignerComponentRegistry.Get(paletteTypeId);

            if (descriptor.Category == DesignerComponentCategory.Text)
            {
                yield return "UGUI.TextMeshProUGUI";
                yield break;
            }

            if (descriptor.Category == DesignerComponentCategory.Media)
            {
                yield return "UGUI.Image";
                yield break;
            }

            // Everything else gets a background it can be seen and clicked through, plus the pieces its
            // shape implies.
            yield return descriptor.DefaultShape == DesignerElementShape.Rectangle ? "UGUI.Image" : "NX.RoundedRect";

            if (descriptor.IsValueComponent) yield return "NX.SegmentedBar";
            if (descriptor.IsCollectionComponent) yield return "UGUI.RectMask2D";
            if (descriptor.IsInteractive) yield return "UGUI.Button";
            if (descriptor.IsOverlayComponent) yield return "UGUI.CanvasGroup";
        }

        /// <summary>
        /// Stamps the preset onto a freshly created element. Components are marked as preset-authored
        /// only so the Inspector can say where they came from - they are removable like any other.
        /// </summary>
        public static void Stamp(DesignerElementMetadata element, string paletteTypeId, DesignerUIComponentFamily backend)
        {
            if (element == null) return;
            element.components ??= new List<DesignerElementComponent>();
            element.components.Clear();

            foreach (var typeId in Compose(paletteTypeId, backend))
            {
                if (typeId == DesignerElementComponentAccess.CoreElement)
                {
                    element.components.Add(new DesignerElementComponent(typeId, fromPreset: true));
                    continue;
                }
                DesignerElementComponentAccess.Attach(element, typeId, backend, fromPreset: true);
            }
        }
    }
}

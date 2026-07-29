using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Components.Preview
{
    /// <summary>
    /// Canvas previews for the control-shaped component types - NexUI's extended library plus
    /// Unity's stock uGUI / UI Toolkit controls. Everything drawn here is a Virtual Preview Part: it
    /// visualizes the control on the Designer canvas and is never stored as an authored element.
    ///
    /// Renderers are deliberately shared across families (a NexUI Checkbox, a <c>UGUI.Toggle</c> and a
    /// <c>UITK.Toggle</c> all read as a checkbox), because the canvas shows intent - the backend
    /// serializers are what make each family produce its own real control.
    /// </summary>
    internal static class DesignerStockPreviewRenderers
    {
        public static void Register(Dictionary<string, IUIDesignerComponentPreviewRenderer> byId)
        {
            var checkbox = new CheckboxPreviewRenderer();
            var slider = new SliderPreviewRenderer(range: false);
            var range = new SliderPreviewRenderer(range: true);
            var dropdown = new DropdownPreviewRenderer();
            var field = new InputFieldPreviewRenderer();
            var tabs = new TabStripPreviewRenderer();
            var table = new TablePreviewRenderer();
            var tree = new TreePreviewRenderer();
            var rows = new CollectionPreviewRenderer(grid: false);
            var scroll = new ScrollAreaPreviewRenderer();
            var stepper = new StepperPreviewRenderer();
            var accordion = new AccordionPreviewRenderer();
            var iconRow = new IconRowPreviewRenderer();
            var linear = new LinearFillPreviewRenderer();

            // ---- NexUI extended library ------------------------------------------------
            byId["Checkbox"] = checkbox;
            byId["Switch"] = new SwitchPreviewRenderer();
            byId["RadioGroup"] = new ChoiceListPreviewRenderer();
            byId["SegmentedControl"] = tabs;
            byId["Dropdown"] = dropdown;
            byId["Slider"] = slider;
            byId["RangeSlider"] = range;
            byId["Stepper"] = stepper;
            byId["TextField"] = field;
            byId["TextArea"] = field;
            byId["SearchField"] = field;
            byId["Rating"] = new RatingPreviewRenderer();
            byId["Tabs"] = tabs;
            byId["AppBar"] = new AppBarPreviewRenderer();
            byId["SideNav"] = rows;
            byId["Breadcrumb"] = new BreadcrumbPreviewRenderer();
            byId["Pagination"] = stepper;
            byId["Menu"] = rows;
            byId["ContextMenu"] = rows;
            byId["StepIndicator"] = new StepIndicatorPreviewRenderer();
            byId["Alert"] = new AlertPreviewRenderer();
            byId["EmptyState"] = new EmptyStatePreviewRenderer();
            byId["ScrollArea"] = scroll;
            byId["Splitter"] = new SplitterPreviewRenderer();
            byId["Accordion"] = accordion;
            byId["Table"] = table;
            byId["TreeView"] = tree;
            byId["Carousel"] = new CarouselPreviewRenderer();
            byId["Avatar"] = new AvatarPreviewRenderer();
            byId["Divider"] = new DividerPreviewRenderer();
            byId["Icon"] = new ImagePreviewRenderer(fullBleed: false);
            byId["Minimap"] = new MinimapPreviewRenderer();
            byId["Compass"] = new CompassPreviewRenderer();
            byId["Crosshair"] = new CrosshairPreviewRenderer();
            byId["QuestTracker"] = rows;
            byId["ChatPanel"] = rows;
            byId["Leaderboard"] = table;
            byId["BuffBar"] = iconRow;
            byId["CooldownIcon"] = new RadialPreviewRenderer(spin: false);
            byId["KeyPrompt"] = new KeyPromptPreviewRenderer();
            byId["LoadingOverlay"] = new RadialPreviewRenderer(spin: true);
            byId["CurrencyDisplay"] = new ImagePreviewRenderer(fullBleed: false);
            byId["DialogueBox"] = new SkeletonPreviewRenderer();

            // ---- Unity uGUI stock controls ----------------------------------------------
            byId["UGUI.Image"] = new ImagePreviewRenderer(fullBleed: true);
            byId["UGUI.RawImage"] = new ImagePreviewRenderer(fullBleed: true);
            byId["UGUI.Toggle"] = checkbox;
            byId["UGUI.ToggleGroup"] = new ChoiceListPreviewRenderer();
            byId["UGUI.Slider"] = slider;
            byId["UGUI.Scrollbar"] = new ScrollbarPreviewRenderer();
            byId["UGUI.Dropdown"] = dropdown;
            byId["UGUI.DropdownTMP"] = dropdown;
            byId["UGUI.InputField"] = field;
            byId["UGUI.InputFieldTMP"] = field;
            byId["UGUI.ScrollView"] = scroll;
            byId["UGUI.HorizontalLayoutGroup"] = iconRow;
            byId["UGUI.VerticalLayoutGroup"] = rows;
            byId["UGUI.GridLayoutGroup"] = new CollectionPreviewRenderer(grid: true);

            // ---- Unity UI Toolkit stock controls ------------------------------------------
            byId["UITK.Image"] = new ImagePreviewRenderer(fullBleed: true);
            byId["UITK.Toggle"] = checkbox;
            byId["UITK.RadioButton"] = checkbox;
            byId["UITK.RadioButtonGroup"] = new ChoiceListPreviewRenderer();
            byId["UITK.Slider"] = slider;
            byId["UITK.SliderInt"] = slider;
            byId["UITK.MinMaxSlider"] = range;
            byId["UITK.ProgressBar"] = linear;
            byId["UITK.DropdownField"] = dropdown;
            byId["UITK.EnumField"] = dropdown;
            byId["UITK.TextField"] = field;
            byId["UITK.IntegerField"] = field;
            byId["UITK.FloatField"] = field;
            byId["UITK.Vector2Field"] = field;
            byId["UITK.Vector3Field"] = field;
            byId["UITK.Vector4Field"] = field;
            byId["UITK.RectField"] = field;
            byId["UITK.ObjectField"] = field;
            byId["UITK.ScrollView"] = scroll;
            byId["UITK.ListView"] = rows;
            byId["UITK.MultiColumnListView"] = table;
            byId["UITK.TreeView"] = tree;
            byId["UITK.Foldout"] = accordion;
            byId["UITK.TabView"] = tabs;
            byId["UITK.TwoPaneSplitView"] = new SplitterPreviewRenderer();
            byId["UITK.HelpBox"] = new AlertPreviewRenderer();
        }

        // ---- Shared building blocks ---------------------------------------------------------

        internal static VisualElement Box(Color color, float radius = 3f)
        {
            var box = new VisualElement();
            box.style.backgroundColor = new StyleColor(color);
            box.style.borderTopLeftRadius = radius; box.style.borderTopRightRadius = radius;
            box.style.borderBottomLeftRadius = radius; box.style.borderBottomRightRadius = radius;
            box.pickingMode = PickingMode.Ignore;
            return box;
        }

        internal static Label Text(string value, in DesignerPreviewContext ctx, float size = 11f, float opacity = 0.85f)
        {
            var label = new Label(value)
            {
                style =
                {
                    fontSize = Mathf.Max(8f, size * ctx.Zoom),
                    color = new StyleColor(Color.white),
                    opacity = opacity,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        internal static VisualElement Row(float padding = 6f)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexGrow = 1,
                    paddingLeft = padding,
                    paddingRight = padding
                }
            };
            row.pickingMode = PickingMode.Ignore;
            return row;
        }
    }

    /// <summary>Checkbox / radio / uGUI Toggle: a check box followed by the element's label text.</summary>
    public sealed class CheckboxPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var row = DesignerStockPreviewRenderers.Row();
            var checkedOn = DesignerComponentPropertyAccess.IsOverridden(ctx.Element, "toggle.isOn")
                ? DesignerComponentPropertyAccess.GetBool(ctx.Element, "toggle.isOn")
                : ctx.State == DesignerComponentState.Selected || ctx.Element.previewValue >= 50f;
            var indeterminate = ctx.State == DesignerComponentState.Indeterminate;
            var radio = ctx.Element.elementType == "UITK.RadioButton";

            var box = DesignerStockPreviewRenderers.Box(
                checkedOn || indeterminate ? DesignerPreviewColors.Accent : DesignerPreviewColors.Lighten(ctx.Tint, 0.2f),
                radio ? 8f : 3f);
            box.style.width = 16; box.style.height = 16; box.style.marginRight = 6;
            box.style.flexShrink = 0;
            box.style.alignItems = Align.Center;
            box.style.justifyContent = Justify.Center;
            DesignerPreviewPartUtility.Register(box, ctx, "background");
            if (checkedOn && !radio)
                box.Add(DesignerPreviewPartUtility.Register(DesignerStockPreviewRenderers.Text("✓", ctx, 10f, 1f), ctx, "checkmark"));
            if (indeterminate)
                box.Add(DesignerPreviewPartUtility.Register(DesignerStockPreviewRenderers.Text("–", ctx, 10f, 1f), ctx, "checkmark"));
            row.Add(box);

            if (!string.IsNullOrEmpty(ctx.Element.text))
                row.Add(DesignerPreviewPartUtility.Register(DesignerStockPreviewRenderers.Text(ctx.Element.text, ctx), ctx, "label"));
            view.Add(row);
        }
    }

    /// <summary>Switch: pill track with the knob on the side matching the on/off preview state.</summary>
    public sealed class SwitchPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var on = DesignerComponentPropertyAccess.IsOverridden(ctx.Element, "toggle.isOn")
                ? DesignerComponentPropertyAccess.GetBool(ctx.Element, "toggle.isOn")
                : ctx.State == DesignerComponentState.Selected || ctx.Element.previewValue >= 50f;
            var track = DesignerStockPreviewRenderers.Box(
                on ? DesignerPreviewColors.Accent : DesignerPreviewColors.Lighten(ctx.Tint, 0.15f), 999f);
            track.style.position = Position.Absolute;
            track.style.left = 4; track.style.right = 4; track.style.top = 4; track.style.bottom = 4;
            track.style.justifyContent = Justify.Center;
            track.style.flexDirection = on ? FlexDirection.RowReverse : FlexDirection.Row;
            track.style.alignItems = Align.Center;

            var knob = DesignerStockPreviewRenderers.Box(Color.white, 999f);
            knob.style.width = 14; knob.style.height = 14;
            knob.style.marginLeft = 3; knob.style.marginRight = 3;
            DesignerPreviewPartUtility.Register(track, ctx, "track");
            track.Add(DesignerPreviewPartUtility.Register(knob, ctx, "handle"));
            view.Add(track);
        }
    }

    /// <summary>Slider / range slider: track, filled span and one or two handles.</summary>
    public sealed class SliderPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        private readonly bool _range;
        public SliderPreviewRenderer(bool range) => _range = range;

        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var minimum = DesignerComponentPropertyAccess.GetFloat(ctx.Element, "value.min", ctx.Element.fill.minValue);
            var maximum = DesignerComponentPropertyAccess.GetFloat(ctx.Element, "value.max", ctx.Element.fill.maxValue);
            if (maximum <= minimum) maximum = minimum + 1f;
            var authoredValue = ctx.Element.previewValue;
            if (DesignerComponentPropertyAccess.GetBool(ctx.Element, "value.wholeNumbers"))
                authoredValue = Mathf.Round(authoredValue);
            var fraction = Mathf.Clamp01(Mathf.InverseLerp(minimum, maximum, authoredValue));
            var low = _range
                ? Mathf.Clamp01(Mathf.InverseLerp(minimum, maximum,
                    DesignerComponentPropertyAccess.GetFloat(ctx.Element, "range.low", authoredValue - (maximum - minimum) * 0.35f)))
                : 0f;
            if (_range)
                fraction = Mathf.Clamp01(Mathf.InverseLerp(minimum, maximum,
                    DesignerComponentPropertyAccess.GetFloat(ctx.Element, "range.high", authoredValue)));

            var direction = DesignerComponentPropertyAccess.GetEnum(ctx.Element, "value.direction");
            var reverse = direction == "RightToLeft" || direction == "TopToBottom";
            if (reverse)
            {
                fraction = 1f - fraction;
                low = 1f - low;
                if (_range && low > fraction) (low, fraction) = (fraction, low);
            }

            var track = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Darken(ctx.Tint, 0.25f), 999f);
            track.style.position = Position.Absolute;
            track.style.left = 6; track.style.right = 6;
            track.style.top = new Length(50, LengthUnit.Percent);
            track.style.height = 4;
            track.style.marginTop = -2;
            DesignerPreviewPartUtility.Register(track, ctx, "track");
            view.Add(track);

            var fill = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Accent, 999f);
            fill.style.position = Position.Absolute;
            fill.style.left = new Length(low * 100f, LengthUnit.Percent);
            fill.style.width = new Length((fraction - low) * 100f, LengthUnit.Percent);
            fill.style.top = 0; fill.style.bottom = 0;
            DesignerPreviewPartUtility.Register(fill, ctx, "fill");
            track.Add(fill);

            AddHandle(view, ctx, fraction);
            if (_range) AddHandle(view, ctx, low);
        }

        private static void AddHandle(VisualElement view, in DesignerPreviewContext ctx, float fraction)
        {
            var handle = DesignerStockPreviewRenderers.Box(Color.white, 999f);
            handle.style.position = Position.Absolute;
            handle.style.width = 12; handle.style.height = 12;
            handle.style.left = new Length(fraction * 100f, LengthUnit.Percent);
            handle.style.marginLeft = -6;
            handle.style.top = new Length(50, LengthUnit.Percent);
            handle.style.marginTop = -6;
            view.Add(DesignerPreviewPartUtility.Register(handle, ctx, "handle"));
        }
    }

    /// <summary>Scrollbar: track with a proportional handle along the element's longer axis.</summary>
    public sealed class ScrollbarPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var vertical = ctx.Element.rect.height > ctx.Element.rect.width;
            var handle = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Lighten(ctx.Tint, 0.35f));
            handle.style.position = Position.Absolute;
            if (vertical)
            {
                handle.style.left = 2; handle.style.right = 2; handle.style.top = 2;
                handle.style.height = new Length(35, LengthUnit.Percent);
            }
            else
            {
                handle.style.top = 2; handle.style.bottom = 2; handle.style.left = 2;
                handle.style.width = new Length(35, LengthUnit.Percent);
            }
            view.Add(DesignerPreviewPartUtility.Register(handle, ctx, "handle"));
        }
    }

    /// <summary>Dropdown / enum field: current value plus a caret on the trailing edge.</summary>
    public sealed class DropdownPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var row = DesignerStockPreviewRenderers.Row();
            row.style.justifyContent = Justify.SpaceBetween;

            var authoredOptions = DesignerComponentPropertyAccess.GetString(ctx.Element, "choice.options");
            var options = !string.IsNullOrWhiteSpace(authoredOptions)
                ? new List<string>(authoredOptions.Split(',')).ConvertAll(option => option.Trim())
                : ctx.Element.previewOptions;
            var selected = DesignerComponentPropertyAccess.GetInt(ctx.Element, "choice.value");
            var caption = !string.IsNullOrEmpty(ctx.Element.text) ? ctx.Element.text
                : options != null && options.Count > 0 ? options[Mathf.Clamp(selected, 0, options.Count - 1)]
                : DesignerComponentPropertyAccess.GetString(ctx.Element, "choice.placeholder", "Option");
            row.Add(DesignerPreviewPartUtility.Register(DesignerStockPreviewRenderers.Text(caption, ctx), ctx, "label"));
            row.Add(DesignerPreviewPartUtility.Register(DesignerStockPreviewRenderers.Text("▾", ctx, 11f, 0.7f), ctx, "arrow"));
            view.Add(row);
        }
    }

    /// <summary>Text/number field: placeholder or value text with a caret and an underline.</summary>
    public sealed class InputFieldPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var focused = ctx.State == DesignerComponentState.Focused;
            var row = DesignerStockPreviewRenderers.Row();
            row.style.alignItems = ctx.Element.rect.height > 60f ? Align.FlexStart : Align.Center;
            row.style.paddingTop = ctx.Element.rect.height > 60f ? 6 : 0;

            var placeholder = DesignerComponentPropertyAccess.GetString(ctx.Element, "input.placeholder", "Enter text...");
            var value = string.IsNullOrEmpty(ctx.Element.text) ? placeholder : ctx.Element.text;
            row.Add(DesignerPreviewPartUtility.Register(
                DesignerStockPreviewRenderers.Text(value, ctx, 11f, string.IsNullOrEmpty(ctx.Element.text) ? 0.45f : 0.9f),
                ctx, string.IsNullOrEmpty(ctx.Element.text) ? "placeholder" : "text"));
            if (focused) row.Add(DesignerStockPreviewRenderers.Text("|", ctx, 11f, 1f));
            view.Add(row);

            var underline = DesignerStockPreviewRenderers.Box(
                ctx.State == DesignerComponentState.Error ? DesignerPreviewColors.Error
                : focused ? DesignerPreviewColors.Accent
                : DesignerPreviewColors.Lighten(ctx.Tint, 0.25f), 0f);
            underline.style.position = Position.Absolute;
            underline.style.left = 4; underline.style.right = 4; underline.style.bottom = 2;
            underline.style.height = focused ? 2 : 1;
            view.Add(underline);
        }
    }

    /// <summary>Tab strip / segmented control: evenly divided segments with the first one active.</summary>
    public sealed class TabStripPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var options = ctx.Element.previewOptions;
            var count = options != null && options.Count > 0 ? Mathf.Min(options.Count, 8)
                : ctx.Element.previewItemCount > 0 ? Mathf.Min(ctx.Element.previewItemCount, 8) : 3;
            var active = DesignerComponentPropertyAccess.IsOverridden(ctx.Element, "tabs.activeIndex")
                ? Mathf.Clamp(DesignerComponentPropertyAccess.GetInt(ctx.Element, "tabs.activeIndex"), 0, count - 1)
                : Mathf.Clamp(Mathf.RoundToInt(ctx.Element.previewValue / 100f * (count - 1)), 0, count - 1);

            var strip = new VisualElement { style = { flexDirection = FlexDirection.Row, height = 28, flexShrink = 0 } };
            strip.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < count; i++)
            {
                var tab = DesignerStockPreviewRenderers.Box(
                    i == active ? DesignerPreviewColors.Accent : DesignerPreviewColors.Lighten(ctx.Tint, 0.10f));
                tab.style.flexGrow = 1;
                tab.style.marginLeft = 2; tab.style.marginRight = 2; tab.style.marginTop = 3;
                tab.style.alignItems = Align.Center;
                tab.style.justifyContent = Justify.Center;
                var label = options != null && i < options.Count ? options[i] : "Tab " + (i + 1);
                tab.Add(DesignerStockPreviewRenderers.Text(label, ctx, 10f, i == active ? 1f : 0.7f));
                strip.Add(tab);
            }
            view.Add(strip);
        }
    }

    /// <summary>Table / multi-column list: a header band over evenly spaced rows.</summary>
    public sealed class TablePreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            if (ctx.State == DesignerComponentState.Empty) { ChoiceListPreviewRenderer.AddEmpty(view, "Empty"); return; }
            if (ctx.State == DesignerComponentState.Loading) { ChoiceListPreviewRenderer.AddEmpty(view, "Loading…"); return; }

            var header = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Lighten(ctx.Tint, 0.22f), 0f);
            header.style.height = 22; header.style.flexShrink = 0;
            header.style.flexDirection = FlexDirection.Row;
            var columnCount = Mathf.Clamp(DesignerComponentPropertyAccess.GetInt(ctx.Element, "table.columns", 3), 1, 8);
            for (int c = 0; c < columnCount; c++)
            {
                var cell = DesignerStockPreviewRenderers.Text(c == 0 ? "Name" : c == 1 ? "Value" : "Column " + (c + 1), ctx, 10f, 0.8f);
                cell.style.flexGrow = 1;
                cell.style.marginLeft = 6;
                header.Add(cell);
            }
            view.Add(header);

            var rows = ctx.Element.previewItemCount > 0 ? Mathf.Min(ctx.Element.previewItemCount, 24) : 5;
            for (int r = 0; r < rows; r++)
            {
                var row = DesignerStockPreviewRenderers.Box(
                    r % 2 == 0 ? DesignerPreviewColors.Lighten(ctx.Tint, 0.06f) : new Color(0f, 0f, 0f, 0f), 0f);
                row.style.height = 20; row.style.flexShrink = 0;
                view.Add(row);
            }
        }
    }

    /// <summary>Tree view: rows indented by depth with disclosure arrows.</summary>
    public sealed class TreePreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            if (ctx.State == DesignerComponentState.Empty) { ChoiceListPreviewRenderer.AddEmpty(view, "Empty"); return; }

            var depths = new[] { 0, 1, 1, 2, 0, 1 };
            var count = ctx.Element.previewItemCount > 0 ? Mathf.Min(ctx.Element.previewItemCount, depths.Length) : depths.Length;
            for (int i = 0; i < count; i++)
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, height = 20 } };
                row.pickingMode = PickingMode.Ignore;
                row.style.paddingLeft = 6 + depths[i] * 12;
                row.Add(DesignerStockPreviewRenderers.Text(depths[i] < 2 ? "▸" : "•", ctx, 9f, 0.6f));
                var bar = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Lighten(ctx.Tint, 0.18f));
                bar.style.height = 8; bar.style.flexGrow = 1; bar.style.marginLeft = 5; bar.style.marginRight = 8;
                row.Add(bar);
                view.Add(row);
            }
        }
    }

    /// <summary>Scroll area / scroll view: content bars plus a scrollbar on the trailing edge.</summary>
    public sealed class ScrollAreaPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var vertical = DesignerComponentPropertyAccess.GetBool(ctx.Element, "scroll.vertical", true);
            var horizontal = DesignerComponentPropertyAccess.GetBool(ctx.Element, "scroll.horizontal");
            var showVerticalBar = vertical && DesignerComponentPropertyAccess.GetEnum(ctx.Element, "scroll.verticalBar") != "Hidden";
            var content = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    flexDirection = horizontal && !vertical ? FlexDirection.Row : FlexDirection.Column,
                    paddingLeft = 6,
                    paddingTop = 6,
                    paddingRight = showVerticalBar ? 14 : 6
                }
            };
            DesignerPreviewPartUtility.Register(content, ctx, "content");
            for (int i = 0; i < 4; i++)
            {
                var bar = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Lighten(ctx.Tint, 0.14f));
                bar.style.height = 14; bar.style.marginBottom = 6;
                if (horizontal && !vertical)
                {
                    bar.style.width = 48; bar.style.marginRight = 6; bar.style.flexShrink = 0;
                }
                else
                    bar.style.width = new Length(i % 2 == 0 ? 92 : 70, LengthUnit.Percent);
                content.Add(bar);
            }
            view.Add(content);

            if (!showVerticalBar) return;
            var scrollbar = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Darken(ctx.Tint, 0.2f), 999f);
            scrollbar.style.position = Position.Absolute;
            scrollbar.style.right = 3; scrollbar.style.top = 4; scrollbar.style.bottom = 4; scrollbar.style.width = 5;
            DesignerPreviewPartUtility.Register(scrollbar, ctx, "vertical-scrollbar");
            var handle = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Lighten(ctx.Tint, 0.4f), 999f);
            handle.style.position = Position.Absolute;
            handle.style.left = 0; handle.style.right = 0; handle.style.top = 0;
            handle.style.height = new Length(45, LengthUnit.Percent);
            scrollbar.Add(handle);
            view.Add(scrollbar);
        }
    }

    /// <summary>Stepper / pagination: decrement and increment affordances around a value.</summary>
    public sealed class StepperPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var row = DesignerStockPreviewRenderers.Row(4f);
            row.style.justifyContent = Justify.SpaceBetween;
            row.Add(Chip("‹", ctx));
            var value = string.IsNullOrEmpty(ctx.Element.text)
                ? Mathf.RoundToInt(ctx.Element.previewValue).ToString()
                : ctx.Element.text;
            row.Add(DesignerStockPreviewRenderers.Text(value, ctx, 11f, 0.95f));
            row.Add(Chip("›", ctx));
            view.Add(row);
        }

        private static VisualElement Chip(string glyph, in DesignerPreviewContext ctx)
        {
            var chip = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Lighten(ctx.Tint, 0.18f));
            chip.style.width = 20; chip.style.height = 20;
            chip.style.alignItems = Align.Center; chip.style.justifyContent = Justify.Center;
            chip.Add(DesignerStockPreviewRenderers.Text(glyph, ctx, 11f, 0.9f));
            return chip;
        }
    }

    /// <summary>Accordion / foldout: a header row with a disclosure arrow over collapsed rows.</summary>
    public sealed class AccordionPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var expanded = ctx.State != DesignerComponentState.Disabled;
            var header = DesignerStockPreviewRenderers.Row(6f);
            header.style.flexGrow = 0;
            header.style.height = 24;
            header.Add(DesignerStockPreviewRenderers.Text(expanded ? "▾" : "▸", ctx, 10f, 0.8f));
            var title = DesignerStockPreviewRenderers.Text(
                string.IsNullOrEmpty(ctx.Element.text) ? "Section" : ctx.Element.text, ctx);
            title.style.marginLeft = 5;
            header.Add(title);
            view.Add(header);

            if (!expanded) return;
            var body = new VisualElement { style = { flexGrow = 1, paddingLeft = 18, paddingRight = 8, paddingTop = 2 } };
            body.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < 2; i++)
            {
                var bar = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Lighten(ctx.Tint, 0.12f));
                bar.style.height = 10; bar.style.marginBottom = 5;
                body.Add(bar);
            }
            view.Add(body);
        }
    }

    /// <summary>Breadcrumb: separated path segments.</summary>
    public sealed class BreadcrumbPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var options = ctx.Element.previewOptions;
            var segments = options != null && options.Count > 0 ? options : new List<string> { "Home", "Section", "Page" };
            var row = DesignerStockPreviewRenderers.Row();
            row.style.justifyContent = Justify.FlexStart;
            for (int i = 0; i < segments.Count && i < 6; i++)
            {
                if (i > 0)
                {
                    var sep = DesignerStockPreviewRenderers.Text("›", ctx, 10f, 0.5f);
                    sep.style.marginLeft = 4; sep.style.marginRight = 4;
                    row.Add(sep);
                }
                row.Add(DesignerStockPreviewRenderers.Text(segments[i], ctx, 10f, i == segments.Count - 1 ? 0.95f : 0.6f));
            }
            view.Add(row);
        }
    }

    /// <summary>App bar: leading action, title, trailing actions.</summary>
    public sealed class AppBarPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var row = DesignerStockPreviewRenderers.Row(10f);
            row.style.justifyContent = Justify.SpaceBetween;

            var leading = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            leading.pickingMode = PickingMode.Ignore;
            leading.Add(DesignerStockPreviewRenderers.Text("‹", ctx, 14f, 0.85f));
            var title = DesignerStockPreviewRenderers.Text(
                string.IsNullOrEmpty(ctx.Element.text) ? "Title" : ctx.Element.text, ctx, 13f);
            title.style.marginLeft = 8;
            leading.Add(title);
            row.Add(leading);

            var actions = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            actions.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < 2; i++)
            {
                var dot = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Lighten(ctx.Tint, 0.3f), 999f);
                dot.style.width = 14; dot.style.height = 14; dot.style.marginLeft = 6;
                actions.Add(dot);
            }
            row.Add(actions);
            view.Add(row);
        }
    }

    /// <summary>Step indicator: numbered step dots joined by a connector line.</summary>
    public sealed class StepIndicatorPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var count = ctx.Element.previewItemCount > 0 ? Mathf.Min(ctx.Element.previewItemCount, 8) : 4;
            var active = Mathf.Clamp(Mathf.RoundToInt(ctx.Element.previewValue / 100f * (count - 1)), 0, count - 1);

            var row = DesignerStockPreviewRenderers.Row();
            row.style.justifyContent = Justify.SpaceBetween;
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    var line = DesignerStockPreviewRenderers.Box(
                        i <= active ? DesignerPreviewColors.Accent : DesignerPreviewColors.Lighten(ctx.Tint, 0.15f), 0f);
                    line.style.height = 2; line.style.flexGrow = 1;
                    line.style.marginLeft = 4; line.style.marginRight = 4;
                    row.Add(line);
                }
                var dot = DesignerStockPreviewRenderers.Box(
                    i <= active ? DesignerPreviewColors.Accent : DesignerPreviewColors.Lighten(ctx.Tint, 0.18f), 999f);
                dot.style.width = 18; dot.style.height = 18; dot.style.flexShrink = 0;
                dot.style.alignItems = Align.Center; dot.style.justifyContent = Justify.Center;
                dot.Add(DesignerStockPreviewRenderers.Text((i + 1).ToString(), ctx, 9f, 1f));
                row.Add(dot);
            }
            view.Add(row);
        }
    }

    /// <summary>Alert / help box: severity stripe, icon and message.</summary>
    public sealed class AlertPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var accent = ctx.State switch
            {
                DesignerComponentState.Error => DesignerPreviewColors.Error,
                DesignerComponentState.Warning => DesignerPreviewColors.Warning,
                DesignerComponentState.Success => DesignerPreviewColors.Success,
                _ => DesignerPreviewColors.Accent
            };

            var stripe = DesignerStockPreviewRenderers.Box(accent, 0f);
            stripe.style.position = Position.Absolute;
            stripe.style.left = 0; stripe.style.top = 0; stripe.style.bottom = 0; stripe.style.width = 3;
            view.Add(stripe);

            var row = DesignerStockPreviewRenderers.Row(10f);
            var icon = DesignerStockPreviewRenderers.Box(accent, 999f);
            icon.style.width = 14; icon.style.height = 14; icon.style.marginRight = 8; icon.style.flexShrink = 0;
            row.Add(icon);
            row.Add(DesignerStockPreviewRenderers.Text(
                string.IsNullOrEmpty(ctx.Element.text) ? "Message" : ctx.Element.text, ctx));
            view.Add(row);
        }
    }

    /// <summary>Empty state: centered glyph, message and a call to action.</summary>
    public sealed class EmptyStatePreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var column = new VisualElement { style = { flexGrow = 1, alignItems = Align.Center, justifyContent = Justify.Center } };
            column.pickingMode = PickingMode.Ignore;

            var glyph = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Lighten(ctx.Tint, 0.16f), 999f);
            glyph.style.width = 40; glyph.style.height = 40; glyph.style.marginBottom = 10;
            column.Add(glyph);
            column.Add(DesignerStockPreviewRenderers.Text(
                string.IsNullOrEmpty(ctx.Element.text) ? "Nothing here yet" : ctx.Element.text, ctx, 12f, 0.7f));
            view.Add(column);
        }
    }

    /// <summary>Splitter: two panes divided by a draggable handle.</summary>
    public sealed class SplitterPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
            row.pickingMode = PickingMode.Ignore;

            var first = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Lighten(ctx.Tint, 0.08f), 0f);
            first.style.width = new Length(38, LengthUnit.Percent);
            row.Add(first);

            var handle = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Lighten(ctx.Tint, 0.3f), 0f);
            handle.style.width = 4;
            row.Add(handle);

            var second = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Lighten(ctx.Tint, 0.04f), 0f);
            second.style.flexGrow = 1;
            row.Add(second);

            view.Add(row);
        }
    }

    /// <summary>Carousel: current page with paging dots along the bottom.</summary>
    public sealed class CarouselPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var page = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Lighten(ctx.Tint, 0.12f));
            page.style.position = Position.Absolute;
            page.style.left = 8; page.style.right = 8; page.style.top = 8; page.style.bottom = 24;
            view.Add(page);

            var count = ctx.Element.previewItemCount > 0 ? Mathf.Min(ctx.Element.previewItemCount, 8) : 4;
            var active = Mathf.Clamp(Mathf.RoundToInt(ctx.Element.previewValue / 100f * (count - 1)), 0, count - 1);
            var dots = new VisualElement
            {
                style =
                {
                    position = Position.Absolute, left = 0, right = 0, bottom = 6,
                    flexDirection = FlexDirection.Row, justifyContent = Justify.Center, alignItems = Align.Center
                }
            };
            dots.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < count; i++)
            {
                var dot = DesignerStockPreviewRenderers.Box(
                    i == active ? DesignerPreviewColors.Accent : DesignerPreviewColors.Lighten(ctx.Tint, 0.3f), 999f);
                dot.style.width = 6; dot.style.height = 6;
                dot.style.marginLeft = 3; dot.style.marginRight = 3;
                dots.Add(dot);
            }
            view.Add(dots);
        }
    }

    /// <summary>Rating: filled and empty stars from the preview value.</summary>
    public sealed class RatingPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var max = ctx.Element.previewItemCount > 0 ? Mathf.Min(ctx.Element.previewItemCount, 10) : 5;
            var filled = Mathf.RoundToInt(Mathf.Clamp01(ctx.Element.previewValue / 100f) * max);
            var row = DesignerStockPreviewRenderers.Row(4f);
            row.style.justifyContent = Justify.FlexStart;
            for (int i = 0; i < max; i++)
            {
                var star = DesignerStockPreviewRenderers.Text(i < filled ? "★" : "☆", ctx, 14f, i < filled ? 1f : 0.45f);
                star.style.marginRight = 2;
                row.Add(star);
            }
            view.Add(row);
        }
    }

    /// <summary>Avatar: round portrait with initials fallback.</summary>
    public sealed class AvatarPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            if (ctx.Element.previewImage != null)
            {
                new ImagePreviewRenderer(fullBleed: true).BuildPreview(view, ctx);
                return;
            }
            var initials = string.IsNullOrEmpty(ctx.Element.text) ? "AB" : ctx.Element.text.Substring(0, Mathf.Min(2, ctx.Element.text.Length));
            var center = new VisualElement { style = { flexGrow = 1, alignItems = Align.Center, justifyContent = Justify.Center } };
            center.pickingMode = PickingMode.Ignore;
            center.Add(DesignerStockPreviewRenderers.Text(initials.ToUpperInvariant(), ctx, 14f, 0.9f));
            view.Add(center);
        }
    }

    /// <summary>Divider: a single hairline centered in the element box.</summary>
    public sealed class DividerPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var vertical = ctx.Element.rect.height > ctx.Element.rect.width;
            var line = DesignerStockPreviewRenderers.Box(new Color(1f, 1f, 1f, 0.35f), 0f);
            line.style.position = Position.Absolute;
            if (vertical)
            {
                line.style.top = 0; line.style.bottom = 0; line.style.width = 1;
                line.style.left = new Length(50, LengthUnit.Percent);
            }
            else
            {
                line.style.left = 0; line.style.right = 0; line.style.height = 1;
                line.style.top = new Length(50, LengthUnit.Percent);
            }
            view.Add(line);
        }
    }

    /// <summary>Row of equally sized icon cells (buff bar, horizontal layout group).</summary>
    public sealed class IconRowPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            if (ctx.State == DesignerComponentState.Empty) { ChoiceListPreviewRenderer.AddEmpty(view, "Empty"); return; }
            var count = ctx.Element.previewItemCount > 0 ? Mathf.Min(ctx.Element.previewItemCount, 12) : 5;
            var row = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexGrow = 1, alignItems = Align.Center, paddingLeft = 4, paddingRight = 4 }
            };
            row.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < count; i++)
            {
                var cell = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Lighten(ctx.Tint, 0.16f));
                cell.style.flexGrow = 1;
                cell.style.height = new Length(70, LengthUnit.Percent);
                cell.style.marginLeft = 2; cell.style.marginRight = 2;
                row.Add(cell);
            }
            view.Add(row);
        }
    }

    /// <summary>Minimap: circular map field with a player arrow and blips.</summary>
    public sealed class MinimapPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var field = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Darken(ctx.Tint, 0.25f), 999f);
            field.style.position = Position.Absolute;
            field.style.left = 4; field.style.right = 4; field.style.top = 4; field.style.bottom = 4;
            view.Add(field);

            var player = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Accent, 999f);
            player.style.position = Position.Absolute;
            player.style.width = 8; player.style.height = 8;
            player.style.left = new Length(50, LengthUnit.Percent);
            player.style.top = new Length(50, LengthUnit.Percent);
            player.style.marginLeft = -4; player.style.marginTop = -4;
            view.Add(player);

            var blips = new[] { new Vector2(0.28f, 0.32f), new Vector2(0.7f, 0.4f), new Vector2(0.6f, 0.72f) };
            foreach (var position in blips)
            {
                var blip = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Warning, 999f);
                blip.style.position = Position.Absolute;
                blip.style.width = 5; blip.style.height = 5;
                blip.style.left = new Length(position.x * 100f, LengthUnit.Percent);
                blip.style.top = new Length(position.y * 100f, LengthUnit.Percent);
                view.Add(blip);
            }
        }
    }

    /// <summary>Compass: cardinal ticks with the heading marker in the center.</summary>
    public sealed class CompassPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var row = DesignerStockPreviewRenderers.Row(8f);
            row.style.justifyContent = Justify.SpaceBetween;
            foreach (var cardinal in new[] { "W", "NW", "N", "NE", "E" })
                row.Add(DesignerStockPreviewRenderers.Text(cardinal, ctx, 10f, cardinal == "N" ? 1f : 0.55f));
            view.Add(row);

            var marker = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Accent, 0f);
            marker.style.position = Position.Absolute;
            marker.style.width = 2; marker.style.top = 0; marker.style.bottom = 0;
            marker.style.left = new Length(50, LengthUnit.Percent);
            view.Add(marker);
        }
    }

    /// <summary>Crosshair: center reticle made of four ticks.</summary>
    public sealed class CrosshairPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var color = ctx.State == DesignerComponentState.Error ? DesignerPreviewColors.Error
                : ctx.State == DesignerComponentState.Success ? DesignerPreviewColors.Success
                : Color.white;
            AddTick(view, color, horizontal: true, leading: true);
            AddTick(view, color, horizontal: true, leading: false);
            AddTick(view, color, horizontal: false, leading: true);
            AddTick(view, color, horizontal: false, leading: false);
        }

        private static void AddTick(VisualElement view, Color color, bool horizontal, bool leading)
        {
            var tick = DesignerStockPreviewRenderers.Box(color, 0f);
            tick.style.position = Position.Absolute;
            if (horizontal)
            {
                tick.style.height = 2; tick.style.width = new Length(22, LengthUnit.Percent);
                tick.style.top = new Length(50, LengthUnit.Percent); tick.style.marginTop = -1;
                if (leading) tick.style.left = 2; else tick.style.right = 2;
            }
            else
            {
                tick.style.width = 2; tick.style.height = new Length(22, LengthUnit.Percent);
                tick.style.left = new Length(50, LengthUnit.Percent); tick.style.marginLeft = -1;
                if (leading) tick.style.top = 2; else tick.style.bottom = 2;
            }
            view.Add(tick);
        }
    }

    /// <summary>Key prompt: a key cap glyph next to its action label.</summary>
    public sealed class KeyPromptPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var row = DesignerStockPreviewRenderers.Row(6f);
            row.style.justifyContent = Justify.FlexStart;

            var cap = DesignerStockPreviewRenderers.Box(DesignerPreviewColors.Lighten(ctx.Tint, 0.35f), 4f);
            cap.style.minWidth = 20; cap.style.height = 20;
            cap.style.alignItems = Align.Center; cap.style.justifyContent = Justify.Center;
            cap.style.paddingLeft = 4; cap.style.paddingRight = 4;
            cap.Add(DesignerStockPreviewRenderers.Text(
                string.IsNullOrEmpty(ctx.Element.text) ? "E" : ctx.Element.text, ctx, 10f, 1f));
            row.Add(cap);
            view.Add(row);
        }
    }
}

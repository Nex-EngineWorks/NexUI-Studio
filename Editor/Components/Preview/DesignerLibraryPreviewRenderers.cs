using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Viewport;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Components.Preview
{
    /// <summary>
    /// Canvas previews for <see cref="NexUILibraryCatalog"/>'s components. Most entries reuse an
    /// existing renderer - a Settings Toggle Row reads like a checkbox row, a Kanban column like a
    /// list - because the canvas needs to show the component's <i>shape</i>, not reimplement it.
    /// Only the shapes nothing existing covers (charts, stat tiles, rows with a leading visual) get
    /// their own renderer here.
    /// </summary>
    internal static class DesignerLibraryPreviewRenderers
    {
        public static void Register(Dictionary<string, IUIDesignerComponentPreviewRenderer> byId)
        {
            var rows = new CollectionPreviewRenderer(grid: false);
            var grid = new CollectionPreviewRenderer(grid: true);
            var field = new InputFieldPreviewRenderer();
            var checkbox = new CheckboxPreviewRenderer();
            var slider = new SliderPreviewRenderer(range: false);
            var dropdown = new DropdownPreviewRenderer();
            var linear = new LinearFillPreviewRenderer();
            var radial = new RadialPreviewRenderer(spin: false);
            var table = new TablePreviewRenderer();
            var tabs = new TabStripPreviewRenderer();
            var scroll = new ScrollAreaPreviewRenderer();
            var stepper = new StepperPreviewRenderer();
            var iconRow = new IconRowPreviewRenderer();
            var image = new ImagePreviewRenderer(fullBleed: true);
            var icon = new ImagePreviewRenderer(fullBleed: false);
            var alert = new AlertPreviewRenderer();
            var empty = new EmptyStatePreviewRenderer();
            var splitter = new SplitterPreviewRenderer();
            var skeleton = new SkeletonPreviewRenderer();
            var listRow = new ListRowPreviewRenderer();
            var statTile = new StatTilePreviewRenderer();
            var bars = new ChartPreviewRenderer(ChartPreviewKind.Bars);
            var line = new ChartPreviewRenderer(ChartPreviewKind.Line);
            var pie = new ChartPreviewRenderer(ChartPreviewKind.Pie);
            var matrix = new ChartPreviewRenderer(ChartPreviewKind.Matrix);
            var points = new ChartPreviewRenderer(ChartPreviewKind.Points);

            // ---- Layout -------------------------------------------------------------------
            byId["FlowContainer"] = iconRow;
            byId["MasonryGrid"] = grid;
            byId["ScrollSnap"] = new CarouselPreviewRenderer();
            byId["SidebarLayout"] = splitter;
            byId["TwoColumn"] = splitter;
            byId["ThreeColumn"] = splitter;
            byId["ResizablePanel"] = splitter;
            byId["PageContainer"] = skeleton;
            byId["Form"] = skeleton;
            byId["FormSection"] = new AccordionPreviewRenderer();
            byId["FieldRow"] = listRow;
            byId["ListItem"] = listRow;
            byId["PanelHeader"] = new AppBarPreviewRenderer();
            byId["StickyHeader"] = new AppBarPreviewRenderer();

            // ---- Media --------------------------------------------------------------------
            foreach (var id in new[]
                     {
                         "VideoView", "RenderTextureView", "CoverImage", "PlaceholderImage",
                         "NineSliceImage", "MaskedImage", "ParallaxLayer", "ModelView", "QRCode"
                     })
                byId[id] = image;
            byId["Thumbnail"] = image;
            byId["PortraitFrame"] = image;
            byId["Logo"] = icon;
            byId["ParticleHost"] = icon;
            byId["ImageGallery"] = new CarouselPreviewRenderer();

            // ---- Controls -----------------------------------------------------------------
            byId["NumberField"] = field;
            byId["PasswordField"] = field;
            byId["KeybindField"] = field;
            byId["VectorField"] = field;
            byId["FilePicker"] = field;
            byId["DatePicker"] = dropdown;
            byId["TimePicker"] = dropdown;
            byId["DateRangePicker"] = dropdown;
            byId["LanguageSelector"] = dropdown;
            byId["Knob"] = radial;
            byId["VolumeSlider"] = slider;
            byId["Scrubber"] = slider;
            byId["CurveField"] = line;
            byId["Calendar"] = grid;
            byId["ColorPicker"] = new ColorAreaPreviewRenderer();
            byId["ColorSwatch"] = new ColorAreaPreviewRenderer();
            byId["GradientPicker"] = new ColorAreaPreviewRenderer();
            byId["ToggleButton"] = checkbox;
            byId["ButtonGroup"] = tabs;
            byId["RadialMenu"] = radial;
            byId["VirtualJoystick"] = new JoystickPreviewRenderer();
            byId["DPad"] = new JoystickPreviewRenderer();
            byId["HoldButton"] = linear;

            // ---- Selection ----------------------------------------------------------------
            byId["MultiSelect"] = new ChoiceListPreviewRenderer();
            byId["CheckboxGroup"] = new ChoiceListPreviewRenderer();
            byId["SelectionList"] = rows;
            byId["ComboBox"] = dropdown;
            byId["SortSelector"] = dropdown;
            byId["Autocomplete"] = rows;
            byId["MentionList"] = rows;
            byId["TagInput"] = iconRow;
            byId["FilterBar"] = iconRow;
            byId["TransferList"] = splitter;
            byId["OptionCard"] = statTile;

            // ---- Navigation ---------------------------------------------------------------
            byId["NavRail"] = rows;
            byId["BottomNav"] = iconRow;
            byId["NavItem"] = listRow;
            byId["MenuItem"] = listRow;
            byId["MenuBar"] = tabs;
            byId["SubMenu"] = rows;
            byId["CommandPalette"] = rows;
            byId["SearchOverlay"] = rows;
            byId["AnchorNav"] = rows;
            byId["QuickAccessBar"] = iconRow;
            byId["Wizard"] = new StepIndicatorPreviewRenderer();
            byId["WizardStep"] = skeleton;

            // ---- Feedback -----------------------------------------------------------------
            byId["LoadingBar"] = linear;
            byId["Meter"] = linear;
            byId["CooldownBar"] = linear;
            byId["ComboMeter"] = linear;
            byId["Gauge"] = radial;
            byId["CountdownTimer"] = radial;
            byId["ActivityIndicator"] = new RadialPreviewRenderer(spin: true);
            byId["SuccessCheck"] = radial;
            byId["Sparkline"] = line;
            byId["ToastStack"] = rows;
            byId["Snackbar"] = alert;
            byId["InlineError"] = alert;
            byId["ConnectionStatus"] = alert;

            // ---- Overlay ------------------------------------------------------------------
            foreach (var id in new[] { "ConfirmDialog", "AlertDialog", "PromptDialog", "CoachMark", "HoverCard" })
                byId[id] = alert;
            byId["BottomSheet"] = skeleton;
            byId["SideSheet"] = skeleton;
            byId["ContextPanel"] = skeleton;
            byId["TutorialOverlay"] = empty;
            byId["Spotlight"] = empty;
            byId["Lightbox"] = image;
            byId["ShareSheet"] = grid;

            // ---- Data ---------------------------------------------------------------------
            byId["TableRow"] = listRow;
            byId["TableHeader"] = tabs;
            byId["PropertyRow"] = listRow;
            byId["MetricRow"] = listRow;
            byId["TreeNode"] = listRow;
            byId["InfiniteList"] = rows;
            byId["Feed"] = rows;
            byId["CommentThread"] = rows;
            byId["TimelineList"] = rows;
            byId["LogView"] = rows;
            byId["KeyValueTable"] = table;
            byId["VirtualGrid"] = grid;
            byId["KanbanBoard"] = splitter;
            byId["KanbanColumn"] = rows;
            byId["StatCard"] = statTile;
            byId["KpiTile"] = statTile;

            // ---- Charts -------------------------------------------------------------------
            byId["BarChart"] = bars;
            byId["StackedBarChart"] = bars;
            byId["Histogram"] = bars;
            byId["FunnelChart"] = bars;
            byId["LineChart"] = line;
            byId["AreaChart"] = line;
            byId["PieChart"] = pie;
            byId["DonutChart"] = pie;
            byId["RadarChart"] = pie;
            byId["GaugeChart"] = radial;
            byId["Heatmap"] = matrix;
            byId["ScatterPlot"] = points;
            byId["ChartLegend"] = rows;

            // ---- Social / Commerce / Settings ----------------------------------------------
            byId["ProfileCard"] = statTile;
            byId["UserRow"] = listRow;
            byId["FriendListItem"] = listRow;
            byId["ReviewCard"] = statTile;
            byId["RatingSummary"] = bars;
            byId["CommentInput"] = field;
            byId["ProductCard"] = statTile;
            byId["ShopItemCard"] = statTile;
            byId["CurrencyPack"] = statTile;
            byId["RewardCard"] = statTile;
            byId["SubscriptionCard"] = statTile;
            byId["CartItem"] = listRow;
            byId["PaymentMethodRow"] = listRow;
            byId["CheckoutSummary"] = table;
            byId["CouponField"] = field;
            byId["SettingsRow"] = listRow;
            byId["SettingsToggleRow"] = checkbox;
            byId["SettingsSliderRow"] = slider;
            byId["VolumeRow"] = slider;
            byId["KeybindRow"] = listRow;
            byId["AccountRow"] = listRow;
            byId["LegalRow"] = listRow;
            byId["AboutRow"] = listRow;
            byId["SettingsSection"] = new AccordionPreviewRenderer();

            // ---- Game HUD -------------------------------------------------------------------
            byId["AbilityBar"] = iconRow;
            byId["SkillSlot"] = new SlotPreviewRenderer();
            byId["EquipmentSlot"] = new SlotPreviewRenderer();
            byId["CraftingSlot"] = new SlotPreviewRenderer();
            byId["SkillTreeNode"] = radial;
            byId["InventoryGrid"] = grid;
            byId["CharacterPortrait"] = image;
            byId["PartyFrame"] = statTile;
            byId["TargetFrame"] = statTile;
            byId["Nameplate"] = statTile;
            byId["CastBar"] = linear;
            byId["BossHealthBar"] = linear;
            byId["XpBar"] = linear;
            byId["ResourceOrb"] = radial;
            byId["Speedometer"] = radial;
            byId["KillFeed"] = rows;
            byId["MapLegend"] = rows;
            byId["QuestCard"] = statTile;
            byId["TradePanel"] = splitter;
            byId["WeaponWheel"] = radial;
            byId["EmoteWheel"] = radial;
            byId["PingWheel"] = radial;
            byId["InteractionPrompt"] = new KeyPromptPreviewRenderer();
            byId["SubtitleBar"] = alert;
            byId["AchievementToast"] = alert;
            byId["LootPopup"] = alert;
            byId["ChatBubble"] = alert;
            byId["Waypoint"] = new CrosshairPreviewRenderer();
            byId["ObjectiveMarker"] = new CrosshairPreviewRenderer();
        }
    }

    internal enum ChartPreviewKind
    {
        Bars,
        Line,
        Pie,
        Matrix,
        Points
    }

    /// <summary>
    /// Placeholder series drawing for the chart components. The shape is deterministic (a fixed
    /// sample series), because a preview that reshuffles on every repaint reads as data rather than
    /// as a placeholder.
    /// </summary>
    public sealed class ChartPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        private static readonly float[] Sample = { 0.45f, 0.72f, 0.38f, 0.90f, 0.60f, 0.78f, 0.32f };
        private readonly ChartPreviewKind _kind;

        internal ChartPreviewRenderer(ChartPreviewKind kind) => _kind = kind;

        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            if (ctx.State == DesignerComponentState.Empty) { ChoiceListPreviewRenderer.AddEmpty(view, "No data"); return; }
            if (ctx.State == DesignerComponentState.Loading) { ChoiceListPreviewRenderer.AddEmpty(view, "Loading…"); return; }

            var plot = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = 10, right = 10, top = 10, bottom = 14,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.FlexEnd,
                    justifyContent = Justify.SpaceBetween
                }
            };
            plot.pickingMode = PickingMode.Ignore;
            view.Add(plot);

            switch (_kind)
            {
                case ChartPreviewKind.Bars: BuildBars(plot, ctx); break;
                case ChartPreviewKind.Line: BuildLine(plot, ctx); break;
                case ChartPreviewKind.Pie: BuildPie(view, ctx); break;
                case ChartPreviewKind.Matrix: BuildMatrix(plot, ctx); break;
                case ChartPreviewKind.Points: BuildPoints(plot, ctx); break;
            }

            if (_kind != ChartPreviewKind.Pie)
            {
                var axis = Bar(DesignerPreviewColors.Lighten(ctx.Tint, 0.3f), 0f);
                axis.style.position = Position.Absolute;
                axis.style.left = 10; axis.style.right = 10; axis.style.bottom = 12; axis.style.height = 1;
                view.Add(axis);
            }
        }

        private static void BuildBars(VisualElement plot, in DesignerPreviewContext ctx)
        {
            foreach (var value in Sample)
            {
                var bar = Bar(DesignerPreviewColors.Accent, 2f);
                bar.style.flexGrow = 1;
                bar.style.marginLeft = 2; bar.style.marginRight = 2;
                bar.style.height = new Length(value * 100f, LengthUnit.Percent);
                plot.Add(bar);
            }
        }

        /// <summary>Line/area series approximated with connected segment ticks - no Painter2D needed.</summary>
        private static void BuildLine(VisualElement plot, in DesignerPreviewContext ctx)
        {
            for (var i = 0; i < Sample.Length; i++)
            {
                var column = new VisualElement { style = { flexGrow = 1, height = new Length(100, LengthUnit.Percent) } };
                column.pickingMode = PickingMode.Ignore;

                var dot = Bar(DesignerPreviewColors.Accent, 999f);
                dot.style.position = Position.Absolute;
                dot.style.width = 5; dot.style.height = 5;
                dot.style.left = new Length(50, LengthUnit.Percent);
                dot.style.bottom = new Length(Sample[i] * 100f, LengthUnit.Percent);
                column.Add(dot);

                var stem = Bar(new Color(DesignerPreviewColors.Accent.r, DesignerPreviewColors.Accent.g, DesignerPreviewColors.Accent.b, 0.25f), 0f);
                stem.style.position = Position.Absolute;
                stem.style.left = new Length(50, LengthUnit.Percent);
                stem.style.width = 2; stem.style.bottom = 0;
                stem.style.height = new Length(Sample[i] * 100f, LengthUnit.Percent);
                column.Add(stem);

                plot.Add(column);
            }
        }

        private static void BuildPie(VisualElement view, in DesignerPreviewContext ctx)
        {
            var ring = new RadialFillPreview
            {
                Value = 68f,
                Spin = false,
                Clockwise = true,
                FillColor = DesignerPreviewColors.Accent
            };
            ring.AddToClassList("nexui-preview-radial");
            view.Add(ring);
        }

        private static void BuildMatrix(VisualElement plot, in DesignerPreviewContext ctx)
        {
            plot.style.flexWrap = Wrap.Wrap;
            plot.style.alignItems = Align.Stretch;
            for (var i = 0; i < 24; i++)
            {
                var alpha = 0.15f + (i * 37 % 100) / 100f * 0.7f;
                var cell = Bar(new Color(DesignerPreviewColors.Accent.r, DesignerPreviewColors.Accent.g, DesignerPreviewColors.Accent.b, alpha), 2f);
                cell.style.width = new Length(15, LengthUnit.Percent);
                cell.style.height = new Length(22, LengthUnit.Percent);
                cell.style.marginLeft = 2; cell.style.marginBottom = 2;
                plot.Add(cell);
            }
        }

        private static void BuildPoints(VisualElement plot, in DesignerPreviewContext ctx)
        {
            for (var i = 0; i < Sample.Length; i++)
            {
                var column = new VisualElement { style = { flexGrow = 1, height = new Length(100, LengthUnit.Percent) } };
                column.pickingMode = PickingMode.Ignore;
                for (var j = 0; j < 2; j++)
                {
                    var height = j == 0 ? Sample[i] : Sample[(i + 3) % Sample.Length] * 0.6f;
                    var dot = Bar(DesignerPreviewColors.Accent, 999f);
                    dot.style.position = Position.Absolute;
                    dot.style.width = 5; dot.style.height = 5;
                    dot.style.left = new Length(35 + j * 25, LengthUnit.Percent);
                    dot.style.bottom = new Length(height * 100f, LengthUnit.Percent);
                    column.Add(dot);
                }
                plot.Add(column);
            }
        }

        private static VisualElement Bar(Color color, float radius)
        {
            var element = new VisualElement();
            element.style.backgroundColor = new StyleColor(color);
            element.style.borderTopLeftRadius = radius; element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius; element.style.borderBottomRightRadius = radius;
            element.pickingMode = PickingMode.Ignore;
            return element;
        }
    }

    /// <summary>Row with a leading visual, two text lines and a trailing affordance.</summary>
    public sealed class ListRowPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexGrow = 1, flexDirection = FlexDirection.Row, alignItems = Align.Center,
                    paddingLeft = 8, paddingRight = 8
                }
            };
            row.pickingMode = PickingMode.Ignore;

            var leading = Block(DesignerPreviewColors.Lighten(ctx.Tint, 0.25f), 999f);
            leading.style.width = 20; leading.style.height = 20; leading.style.marginRight = 8; leading.style.flexShrink = 0;
            row.Add(leading);

            var lines = new VisualElement { style = { flexGrow = 1, justifyContent = Justify.Center } };
            lines.pickingMode = PickingMode.Ignore;
            if (!string.IsNullOrEmpty(ctx.Element.text))
            {
                var label = new Label(ctx.Element.text)
                {
                    style = { fontSize = Mathf.Max(8f, 11f * ctx.Zoom), color = new StyleColor(Color.white), opacity = 0.9f }
                };
                label.pickingMode = PickingMode.Ignore;
                lines.Add(label);
            }
            else
            {
                var primary = Block(DesignerPreviewColors.Lighten(ctx.Tint, 0.22f), 2f);
                primary.style.height = 8; primary.style.width = new Length(55, LengthUnit.Percent);
                lines.Add(primary);
            }
            var secondary = Block(DesignerPreviewColors.Lighten(ctx.Tint, 0.12f), 2f);
            secondary.style.height = 6; secondary.style.width = new Length(35, LengthUnit.Percent); secondary.style.marginTop = 4;
            lines.Add(secondary);
            row.Add(lines);

            var trailing = Block(DesignerPreviewColors.Lighten(ctx.Tint, 0.2f), 2f);
            trailing.style.width = 14; trailing.style.height = 14; trailing.style.flexShrink = 0;
            row.Add(trailing);

            view.Add(row);
        }

        internal static VisualElement Block(Color color, float radius)
        {
            var element = new VisualElement();
            element.style.backgroundColor = new StyleColor(color);
            element.style.borderTopLeftRadius = radius; element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius; element.style.borderBottomRightRadius = radius;
            element.pickingMode = PickingMode.Ignore;
            return element;
        }
    }

    /// <summary>Card/tile with a headline value, a caption and a small trend chip.</summary>
    public sealed class StatTilePreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var column = new VisualElement { style = { flexGrow = 1, paddingLeft = 10, paddingRight = 10, paddingTop = 8, justifyContent = Justify.Center } };
            column.pickingMode = PickingMode.Ignore;

            var caption = ListRowPreviewRenderer.Block(DesignerPreviewColors.Lighten(ctx.Tint, 0.16f), 2f);
            caption.style.height = 6; caption.style.width = new Length(40, LengthUnit.Percent); caption.style.marginBottom = 6;
            column.Add(caption);

            var headline = new Label(string.IsNullOrEmpty(ctx.Element.text)
                ? Mathf.RoundToInt(ctx.Element.previewValue).ToString()
                : ctx.Element.text)
            {
                style =
                {
                    fontSize = Mathf.Max(11f, 20f * ctx.Zoom),
                    color = new StyleColor(Color.white),
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            headline.pickingMode = PickingMode.Ignore;
            column.Add(headline);

            var trend = ListRowPreviewRenderer.Block(
                ctx.State == DesignerComponentState.Error ? DesignerPreviewColors.Error : DesignerPreviewColors.Success, 999f);
            trend.style.height = 6; trend.style.width = 34; trend.style.marginTop = 6;
            column.Add(trend);

            view.Add(column);
        }
    }

    /// <summary>Color area / swatch / gradient strip.</summary>
    public sealed class ColorAreaPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        private static readonly Color[] Hues =
        {
            new Color(0.90f, 0.30f, 0.30f), new Color(0.90f, 0.65f, 0.25f), new Color(0.85f, 0.85f, 0.30f),
            new Color(0.35f, 0.80f, 0.40f), new Color(0.30f, 0.65f, 0.90f), new Color(0.55f, 0.40f, 0.90f)
        };

        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var strip = new VisualElement
            {
                style =
                {
                    position = Position.Absolute, left = 4, right = 4, top = 4, bottom = 4,
                    flexDirection = FlexDirection.Row
                }
            };
            strip.pickingMode = PickingMode.Ignore;
            foreach (var hue in Hues)
            {
                var band = ListRowPreviewRenderer.Block(hue, 0f);
                band.style.flexGrow = 1;
                strip.Add(band);
            }
            view.Add(strip);
        }
    }

    /// <summary>Virtual stick / d-pad: base ring with the knob offset by the preview value.</summary>
    public sealed class JoystickPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var ring = ListRowPreviewRenderer.Block(DesignerPreviewColors.Darken(ctx.Tint, 0.2f), 999f);
            ring.style.position = Position.Absolute;
            ring.style.left = 6; ring.style.right = 6; ring.style.top = 6; ring.style.bottom = 6;
            view.Add(ring);

            var offset = Mathf.Clamp01(ctx.Element.previewValue / 100f) * 0.3f;
            var knob = ListRowPreviewRenderer.Block(DesignerPreviewColors.Accent, 999f);
            knob.style.position = Position.Absolute;
            knob.style.width = new Length(38, LengthUnit.Percent);
            knob.style.height = new Length(38, LengthUnit.Percent);
            knob.style.left = new Length((31f + offset * 100f * 0.3f), LengthUnit.Percent);
            knob.style.top = new Length(31, LengthUnit.Percent);
            view.Add(knob);
        }
    }
}

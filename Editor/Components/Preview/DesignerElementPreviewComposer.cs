using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Components.Preview
{
    /// <summary>
    /// Draws an element on the canvas from the components attached to it, in attachment order.
    /// </summary>
    /// <remarks>
    /// This is what keeps the canvas honest under the component model: remove the Image and the fill
    /// disappears, add a Gradient and it lands on top of whatever draws beneath it. Each component
    /// contributes its own layer, exactly as its runtime counterpart would - a renderer paints the
    /// element, and effects such as Gradient, Soft Shadow or Cooldown Overlay stack over it.
    ///
    /// An element whose components the canvas has no drawing for (a UI Toolkit control, a plain
    /// layout group) falls back to the palette preset's renderer, so nothing ever regresses to an
    /// empty box just because a component is not visual.
    /// </remarks>
    public static class DesignerElementPreviewComposer
    {
        private static readonly Dictionary<string, IUIDesignerComponentPreviewRenderer> ByComponent =
            new Dictionary<string, IUIDesignerComponentPreviewRenderer>();
        private static bool _built;

        /// <summary>Components that only affect behaviour; the canvas deliberately draws nothing for them.</summary>
        private static readonly HashSet<string> Invisible = new HashSet<string>
        {
            "Core.Element", "UGUI.CanvasGroup", "UGUI.Canvas", "UGUI.GraphicRaycaster",
            "UGUI.LayoutElement", "UGUI.ContentSizeFitter", "UGUI.AspectRatioFitter",
            "UGUI.RectMask2D", "UGUI.Mask", "UGUI.ToggleGroup",
            "NX.SafeArea", "NX.SwipeArea", "NX.TooltipTrigger", "NX.HoldButton"
        };

        public static void Build(VisualElement view, in DesignerPreviewContext ctx)
        {
            var element = ctx.Element;
            var components = element?.components;

            // Elements that predate the component model - or that carry only behaviour components -
            // still draw through the preset renderer they were created from.
            if (components == null || components.Count == 0)
            {
                DesignerComponentPreviewRegistry.Get(element?.elementType).BuildPreview(view, ctx);
                return;
            }

            EnsureBuilt();

            var drawn = 0;
            foreach (var component in components)
            {
                if (component == null || !component.enabled || string.IsNullOrEmpty(component.typeId)) continue;
                if (Invisible.Contains(component.typeId)) continue;
                if (!ByComponent.TryGetValue(component.typeId, out var renderer)) continue;

                renderer.BuildPreview(view, ctx);
                drawn++;
            }

            if (drawn == 0)
                DesignerComponentPreviewRegistry.Get(element.elementType).BuildPreview(view, ctx);
        }

        /// <summary>True when the canvas can draw something for this component type.</summary>
        public static bool Draws(string componentTypeId)
        {
            EnsureBuilt();
            return !string.IsNullOrEmpty(componentTypeId)
                   && !Invisible.Contains(componentTypeId)
                   && ByComponent.ContainsKey(componentTypeId);
        }

        private static void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            var image = new ImagePreviewRenderer(fullBleed: true);
            var linear = new LinearFillPreviewRenderer();
            var radial = new RadialPreviewRenderer(spin: false);
            var rows = new CollectionPreviewRenderer(grid: false);
            var grid = new CollectionPreviewRenderer(grid: true);
            var iconRow = new IconRowPreviewRenderer();

            // ---- Renderers: what actually paints the element ------------------------------
            ByComponent["UGUI.Image"] = image;
            ByComponent["UGUI.RawImage"] = image;
            ByComponent["UITK.Image"] = image;
            ByComponent["NX.RoundedRect"] = new SurfacePreviewRenderer(rounded: true);
            ByComponent["UGUI.Text"] = new ComponentTextPreviewRenderer();
            ByComponent["UGUI.TextMeshProUGUI"] = new ComponentTextPreviewRenderer();
            ByComponent["UITK.Label"] = new ComponentTextPreviewRenderer();

            // ---- Controls -----------------------------------------------------------------
            ByComponent["UGUI.Slider"] = new SliderPreviewRenderer(range: false);
            ByComponent["UITK.Slider"] = new SliderPreviewRenderer(range: false);
            ByComponent["UGUI.Scrollbar"] = new ScrollbarPreviewRenderer();
            ByComponent["UGUI.Toggle"] = new CheckboxPreviewRenderer();
            ByComponent["UITK.Toggle"] = new CheckboxPreviewRenderer();
            ByComponent["UGUI.Dropdown"] = new DropdownPreviewRenderer();
            ByComponent["UGUI.TMP_Dropdown"] = new DropdownPreviewRenderer();
            ByComponent["UITK.DropdownField"] = new DropdownPreviewRenderer();
            ByComponent["UGUI.InputField"] = new InputFieldPreviewRenderer();
            ByComponent["UGUI.TMP_InputField"] = new InputFieldPreviewRenderer();
            ByComponent["UITK.TextField"] = new InputFieldPreviewRenderer();
            ByComponent["UGUI.ScrollRect"] = new ScrollAreaPreviewRenderer();
            ByComponent["UITK.ScrollView"] = new ScrollAreaPreviewRenderer();
            ByComponent["UITK.ListView"] = rows;
            ByComponent["UITK.TabView"] = new TabStripPreviewRenderer();

            // ---- Layout: shown as the arrangement they impose -------------------------------
            ByComponent["UGUI.HorizontalLayoutGroup"] = iconRow;
            ByComponent["UGUI.VerticalLayoutGroup"] = rows;
            ByComponent["UGUI.GridLayoutGroup"] = grid;
            ByComponent["NX.AutoGrid"] = grid;
            ByComponent["NX.FlowLayout"] = iconRow;
            ByComponent["NX.RadialLayout"] = new RadialArrangementPreviewRenderer();

            // ---- Effects that stack over whatever drew beneath them --------------------------
            ByComponent["NX.Gradient"] = new GradientOverlayPreviewRenderer();
            ByComponent["NX.SoftShadow"] = new SoftShadowPreviewRenderer();
            ByComponent["NX.SegmentedBar"] = new SegmentedBarPreviewRenderer();
            ByComponent["NX.CooldownOverlay"] = radial;
            ByComponent["UGUI.Outline"] = new OutlinePreviewRenderer();
            ByComponent["UGUI.Shadow"] = new SoftShadowPreviewRenderer();

            // ---- Text effects drive the text that a renderer already drew ---------------------
            ByComponent["NX.MarqueeText"] = new ComponentTextPreviewRenderer(suffix: "  ›››");
            ByComponent["NX.TypewriterText"] = new ComponentTextPreviewRenderer(typewriter: true);
            ByComponent["NX.NumberTicker"] = new ComponentTextPreviewRenderer(numeric: true);

            ByComponent["UITK.ProgressBar"] = linear;
        }
    }

    /// <summary>Flat or rounded surface fill - what an Image or a Rounded Rect contributes.</summary>
    public sealed class SurfacePreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        private readonly bool _rounded;
        public SurfacePreviewRenderer(bool rounded) => _rounded = rounded;

        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var surface = new VisualElement();
            surface.style.position = Position.Absolute;
            surface.style.left = 0; surface.style.right = 0; surface.style.top = 0; surface.style.bottom = 0;
            surface.style.backgroundColor = new StyleColor(ctx.Tint);
            surface.pickingMode = PickingMode.Ignore;

            if (_rounded)
            {
                var radius = 10f;
                surface.style.borderTopLeftRadius = radius; surface.style.borderTopRightRadius = radius;
                surface.style.borderBottomLeftRadius = radius; surface.style.borderBottomRightRadius = radius;
            }
            view.Add(surface);
        }
    }

    /// <summary>The element's text, drawn because a text component is attached rather than because of its type.</summary>
    public sealed class ComponentTextPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        private readonly string _suffix;
        private readonly bool _typewriter;
        private readonly bool _numeric;

        public ComponentTextPreviewRenderer(string suffix = null, bool typewriter = false, bool numeric = false)
        {
            _suffix = suffix;
            _typewriter = typewriter;
            _numeric = numeric;
        }

        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var text = ctx.Element.text ?? string.Empty;
            if (_numeric) text = Mathf.RoundToInt(ctx.Element.previewValue).ToString("N0");
            // A typewriter shows its line mid-reveal, which is the state worth seeing while authoring.
            if (_typewriter && text.Length > 2) text = text.Substring(0, Mathf.Max(1, text.Length / 2)) + "|";
            if (!string.IsNullOrEmpty(_suffix)) text += _suffix;
            if (string.IsNullOrEmpty(text)) return;

            var label = new Label(text)
            {
                style =
                {
                    position = Position.Absolute,
                    left = 4, right = 4, top = 0, bottom = 0,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Max(8f, ctx.Element.fontSize * ctx.Zoom),
                    color = new StyleColor(ctx.Element.textColor)
                }
            };
            label.pickingMode = PickingMode.Ignore;
            view.Add(label);
        }
    }

    /// <summary>Gradient wash over whatever is already drawn.</summary>
    public sealed class GradientOverlayPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            // USS has no gradient, so the canvas approximates it with stacked translucent bands - the
            // same trick the runtime element uses, at a resolution that is cheap to rebuild.
            const int bands = 6;
            var container = new VisualElement { style = { position = Position.Absolute, left = 0, right = 0, top = 0, bottom = 0 } };
            container.pickingMode = PickingMode.Ignore;

            for (var i = 0; i < bands; i++)
            {
                var t = i / (float)(bands - 1);
                var band = new VisualElement();
                band.style.flexGrow = 1;
                band.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, Mathf.Lerp(0.18f, 0f, t)));
                band.pickingMode = PickingMode.Ignore;
                container.Add(band);
            }
            view.Add(container);
        }
    }

    /// <summary>Soft shadow hint drawn just outside the element's lower edge.</summary>
    public sealed class SoftShadowPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var shadow = new VisualElement();
            shadow.style.position = Position.Absolute;
            shadow.style.left = 3; shadow.style.right = -3; shadow.style.top = 6; shadow.style.bottom = -6;
            shadow.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.28f));
            shadow.style.borderTopLeftRadius = 10; shadow.style.borderTopRightRadius = 10;
            shadow.style.borderBottomLeftRadius = 10; shadow.style.borderBottomRightRadius = 10;
            shadow.pickingMode = PickingMode.Ignore;
            // Behind everything else this element already drew.
            view.Insert(0, shadow);
        }
    }

    /// <summary>Outline ring for uGUI's Outline effect.</summary>
    public sealed class OutlinePreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var outline = new VisualElement();
            outline.style.position = Position.Absolute;
            outline.style.left = -1; outline.style.right = -1; outline.style.top = -1; outline.style.bottom = -1;
            outline.style.borderTopWidth = 1; outline.style.borderBottomWidth = 1;
            outline.style.borderLeftWidth = 1; outline.style.borderRightWidth = 1;
            var color = new Color(0f, 0f, 0f, 0.55f);
            outline.style.borderTopColor = color; outline.style.borderBottomColor = color;
            outline.style.borderLeftColor = color; outline.style.borderRightColor = color;
            outline.pickingMode = PickingMode.Ignore;
            view.Add(outline);
        }
    }

    /// <summary>Discrete segments, matching what the runtime Segmented Bar draws.</summary>
    public sealed class SegmentedBarPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var component = FindComponent(ctx.Element, "NX.SegmentedBar");
            var segments = component != null
                ? Mathf.Clamp(DesignerElementComponentAccess.GetInt(component, "segments", 5), 1, 64)
                : 5;
            var value = component != null
                ? Mathf.Clamp01(DesignerElementComponentAccess.GetFloat(component, "value", 1f))
                : Mathf.Clamp01(ctx.Element.previewValue / 100f);

            var row = new VisualElement
            {
                style =
                {
                    position = Position.Absolute, left = 0, right = 0, top = 0, bottom = 0,
                    flexDirection = FlexDirection.Row
                }
            };
            row.pickingMode = PickingMode.Ignore;

            var filled = value * segments;
            for (var i = 0; i < segments; i++)
            {
                var fraction = Mathf.Clamp01(filled - i);
                var cell = new VisualElement { style = { flexGrow = 1, marginLeft = i == 0 ? 0 : 2 } };
                cell.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.12f));
                cell.pickingMode = PickingMode.Ignore;

                if (fraction > 0f)
                {
                    var fill = new VisualElement
                    {
                        style =
                        {
                            position = Position.Absolute, left = 0, top = 0, bottom = 0,
                            width = new Length(fraction * 100f, LengthUnit.Percent),
                            backgroundColor = new StyleColor(DesignerPreviewColors.Lighten(ctx.Tint, 0.45f))
                        }
                    };
                    fill.pickingMode = PickingMode.Ignore;
                    cell.Add(fill);
                }
                row.Add(cell);
            }
            view.Add(row);
        }

        internal static DesignerElementComponent FindComponent(DesignerElementMetadata element, string typeId)
        {
            if (element?.components == null) return null;
            foreach (var component in element.components)
                if (component != null && component.typeId == typeId) return component;
            return null;
        }
    }

    /// <summary>Children arranged around a circle, as the Radial Layout would place them.</summary>
    public sealed class RadialArrangementPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            const int count = 6;
            for (var i = 0; i < count; i++)
            {
                var angle = i / (float)count * Mathf.PI * 2f;
                var dot = new VisualElement();
                dot.style.position = Position.Absolute;
                dot.style.width = 12; dot.style.height = 12;
                dot.style.left = new Length(50f + Mathf.Cos(angle) * 32f, LengthUnit.Percent);
                dot.style.top = new Length(50f - Mathf.Sin(angle) * 32f, LengthUnit.Percent);
                dot.style.marginLeft = -6; dot.style.marginTop = -6;
                dot.style.backgroundColor = new StyleColor(DesignerPreviewColors.Lighten(ctx.Tint, 0.3f));
                dot.style.borderTopLeftRadius = 6; dot.style.borderTopRightRadius = 6;
                dot.style.borderBottomLeftRadius = 6; dot.style.borderBottomRightRadius = 6;
                dot.pickingMode = PickingMode.Ignore;
                view.Add(dot);
            }
        }
    }
}

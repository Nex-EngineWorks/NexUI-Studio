using emiteat.NexUI.Designer.Editor.Viewport;
using UnityEngine;
using UnityEngine.UIElements;

// IStyle.unityBackgroundScaleMode is deprecated in newer Unity, but remains the working way to set
// a preview VisualElement's background scaling in this project's Unity version (the background-*
// replacement is not consistently available). Scoped to the preview renderers only.
#pragma warning disable CS0618

namespace emiteat.NexUI.Designer.Editor.Components.Preview
{
    /// <summary>No-op renderer for leaf/plain types (Panel/Label/Button/etc.) - the base tinted box + name/text is enough.</summary>
    public sealed class GenericPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx) { }
    }

    /// <summary>Progress Bar / Stat Bar: virtual Track + Fill honoring fill direction and preview value.</summary>
    public sealed class LinearFillPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var element = ctx.Element;
            var direction = DesignerComponentPropertyAccess.GetEnum(element, "value.direction") switch
            {
                "RightToLeft" => DesignerFillDirection.RightToLeft,
                "BottomToTop" => DesignerFillDirection.BottomToTop,
                "TopToBottom" => DesignerFillDirection.TopToBottom,
                _ => element.fill.direction
            };
            var horizontal = direction == DesignerFillDirection.LeftToRight || direction == DesignerFillDirection.RightToLeft;

            var track = new VisualElement();
            track.AddToClassList("nexui-preview-fill-track");
            track.EnableInClassList("is-vertical", !horizontal);
            DesignerPreviewPartUtility.Register(track, ctx, "track");
            view.Add(track);

            var indeterminate = ctx.State == DesignerComponentState.Indeterminate;
            var minimum = DesignerComponentPropertyAccess.GetFloat(element, "value.min", element.fill.minValue);
            var maximum = DesignerComponentPropertyAccess.GetFloat(element, "value.max", element.fill.maxValue);
            if (maximum <= minimum) maximum = minimum + 1f;
            var previewValue = DesignerComponentPropertyAccess.GetBool(element, "value.wholeNumbers")
                ? Mathf.Round(element.previewValue)
                : element.previewValue;
            var fraction = indeterminate ? 0.4f
                : Mathf.Clamp01(Mathf.InverseLerp(minimum, maximum, previewValue));

            var fillColor = ctx.State == DesignerComponentState.Error ? DesignerPreviewColors.Error
                : ctx.State == DesignerComponentState.Success ? DesignerPreviewColors.Success
                : ctx.FillColor;

            var fillBar = new VisualElement();
            fillBar.AddToClassList("nexui-preview-fill-bar");
            fillBar.style.position = Position.Absolute;
            fillBar.style.backgroundColor = new StyleColor(fillColor);

            if (horizontal)
            {
                fillBar.style.top = 0;
                fillBar.style.bottom = 0;
                fillBar.style.width = new Length(fraction * 100f, LengthUnit.Percent);
                if (direction == DesignerFillDirection.RightToLeft) { fillBar.style.right = 0; fillBar.style.left = StyleKeyword.Auto; }
                else { fillBar.style.left = 0; fillBar.style.right = StyleKeyword.Auto; }
            }
            else
            {
                fillBar.style.left = 0;
                fillBar.style.right = 0;
                fillBar.style.height = new Length(fraction * 100f, LengthUnit.Percent);
                if (direction == DesignerFillDirection.TopToBottom) { fillBar.style.top = 0; fillBar.style.bottom = StyleKeyword.Auto; }
                else { fillBar.style.bottom = 0; fillBar.style.top = StyleKeyword.Auto; }
            }
            DesignerPreviewPartUtility.Register(fillBar, ctx, "fill");
            track.Add(fillBar);

            if (indeterminate)
            {
                // Sweep the fill back and forth to signal indeterminate progress.
                var forward = true;
                fillBar.schedule.Execute(() =>
                {
                    forward = !forward;
                    fillBar.style.width = new Length((forward ? 0.25f : 0.6f) * 100f, LengthUnit.Percent);
                }).Every(500);
            }
        }
    }

    /// <summary>Radial Fill / Spinner: reuses the Painter2D-based <see cref="RadialFillPreview"/> element.</summary>
    public sealed class RadialPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        private readonly bool _spin;
        public RadialPreviewRenderer(bool spin) => _spin = spin;

        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var ring = new RadialFillPreview
            {
                Value = ctx.State == DesignerComponentState.Indeterminate ? 65f : ctx.Element.previewValue,
                Spin = _spin || ctx.State == DesignerComponentState.Indeterminate,
                Clockwise = ctx.Element.fill.clockwise,
                FillColor = ctx.State == DesignerComponentState.Error ? DesignerPreviewColors.Error : ctx.FillColor
            };
            ring.AddToClassList("nexui-preview-radial");
            view.Add(ring);
        }
    }

    public sealed class ChoiceListPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var options = ctx.Element.previewOptions;
            var hasOptions = options != null && options.Count > 0;
            if (ctx.State == DesignerComponentState.Empty) { AddEmpty(view, "No options"); return; }

            var list = new VisualElement();
            list.AddToClassList("nexui-preview-choice-list");
            var count = hasOptions ? options.Count : 3;
            for (int i = 0; i < count; i++)
            {
                var row = new VisualElement();
                row.AddToClassList("nexui-preview-choice-row");
                var box = new VisualElement();
                box.AddToClassList("nexui-preview-choice-box");
                box.EnableInClassList("is-checked", i == 0);
                row.Add(box);
                if (hasOptions)
                {
                    var lbl = new Label(options[i]) { style = { fontSize = System.Math.Max(9f, 11f * ctx.Zoom), marginLeft = 4 } };
                    lbl.pickingMode = PickingMode.Ignore;
                    row.Add(lbl);
                }
                list.Add(row);
            }
            view.Add(list);
        }

        internal static void AddEmpty(VisualElement view, string message)
        {
            var label = new Label(message);
            label.AddToClassList("nexui-preview-empty");
            label.style.opacity = 0.6f;
            view.Add(label);
        }
    }

    /// <summary>List / Grid: generated preview rows/cells honoring Loading/Empty/Error states.</summary>
    public sealed class CollectionPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        private readonly bool _grid;
        public CollectionPreviewRenderer(bool grid) => _grid = grid;

        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            switch (ctx.State)
            {
                case DesignerComponentState.Empty: ChoiceListPreviewRenderer.AddEmpty(view, "Empty"); return;
                case DesignerComponentState.Loading: ChoiceListPreviewRenderer.AddEmpty(view, "Loading…"); return;
                case DesignerComponentState.Error: ChoiceListPreviewRenderer.AddEmpty(view, "Error"); return;
            }

            var container = new VisualElement();
            container.AddToClassList(_grid ? "nexui-preview-grid" : "nexui-preview-list");
            var defaultCount = _grid ? 6 : 3;
            var count = ctx.Element.previewItemCount > 0 ? Mathf.Min(ctx.Element.previewItemCount, 64) : defaultCount;
            for (int i = 0; i < count; i++)
            {
                var cell = new VisualElement();
                cell.AddToClassList(_grid ? "nexui-preview-grid-cell" : "nexui-preview-list-row");
                container.Add(cell);
            }
            view.Add(container);
        }
    }

    /// <summary>
    /// Hotbar: a row of generated slot cells (count = previewItemCount, default 8) with one active
    /// index highlighted (from previewValue). Slots are generated preview items, not stored elements.
    /// </summary>
    public sealed class HotbarPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var count = ctx.Element.previewItemCount > 0 ? Mathf.Min(ctx.Element.previewItemCount, 12) : 8;
            var active = Mathf.Clamp(Mathf.RoundToInt(ctx.Element.previewValue), 0, count - 1);

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1, justifyContent = Justify.Center, alignItems = Align.Center } };
            for (int i = 0; i < count; i++)
            {
                var cell = new VisualElement();
                cell.style.flexGrow = 1;
                cell.style.marginLeft = 2; cell.style.marginRight = 2;
                cell.style.height = new Length(80, LengthUnit.Percent);
                cell.style.backgroundColor = new StyleColor(DesignerPreviewColors.Lighten(ctx.Tint, 0.12f));
                cell.style.borderTopLeftRadius = 4; cell.style.borderTopRightRadius = 4;
                cell.style.borderBottomLeftRadius = 4; cell.style.borderBottomRightRadius = 4;
                if (i == active)
                {
                    cell.style.borderTopWidth = 2; cell.style.borderBottomWidth = 2;
                    cell.style.borderLeftWidth = 2; cell.style.borderRightWidth = 2;
                    var c = DesignerPreviewColors.Accent;
                    cell.style.borderTopColor = c; cell.style.borderBottomColor = c;
                    cell.style.borderLeftColor = c; cell.style.borderRightColor = c;
                }
                // Key label (1..9, 0).
                var key = i < 9 ? (i + 1).ToString() : i == 9 ? "0" : "";
                if (!string.IsNullOrEmpty(key))
                {
                    var lbl = new Label(key) { style = { position = Position.Absolute, left = 3, top = 1, fontSize = Mathf.Max(8f, 10f * ctx.Zoom), opacity = 0.7f } };
                    lbl.pickingMode = PickingMode.Ignore;
                    cell.Add(lbl);
                }
                row.Add(cell);
            }
            view.Add(row);
        }
    }

    public sealed class SkeletonPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var container = new VisualElement();
            container.AddToClassList("nexui-preview-skeleton");
            for (int i = 0; i < 3; i++)
            {
                var bar = new VisualElement();
                bar.AddToClassList("nexui-preview-skeleton-bar");
                container.Add(bar);

                var dim = false;
                bar.schedule.Execute(() =>
                {
                    dim = !dim;
                    bar.style.opacity = dim ? 0.35f : 0.85f;
                }).Every(450);
            }
            view.Add(container);
        }
    }

    /// <summary>
    /// Real-texture preview for sprite-carrying types: full-bleed for image-shaped components
    /// (Image, uGUI Image/RawImage, Avatar), or a centered icon for icon-carrying ones
    /// (IconButton, Icon, Currency Display).
    /// </summary>
    public sealed class ImagePreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        private readonly bool _fullBleed;

        public ImagePreviewRenderer(bool fullBleed = true) => _fullBleed = fullBleed;

        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var element = ctx.Element;
            if (element.previewImage == null) return;
            if (_fullBleed)
            {
                view.style.backgroundImage = new StyleBackground(element.previewImage);
                view.style.unityBackgroundScaleMode = new StyleEnum<ScaleMode>(ScaleMode.ScaleToFit);
                return;
            }
            var icon = new VisualElement();
            icon.AddToClassList("nexui-preview-icon");
            icon.style.backgroundImage = new StyleBackground(element.previewImage);
            icon.style.unityBackgroundScaleMode = new StyleEnum<ScaleMode>(ScaleMode.ScaleToFit);
            view.Add(icon);
        }
    }

    /// <summary>
    /// Inventory/hotbar Slot cell: empty placeholder, count overlay, selected outline, cooldown/
    /// locked/disabled overlays driven by the forced state and preview value.
    /// </summary>
    public sealed class SlotPreviewRenderer : IUIDesignerComponentPreviewRenderer
    {
        public void BuildPreview(VisualElement view, in DesignerPreviewContext ctx)
        {
            var state = ctx.State;

            if (state == DesignerComponentState.Empty)
            {
                var empty = new VisualElement();
                empty.AddToClassList("nexui-preview-slot-empty");
                empty.style.flexGrow = 1;
                empty.style.opacity = 0.4f;
                view.Add(empty);
                return;
            }

            // Item glyph (uses the preview image if the user assigned one, else a filled square).
            var item = new VisualElement();
            item.style.position = Position.Absolute;
            item.style.left = 6; item.style.top = 6; item.style.right = 6; item.style.bottom = 6;
            item.style.backgroundColor = new StyleColor(DesignerPreviewColors.Lighten(ctx.Tint, 0.25f));
            if (ctx.Element.previewImage != null)
            {
                item.style.backgroundImage = new StyleBackground(ctx.Element.previewImage);
                item.style.unityBackgroundScaleMode = new StyleEnum<ScaleMode>(ScaleMode.ScaleToFit);
            }
            view.Add(item);

            // Stack count overlay (bottom-right).
            var count = new Label(Mathf.Max(1, Mathf.RoundToInt(ctx.Element.previewValue)).ToString());
            count.style.position = Position.Absolute;
            count.style.right = 4; count.style.bottom = 2;
            count.style.fontSize = Mathf.Max(9f, 12f * ctx.Zoom);
            count.style.color = Color.white;
            count.pickingMode = PickingMode.Ignore;
            view.Add(count);

            // Cooldown radial overlay.
            if (state == DesignerComponentState.Loading) // reuse Loading as "cooldown" preview
            {
                var cd = new VisualElement();
                cd.style.position = Position.Absolute;
                cd.style.left = 0; cd.style.top = 0; cd.style.right = 0; cd.style.bottom = 0;
                cd.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.45f));
                cd.pickingMode = PickingMode.Ignore;
                view.Add(cd);
            }

            // Selected outline / locked overlay.
            if (state == DesignerComponentState.Selected || state == DesignerComponentState.Focused)
            {
                view.style.borderTopWidth = 2; view.style.borderBottomWidth = 2;
                view.style.borderLeftWidth = 2; view.style.borderRightWidth = 2;
                var c = DesignerPreviewColors.Accent;
                view.style.borderTopColor = c; view.style.borderBottomColor = c;
                view.style.borderLeftColor = c; view.style.borderRightColor = c;
            }
        }
    }
}

#pragma warning restore CS0618

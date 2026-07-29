using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Viewport
{
    /// <summary>
    /// The horizontal or vertical ruler strip around the canvas: tick marks that stay readable at any
    /// zoom, a cursor tracker, and drag-out guide creation.
    ///
    /// The ruler owns no state - it asks for zoom/scroll when it draws and hands new guides back
    /// through <see cref="GuideCreated"/>. That keeps guide ownership in one place (the viewport) and
    /// lets both rulers share this class.
    /// </summary>
    public sealed class DesignerRulerOverlay : VisualElement
    {
        public const float Thickness = 18f;

        private readonly DesignerGuideAxis _axis;
        private readonly Func<float> _zoom;
        private readonly Func<float> _scroll;
        private readonly VisualElement _ticks = new VisualElement();
        private readonly VisualElement _cursor = new VisualElement();

        private bool _dragging;

        /// <summary>Raised when the user drags a new guide out of the ruler. Position is in canvas space.</summary>
        public event Action<DesignerGuide> GuideCreated;
        /// <summary>Raised while dragging so the viewport can preview the guide before release.</summary>
        public event Action<DesignerGuideAxis, float?> GuidePreview;

        public DesignerRulerOverlay(DesignerGuideAxis axis, Func<float> zoom, Func<float> scroll)
        {
            _axis = axis;
            _zoom = zoom;
            _scroll = scroll;

            AddToClassList("nexui-ruler");
            AddToClassList(axis == DesignerGuideAxis.Vertical ? "is-horizontal-strip" : "is-vertical-strip");
            pickingMode = PickingMode.Position;
            tooltip = axis == DesignerGuideAxis.Vertical
                ? "Drag down to add a vertical guide."
                : "Drag right to add a horizontal guide.";

            _ticks.AddToClassList("nexui-ruler-ticks");
            _ticks.pickingMode = PickingMode.Ignore;
            Add(_ticks);

            _cursor.AddToClassList("nexui-ruler-cursor");
            _cursor.pickingMode = PickingMode.Ignore;
            _cursor.style.display = DisplayStyle.None;
            Add(_cursor);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<GeometryChangedEvent>(_ => Rebuild());
        }

        /// <summary>Moves the thin cursor marker so the ruler tracks the pointer over the canvas.</summary>
        public void SetCursor(float? canvasPosition)
        {
            if (canvasPosition == null)
            {
                _cursor.style.display = DisplayStyle.None;
                return;
            }
            _cursor.style.display = DisplayStyle.Flex;
            var screen = canvasPosition.Value * _zoom() - _scroll();
            if (_axis == DesignerGuideAxis.Vertical) _cursor.style.left = screen;
            else _cursor.style.top = screen;
        }

        /// <summary>Redraws tick marks for the current zoom and scroll. Cheap enough to call on every canvas change.</summary>
        public void Rebuild()
        {
            _ticks.Clear();

            var zoom = Mathf.Max(0.01f, _zoom());
            var scroll = _scroll();
            var extent = _axis == DesignerGuideAxis.Vertical ? resolvedStyle.width : resolvedStyle.height;
            if (extent <= 1f) return;

            var step = DesignerCanvasGuides.TickStep(zoom);
            var fromCanvas = scroll / zoom;
            var toCanvas = (scroll + extent) / zoom;

            foreach (var tick in DesignerCanvasGuides.Ticks(fromCanvas, toCanvas, step))
            {
                var screen = tick * zoom - scroll;
                if (screen < -40f || screen > extent + 40f) continue;

                var mark = new VisualElement();
                mark.AddToClassList("nexui-ruler-tick");
                mark.pickingMode = PickingMode.Ignore;
                if (_axis == DesignerGuideAxis.Vertical) mark.style.left = screen;
                else mark.style.top = screen;

                var label = new Label(DesignerCanvasGuides.TickLabel(tick));
                label.AddToClassList("nexui-ruler-label");
                label.pickingMode = PickingMode.Ignore;
                mark.Add(label);
                _ticks.Add(mark);
            }
        }

        private float CanvasPositionFrom(Vector2 localPointerPosition)
        {
            var along = _axis == DesignerGuideAxis.Vertical ? localPointerPosition.x : localPointerPosition.y;
            return (along + _scroll()) / Mathf.Max(0.01f, _zoom());
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            _dragging = true;
            this.CapturePointer(evt.pointerId);
            GuidePreview?.Invoke(_axis, CanvasPositionFrom(evt.localPosition));
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_dragging) return;
            GuidePreview?.Invoke(_axis, CanvasPositionFrom(evt.localPosition));
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_dragging) return;
            _dragging = false;
            if (this.HasPointerCapture(evt.pointerId)) this.ReleasePointer(evt.pointerId);

            GuidePreview?.Invoke(_axis, null);

            // Releasing inside the ruler itself is a cancel - the user pulled a guide out and put it
            // back, which should not litter the canvas with a guide at the edge.
            var across = _axis == DesignerGuideAxis.Vertical ? evt.localPosition.y : evt.localPosition.x;
            if (across >= 0f && across <= Thickness)
            {
                evt.StopPropagation();
                return;
            }

            GuideCreated?.Invoke(new DesignerGuide(_axis, Mathf.Round(CanvasPositionFrom(evt.localPosition))));
            evt.StopPropagation();
        }
    }

    /// <summary>
    /// Draws the user's guides over the canvas and makes them grabbable.
    ///
    /// A 1px line is close to impossible to hit with a mouse, so each guide is drawn as a wide
    /// transparent <i>grab band</i> with the visible hair-line centred inside it. The band is what
    /// receives pointer events; the line is purely visual. That is the whole reason guides were
    /// previously create-and-delete only.
    /// </summary>
    public sealed class DesignerGuideLayer : VisualElement
    {
        /// <summary>Width of the invisible grab band around each guide, in screen pixels.</summary>
        public const float GrabBandPixels = 11f;

        private readonly Func<float> _zoom;
        private readonly List<DesignerGuide> _guides;

        private int _draggingIndex = -1;
        private bool _dragChangedAnything;

        /// <summary>Raised after the guide list is mutated so the owner can persist it.</summary>
        public event Action Changed;

        /// <summary>Optional grid snapping applied while dragging a guide, supplied by the viewport.</summary>
        public Func<float, float> SnapPosition { get; set; }

        public DesignerGuideLayer(List<DesignerGuide> guides, Func<float> zoom)
        {
            _guides = guides;
            _zoom = zoom;
            AddToClassList("nexui-guide-layer");
            // The layer itself must not swallow canvas clicks - only the grab bands are pickable.
            pickingMode = PickingMode.Ignore;
        }

        /// <summary>A guide the ruler is currently dragging out, drawn before it is committed.</summary>
        public DesignerGuide? Preview { get; set; }

        /// <summary>True while a guide is being dragged, so the canvas can stand down.</summary>
        public bool IsDraggingGuide => _draggingIndex >= 0;

        public void Rebuild()
        {
            // Rebuilding mid-drag would destroy the band holding the pointer capture and silently
            // cancel the drag. Anything that repaints the canvas (selection, zoom, metadata change)
            // can land here, so the guard belongs at the entry point rather than at each caller.
            if (_draggingIndex >= 0) return;

            Clear();
            var zoom = Mathf.Max(0.01f, _zoom());

            for (int i = 0; i < _guides.Count; i++)
                Add(MakeBand(_guides[i], zoom, i));

            if (Preview.HasValue)
                Add(MakeBand(Preview.Value, zoom, -1));
        }

        private VisualElement MakeBand(DesignerGuide guide, float zoom, int index)
        {
            var vertical = guide.Axis == DesignerGuideAxis.Vertical;
            var offset = guide.Position * zoom;

            var band = new VisualElement();
            band.AddToClassList("nexui-guide-band");
            band.AddToClassList(vertical ? "is-vertical" : "is-horizontal");
            // The preview guide is not in the list yet, so it must not be grabbable.
            band.pickingMode = index >= 0 ? PickingMode.Position : PickingMode.Ignore;
            band.userData = index;

            if (vertical) band.style.left = offset - GrabBandPixels * 0.5f;
            else band.style.top = offset - GrabBandPixels * 0.5f;

            var line = new VisualElement();
            line.AddToClassList("nexui-guide-line");
            line.AddToClassList(vertical ? "is-vertical" : "is-horizontal");
            line.EnableInClassList("is-preview", index < 0);
            line.pickingMode = PickingMode.Ignore;
            band.Add(line);

            if (index < 0) return band;

            band.tooltip = vertical
                ? $"Guide X = {Mathf.RoundToInt(guide.Position)}. Drag to move, Alt-click or drag onto the ruler to remove."
                : $"Guide Y = {Mathf.RoundToInt(guide.Position)}. Drag to move, Alt-click or drag onto the ruler to remove.";

            band.RegisterCallback<PointerDownEvent>(evt => OnBandPointerDown(evt, band, index));
            band.RegisterCallback<PointerMoveEvent>(evt => OnBandPointerMove(evt, band));
            band.RegisterCallback<PointerUpEvent>(evt => OnBandPointerUp(evt, band));
            return band;
        }

        private void OnBandPointerDown(PointerDownEvent evt, VisualElement band, int index)
        {
            if (evt.button != 0 || index < 0 || index >= _guides.Count) return;

            if (evt.altKey)
            {
                _guides.RemoveAt(index);
                Rebuild();
                Changed?.Invoke();
                evt.StopPropagation();
                return;
            }

            _draggingIndex = index;
            _dragChangedAnything = false;
            band.CapturePointer(evt.pointerId);
            band.AddToClassList("is-dragging");
            evt.StopPropagation();
        }

        private void OnBandPointerMove(PointerMoveEvent evt, VisualElement band)
        {
            if (_draggingIndex < 0 || !band.HasPointerCapture(evt.pointerId)) return;

            var guide = _guides[_draggingIndex];
            var local = this.WorldToLocal(evt.position);
            var zoom = Mathf.Max(0.01f, _zoom());
            var position = (guide.Axis == DesignerGuideAxis.Vertical ? local.x : local.y) / zoom;

            if (SnapPosition != null) position = SnapPosition(position);
            position = Mathf.Round(position);

            if (!Mathf.Approximately(position, guide.Position))
            {
                guide.Position = position;
                _guides[_draggingIndex] = guide;
                _dragChangedAnything = true;

                // Move the existing band instead of rebuilding the layer. Rebuild() clears its
                // children, which would destroy the very element holding the pointer capture and drop
                // the drag on the first mouse move.
                MoveBand(band, guide, zoom);
            }
            evt.StopPropagation();
        }

        /// <summary>Repositions a live band without recreating it, so pointer capture survives the drag.</summary>
        private static void MoveBand(VisualElement band, DesignerGuide guide, float zoom)
        {
            var offset = guide.Position * zoom - GrabBandPixels * 0.5f;
            if (guide.Axis == DesignerGuideAxis.Vertical) band.style.left = offset;
            else band.style.top = offset;
        }

        private void OnBandPointerUp(PointerUpEvent evt, VisualElement band)
        {
            if (_draggingIndex < 0) return;
            if (band.HasPointerCapture(evt.pointerId)) band.ReleasePointer(evt.pointerId);

            var index = _draggingIndex;
            _draggingIndex = -1;   // cleared before Rebuild so the guard above lets it through
            band.RemoveFromClassList("is-dragging");

            // Dropped back past the canvas edge (i.e. onto the ruler) - that is the standard
            // "throw it away" gesture in every layout tool.
            var guide = _guides[index];
            var beyondEdge = guide.Position * Mathf.Max(0.01f, _zoom()) < -DesignerCanvasGuides.DeleteThresholdPixels;
            if (beyondEdge)
            {
                _guides.RemoveAt(index);
                _dragChangedAnything = true;
            }

            Rebuild();
            if (_dragChangedAnything) Changed?.Invoke();
            evt.StopPropagation();
        }
    }
}

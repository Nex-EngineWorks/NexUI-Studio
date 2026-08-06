using System;
using emiteat.NexUI.Vector;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Viewport
{
    /// <summary>
    /// Draws and edits the selected element's vector path directly on the canvas.
    /// </summary>
    /// <remarks>
    /// The geometry lives in <see cref="NexPathEditing"/> and the coordinate mapping in
    /// <see cref="DesignerVectorSpace"/>; what is left here is the part no test can reach - hit
    /// radius in screen pixels, which modifier does what, and when an undo entry opens and closes.
    /// That split is what let subdivision and handle mirroring be verified without an EditorWindow.
    ///
    /// The overlay only takes input while <see cref="DesignerTool.Pen"/> is the active tool.
    /// An always-on pen would swallow the clicks that select and drag elements, which is where
    /// nearly all canvas time is spent, so it is opt-in and switching away restores the canvas
    /// exactly.
    /// </remarks>
    public sealed class PenToolOverlay : VisualElement
    {
        /// <summary>Grab distance in screen pixels, so it stays usable at any zoom.</summary>
        private const float HitRadiusPixels = 7f;

        private const float PointRadius = 4f;
        private const float HandleRadius = 3f;

        private static readonly Color PathColor = new Color(0.26f, 0.62f, 1f, 0.95f);
        private static readonly Color PointColor = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color SelectedColor = new Color(1f, 0.72f, 0.2f, 1f);
        private static readonly Color HandleColor = new Color(0.26f, 0.9f, 0.76f, 0.9f);

        private readonly NexUIDesignerContext _context;
        private readonly ContextBoundSubscriptions _subscriptions;

        private bool _active;
        private NexPathHit _dragging = NexPathHit.None;
        private NexPathHit _selected = NexPathHit.None;
        private Vector2 _dragOrigin;
        private int _dragUndoGroup = -1;

        public PenToolOverlay(NexUIDesignerContext context)
        {
            _context = context;
            name = "PenToolOverlay";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;
            generateVisualContent += OnGenerateVisualContent;

            _subscriptions = new ContextBoundSubscriptions(this);
            _subscriptions.Add<DesignerElementMetadata>(
                h => context.MetadataSelectionChanged += h, h => context.MetadataSelectionChanged -= h,
                _ => ClearEditState());
            _subscriptions.Add(h => context.CanvasChanged += h, h => context.CanvasChanged -= h, MarkDirtyRepaint);
            _subscriptions.Add<DesignerElementMetadata>(
                h => context.ElementChanged += h, h => context.ElementChanged -= h, _ => MarkDirtyRepaint());
            _subscriptions.Add(h => context.UIStateChanged += h, h => context.UIStateChanged -= h, SyncActiveTool);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag());
            RegisterCallback<KeyDownEvent>(OnKeyDown);

            SyncActiveTool();
        }

        private DesignerElementMetadata Element => _context.SelectedMetadata;

        /// <summary>The selected element's path, or null when it has never been drawn on.</summary>
        /// <remarks>
        /// Gated on <c>hasShape</c>: the field itself is always non-null because Unity's serializer
        /// materialises it on every element.
        /// </remarks>
        private static NexVectorShape ShapeOf(DesignerElementMetadata element)
            => element != null && element.hasShape ? element.vectorShape : null;

        private NexVectorShape Shape => ShapeOf(Element);

        private void SyncActiveTool()
        {
            var active = _context.CurrentTool == DesignerTool.Pen;
            if (_active == active) return;
            _active = active;

            // Picking is off unless the pen is active, so every other tool behaves as if this
            // overlay were not in the hierarchy at all.
            pickingMode = active ? PickingMode.Position : PickingMode.Ignore;
            focusable = active;
            if (active) Focus();

            ClearEditState();
        }

        private void ClearEditState()
        {
            EndDrag();
            _selected = NexPathHit.None;
            MarkDirtyRepaint();
        }

        // ---- coordinates -----------------------------------------------------

        /// <summary>Canvas coordinates for a pointer position, undoing the zoom.</summary>
        private Vector2 CanvasPoint(Vector2 worldPosition)
            => this.WorldToLocal(worldPosition) / Mathf.Max(0.01f, _context.Zoom);

        /// <summary>Grab radius in canvas units, so it covers the same screen distance at any zoom.</summary>
        private float HitRadius() => HitRadiusPixels / Mathf.Max(0.01f, _context.Zoom);

        // ---- input -----------------------------------------------------------

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!_active || evt.button != 0) return;
            Focus();

            var element = Element;
            if (element == null) return;

            var point = CanvasPoint(evt.position);
            var existing = ShapeOf(element);
            evt.StopPropagation();

            // Alt-click deletes, the shortcut every vector editor uses. Doing it with a modifier
            // rather than a mode means the tool never has to be switched out of.
            if (evt.altKey && existing != null)
            {
                var target = NexPathEditing.HitTest(
                    InCanvasSpace(element, existing), point, HitRadius());

                if (target.Found && target.Part == NexAnchorPart.Point)
                {
                    Edit(element, "Delete Path Point",
                        shape => NexPathEditing.RemoveAnchor(shape, target.Contour, target.Anchor));
                    _selected = NexPathHit.None;
                    return;
                }
            }

            if (existing != null)
            {
                var hit = NexPathEditing.HitTest(
                    InCanvasSpace(element, existing), point, HitRadius());

                if (hit.Found)
                {
                    BeginDrag(hit, point, evt.pointerId);
                    return;
                }
            }

            // Nothing under the cursor: extend the path, starting one if the element has none.
            Edit(element, "Add Path Point", shape =>
            {
                var contour = shape.Contours.Count - 1;
                return NexPathEditing.Append(shape, contour, point);
            }, createShape: true);

            var drawn = ShapeOf(element);
            var anchors = LastContourAnchorCount(drawn);
            _selected = anchors > 0
                ? new NexPathHit(drawn.Contours.Count - 1, anchors - 1, NexAnchorPart.Point)
                : NexPathHit.None;
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_active || !_dragging.Found) return;

            var point = CanvasPoint(evt.position);
            var delta = point - _dragOrigin;
            _dragOrigin = point;

            var element = Element;
            if (element == null) return;

            // Alt breaks the handle pair, which is how one side of a point is given a corner
            // without deleting and redrawing it.
            var mirror = !evt.altKey;
            var hit = _dragging;

            Edit(element, "Move Path Point", shape => NexPathEditing.Move(shape, hit, delta, mirror));
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_dragging.Found) return;

            if (this.HasPointerCapture(evt.pointerId)) this.ReleasePointer(evt.pointerId);
            EndDrag();
            evt.StopPropagation();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!_active) return;

            var element = Element;
            if (ShapeOf(element) == null) return;

            var hit = _selected;

            switch (evt.keyCode)
            {
                case KeyCode.C when hit.Found:
                    Edit(element, "Toggle Path Corner",
                        shape => NexPathEditing.ToggleCorner(shape, hit.Contour, hit.Anchor));
                    evt.StopPropagation();
                    break;

                case KeyCode.Delete when hit.Found:
                case KeyCode.Backspace when hit.Found:
                    Edit(element, "Delete Path Point",
                        shape => NexPathEditing.RemoveAnchor(shape, hit.Contour, hit.Anchor));
                    _selected = NexPathHit.None;
                    evt.StopPropagation();
                    break;

                case KeyCode.Return:
                    // Closing is what turns a drawn run into a fillable shape, so it gets a key of
                    // its own rather than a click back onto the first point - which is fiddly at low
                    // zoom and impossible when that point has scrolled off screen.
                    Edit(element, "Close Path", CloseAll);
                    evt.StopPropagation();
                    break;

                case KeyCode.Escape:
                    // A fresh contour ends the current run, so the next click starts a separate
                    // sub-path instead of joining onto what was just drawn.
                    Edit(element, "End Path Run",
                        shape => { shape.Contours.Add(new NexVectorContour { Closed = false }); return true; });
                    _selected = NexPathHit.None;
                    evt.StopPropagation();
                    break;
            }
        }

        private void BeginDrag(NexPathHit hit, Vector2 origin, int pointerId)
        {
            _dragging = hit;
            _selected = hit;
            _dragOrigin = origin;

            // One undo entry per drag: opened here and collapsed on release, because a drag
            // otherwise leaves an entry per pointer-move event and Ctrl+Z stops meaning anything.
            _dragUndoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Move Path Point");

            this.CapturePointer(pointerId);
            MarkDirtyRepaint();
        }

        private void EndDrag()
        {
            if (_dragUndoGroup >= 0)
            {
                Undo.CollapseUndoOperations(_dragUndoGroup);
                _dragUndoGroup = -1;
            }

            _dragging = NexPathHit.None;
        }

        // ---- editing ---------------------------------------------------------

        /// <summary>
        /// Runs one path edit in canvas coordinates, under undo, leaving the element's rect
        /// matching what was drawn.
        /// </summary>
        /// <remarks>
        /// The bake before and the rect update after are what keep the renderer's bounds-to-rect
        /// fit at identity. Without them each edit would rescale every other point, and the path
        /// would drift away from the cursor as it was drawn.
        /// </remarks>
        private void Edit(DesignerElementMetadata element, string action, Func<NexVectorShape, bool> edit,
            bool createShape = false)
        {
            if (element == null) return;
            if (!element.hasShape && !createShape) return;

            _context.UpdateElement(element, target =>
            {
                if (!target.hasShape)
                {
                    // Starting a path takes the element's tint as its fill, so the first click does
                    // not turn a coloured panel white.
                    target.vectorShape = new NexVectorShape
                    {
                        Filled = true,
                        FillColor = target.tint
                    };
                    target.hasShape = true;
                }

                var shape = target.vectorShape ?? (target.vectorShape = new NexVectorShape());
                if (shape.Contours.Count == 0) shape.Contours.Add(new NexVectorContour { Closed = false });

                DesignerVectorSpace.Bake(shape, target.rect);
                if (!edit(shape)) return;

                target.rect = DesignerVectorSpace.RectFor(shape, target.rect);
            }, action);

            MarkDirtyRepaint();
        }

        private static bool CloseAll(NexVectorShape shape)
        {
            var changed = false;
            foreach (var contour in shape.Contours)
            {
                if (contour == null || contour.Closed || contour.Anchors.Count < 3) continue;
                contour.Closed = true;
                changed = true;
            }
            return changed;
        }

        private static int LastContourAnchorCount(NexVectorShape shape)
        {
            if (shape == null || shape.Contours.Count == 0) return 0;
            return shape.Contours[shape.Contours.Count - 1]?.Anchors.Count ?? 0;
        }

        /// <summary>
        /// The path as the canvas sees it, so a hit test can be done in canvas coordinates.
        /// </summary>
        /// <remarks>
        /// A copy, because hit-testing must not move anything: the element may have been dragged or
        /// resized since the last path edit, and baking that into the real shape is an edit the
        /// user did not ask for and cannot see.
        /// </remarks>
        private static NexVectorShape InCanvasSpace(DesignerElementMetadata element, NexVectorShape shape)
        {
            var copy = shape.Clone();
            DesignerVectorSpace.Bake(copy, element.rect);
            return copy;
        }

        // ---- drawing ---------------------------------------------------------

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (!_active) return;

            var element = Element;
            var shape = element?.vectorShape;
            if (shape == null) return;

            var zoom = _context.Zoom;
            var painter = ctx.painter2D;

            for (var c = 0; c < shape.Contours.Count; c++)
            {
                var contour = shape.Contours[c];
                if (contour == null || contour.Anchors.Count == 0) continue;

                DrawContour(painter, element, shape, contour, zoom);
                DrawAnchors(painter, element, shape, contour, c, zoom);
            }
        }

        /// <summary>Screen position of a path point, through the same fit the renderer uses.</summary>
        private static Vector2 Screen(DesignerElementMetadata element, NexVectorShape shape, Vector2 point, float zoom)
            => DesignerVectorSpace.ShapeToCanvas(shape, element.rect, point) * zoom;

        private static void DrawContour(Painter2D painter, DesignerElementMetadata element, NexVectorShape shape,
            NexVectorContour contour, float zoom)
        {
            var anchors = contour.Anchors;
            if (anchors.Count < 2) return;

            painter.strokeColor = PathColor;
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.MoveTo(Screen(element, shape, anchors[0].Position, zoom));

            var segments = contour.Closed ? anchors.Count : anchors.Count - 1;
            for (var i = 0; i < segments; i++)
            {
                var from = anchors[i];
                var to = anchors[(i + 1) % anchors.Count];

                // The real cubic rather than a straight line between points, so what the tool shows
                // is what the tessellator will produce.
                painter.BezierCurveTo(
                    Screen(element, shape, from.Position + from.OutHandle, zoom),
                    Screen(element, shape, to.Position + to.InHandle, zoom),
                    Screen(element, shape, to.Position, zoom));
            }

            painter.Stroke();
        }

        private void DrawAnchors(Painter2D painter, DesignerElementMetadata element, NexVectorShape shape,
            NexVectorContour contour, int contourIndex, float zoom)
        {
            var anchors = contour.Anchors;

            for (var i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                var point = Screen(element, shape, anchor.Position, zoom);

                DrawHandle(painter, point, Screen(element, shape, anchor.Position + anchor.InHandle, zoom),
                    anchor.InHandle);
                DrawHandle(painter, point, Screen(element, shape, anchor.Position + anchor.OutHandle, zoom),
                    anchor.OutHandle);

                var isSelected = _selected.Found && _selected.Contour == contourIndex && _selected.Anchor == i;
                painter.fillColor = isSelected ? SelectedColor : PointColor;
                painter.BeginPath();
                painter.Arc(point, PointRadius, Angle.Degrees(0f), Angle.Degrees(360f));
                painter.Fill();
            }
        }

        private static void DrawHandle(Painter2D painter, Vector2 anchorPoint, Vector2 handlePoint, Vector2 offset)
        {
            // A zero handle is not drawn, matching the hit test: everything visible is grabbable and
            // everything grabbable is visible.
            if (offset == Vector2.zero) return;

            painter.strokeColor = HandleColor;
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(anchorPoint);
            painter.LineTo(handlePoint);
            painter.Stroke();

            painter.fillColor = HandleColor;
            painter.BeginPath();
            painter.Arc(handlePoint, HandleRadius, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.Fill();
        }
    }
}

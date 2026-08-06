using emiteat.NexUI.Vector;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor
{
    /// <summary>
    /// Maps between a vector path's own coordinates and the Designer canvas.
    /// </summary>
    /// <remarks>
    /// <see cref="emiteat.NexUI.Integrations.UGUI.NXVectorGraphic"/> draws a path by fitting its
    /// bounds onto the element's rect, so a path has no absolute position of its own - it is
    /// wherever the rect is. That is right for rendering (resizing an element scales its artwork)
    /// but wrong for editing: with a live fit, dragging one point rescales every other point, and
    /// the path appears to squirm away from the cursor.
    ///
    /// The fix is an invariant rather than a special case: after any edit the path is stored in
    /// canvas coordinates and the rect is set to its bounds, which makes the fit the identity.
    /// <see cref="Bake"/> re-establishes that invariant when the element has since been moved or
    /// resized with the ordinary tools, and <see cref="RectFor"/> restores it afterwards.
    ///
    /// A path with no area - one point, or a straight horizontal run - has no fit to speak of on
    /// the degenerate axis. Those cases keep the rect's existing extent rather than collapsing it,
    /// because a zero-height element is unselectable and there would be no way back to it.
    /// </remarks>
    public static class DesignerVectorSpace
    {
        /// <summary>Below this an axis carries no scale information and is treated as degenerate.</summary>
        private const float MinimumExtent = 0.0001f;

        /// <summary>The scale the renderer applies when fitting <paramref name="shape"/> into <paramref name="rect"/>.</summary>
        public static Vector2 ScaleFor(NexVectorShape shape, Rect rect)
        {
            if (shape == null) return Vector2.one;
            var bounds = shape.Bounds();
            return new Vector2(
                bounds.width > MinimumExtent ? rect.width / bounds.width : 1f,
                bounds.height > MinimumExtent ? rect.height / bounds.height : 1f);
        }

        /// <summary>Where a point of the path lands on the canvas.</summary>
        public static Vector2 ShapeToCanvas(NexVectorShape shape, Rect rect, Vector2 point)
        {
            if (shape == null) return point;
            var bounds = shape.Bounds();
            var scale = ScaleFor(shape, rect);
            return new Vector2(
                rect.xMin + (point.x - bounds.xMin) * scale.x,
                rect.yMin + (point.y - bounds.yMin) * scale.y);
        }

        /// <summary>Which point of the path a canvas position refers to.</summary>
        public static Vector2 CanvasToShape(NexVectorShape shape, Rect rect, Vector2 point)
        {
            if (shape == null) return point;
            var bounds = shape.Bounds();
            var scale = ScaleFor(shape, rect);
            return new Vector2(
                bounds.xMin + (point.x - rect.xMin) / (Mathf.Abs(scale.x) > MinimumExtent ? scale.x : 1f),
                bounds.yMin + (point.y - rect.yMin) / (Mathf.Abs(scale.y) > MinimumExtent ? scale.y : 1f));
        }

        /// <summary>
        /// Rewrites the path into canvas coordinates, so editing it needs no transform.
        /// </summary>
        /// <remarks>
        /// Handles scale but do not translate - they are stored relative to their anchor, so
        /// carrying the offset into them would move each one twice.
        /// </remarks>
        /// <returns>Whether any coordinate actually changed.</returns>
        public static bool Bake(NexVectorShape shape, Rect rect)
        {
            if (shape == null) return false;

            var bounds = shape.Bounds();
            var scale = ScaleFor(shape, rect);
            var offset = new Vector2(rect.xMin - bounds.xMin * scale.x, rect.yMin - bounds.yMin * scale.y);

            if (offset == Vector2.zero && scale == Vector2.one) return false;

            var changed = false;

            for (var c = 0; c < shape.Contours.Count; c++)
            {
                var anchors = shape.Contours[c]?.Anchors;
                if (anchors == null) continue;

                for (var a = 0; a < anchors.Count; a++)
                {
                    var anchor = anchors[a];
                    anchor.Position = new Vector2(
                        anchor.Position.x * scale.x + offset.x,
                        anchor.Position.y * scale.y + offset.y);
                    anchor.InHandle = Vector2.Scale(anchor.InHandle, scale);
                    anchor.OutHandle = Vector2.Scale(anchor.OutHandle, scale);
                    anchors[a] = anchor;
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>
        /// The rect an element should have so its path renders exactly where it was drawn.
        /// </summary>
        /// <param name="shape">The path, already in canvas coordinates.</param>
        /// <param name="current">The element's rect, whose extent is kept on any degenerate axis.</param>
        public static Rect RectFor(NexVectorShape shape, Rect current)
        {
            if (shape == null) return current;

            var bounds = shape.Bounds();
            var anyAnchor = false;
            for (var c = 0; c < shape.Contours.Count && !anyAnchor; c++)
                anyAnchor = shape.Contours[c] != null && shape.Contours[c].Anchors.Count > 0;

            // Nothing drawn yet: the element keeps whatever box it was created with, so a path
            // started on an existing element does not make it jump to the canvas origin.
            if (!anyAnchor) return current;

            return new Rect(
                bounds.xMin,
                bounds.yMin,
                bounds.width > MinimumExtent ? bounds.width : current.width,
                bounds.height > MinimumExtent ? bounds.height : current.height);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Viewport
{
    public readonly struct NexUISmartGuideResult
    {
        public readonly Rect Rect;
        public readonly float? VerticalGuide;
        public readonly float? HorizontalGuide;
        public readonly string DistanceLabel;

        public NexUISmartGuideResult(Rect rect, float? verticalGuide, float? horizontalGuide, string distanceLabel)
        {
            Rect = rect;
            VerticalGuide = verticalGuide;
            HorizontalGuide = horizontalGuide;
            DistanceLabel = distanceLabel;
        }
    }

    public static class NexUISmartGuideUtility
    {
        public static NexUISmartGuideResult Snap(Rect moving, IEnumerable<DesignerElementMetadata> elements, DesignerElementMetadata movingElement, float threshold)
        {
            var rect = moving;
            float? vertical = null;
            float? horizontal = null;
            var bestX = threshold;
            var bestY = threshold;
            var distanceLabel = string.Empty;

            foreach (var element in elements)
            {
                if (element == null || element == movingElement || element.hiddenInDesigner) continue;
                var target = element.rect;
                // Alignment: like edges and centres line up.
                TrySnapX(ref rect, moving, target.xMin, moving.xMin, ref bestX, ref vertical);
                TrySnapX(ref rect, moving, target.center.x, moving.center.x, ref bestX, ref vertical);
                TrySnapX(ref rect, moving, target.xMax, moving.xMax, ref bestX, ref vertical);
                // Adjacency: butting one element against another. Without these, dragging a element
                // up beside its neighbour never snaps - which is the single most common layout move.
                TrySnapX(ref rect, moving, target.xMin, moving.xMax, ref bestX, ref vertical);
                TrySnapX(ref rect, moving, target.xMax, moving.xMin, ref bestX, ref vertical);

                TrySnapY(ref rect, moving, target.yMin, moving.yMin, ref bestY, ref horizontal);
                TrySnapY(ref rect, moving, target.center.y, moving.center.y, ref bestY, ref horizontal);
                TrySnapY(ref rect, moving, target.yMax, moving.yMax, ref bestY, ref horizontal);
                TrySnapY(ref rect, moving, target.yMin, moving.yMax, ref bestY, ref horizontal);
                TrySnapY(ref rect, moving, target.yMax, moving.yMin, ref bestY, ref horizontal);

                var xGap = Gap(moving.xMin, moving.xMax, target.xMin, target.xMax);
                var yGap = Gap(moving.yMin, moving.yMax, target.yMin, target.yMax);
                if (xGap >= 0f && xGap <= threshold * 4f)
                    distanceLabel = Mathf.RoundToInt(xGap) + "px";
                else if (yGap >= 0f && yGap <= threshold * 4f)
                    distanceLabel = Mathf.RoundToInt(yGap) + "px";
            }

            return new NexUISmartGuideResult(rect, vertical, horizontal, distanceLabel);
        }

        private static void TrySnapX(ref Rect rect, Rect original, float targetEdge, float movingEdge, ref float best, ref float? guide)
        {
            var delta = targetEdge - movingEdge;
            var abs = Mathf.Abs(delta);
            if (abs > best) return;
            best = abs;
            rect.x = original.x + delta;
            guide = targetEdge;
        }

        private static void TrySnapY(ref Rect rect, Rect original, float targetEdge, float movingEdge, ref float best, ref float? guide)
        {
            var delta = targetEdge - movingEdge;
            var abs = Mathf.Abs(delta);
            if (abs > best) return;
            best = abs;
            rect.y = original.y + delta;
            guide = targetEdge;
        }

        private static float Gap(float aMin, float aMax, float bMin, float bMax)
        {
            if (aMax < bMin) return bMin - aMax;
            if (bMax < aMin) return aMin - bMax;
            return -1f;
        }
    }
}

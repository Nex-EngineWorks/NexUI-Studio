using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Viewport
{
    public enum DesignerGuideAxis
    {
        /// <summary>A vertical line at a fixed canvas X.</summary>
        Vertical,
        /// <summary>A horizontal line at a fixed canvas Y.</summary>
        Horizontal
    }

    /// <summary>One user-placed ruler guide, in unscaled canvas coordinates.</summary>
    public struct DesignerGuide : IEquatable<DesignerGuide>
    {
        public DesignerGuideAxis Axis;
        public float Position;

        public DesignerGuide(DesignerGuideAxis axis, float position)
        {
            Axis = axis;
            Position = position;
        }

        public bool Equals(DesignerGuide other)
            => Axis == other.Axis && Mathf.Approximately(Position, other.Position);

        public override bool Equals(object obj) => obj is DesignerGuide other && Equals(other);
        public override int GetHashCode() => ((int)Axis * 397) ^ Mathf.RoundToInt(Position * 100f);
    }

    /// <summary>
    /// Ruler tick spacing and guide list maths for the canvas.
    ///
    /// Pure and UI-free so the behaviour that actually matters while laying a screen out - how dense
    /// the ruler is at a given zoom, which guide the cursor grabbed, whether a drag should snap - is
    /// unit-testable without opening a window.
    /// </summary>
    public static class DesignerCanvasGuides
    {
        /// <summary>Distance in screen pixels within which a click grabs an existing guide.</summary>
        public const float GrabThresholdPixels = 5f;

        /// <summary>Dragging a guide this far into the ruler gutter deletes it.</summary>
        public const float DeleteThresholdPixels = 12f;

        private static readonly float[] NiceSteps = { 1f, 2f, 5f, 10f, 20f, 25f, 50f, 100f, 200f, 250f, 500f, 1000f, 2000f, 5000f };

        /// <summary>
        /// Canvas-space distance between ruler ticks, chosen so ticks stay at least
        /// <paramref name="minimumPixelSpacing"/> apart on screen at the current zoom. Returns a
        /// "nice" number (1/2/5 × 10ⁿ) so labels read as round values rather than 37.5.
        /// </summary>
        public static float TickStep(float zoom, float minimumPixelSpacing = 64f)
        {
            zoom = Mathf.Max(0.01f, zoom);
            minimumPixelSpacing = Mathf.Max(1f, minimumPixelSpacing);
            var required = minimumPixelSpacing / zoom;

            foreach (var step in NiceSteps)
                if (step >= required)
                    return step;
            return NiceSteps[NiceSteps.Length - 1];
        }

        /// <summary>
        /// Canvas positions of the ruler ticks visible between <paramref name="fromCanvas"/> and
        /// <paramref name="toCanvas"/>, inclusive of the first tick at or before the start.
        /// </summary>
        public static List<float> Ticks(float fromCanvas, float toCanvas, float step)
        {
            var result = new List<float>();
            if (step <= 0f || toCanvas < fromCanvas) return result;

            // Guard against a pathological range/step producing a huge list.
            var count = Mathf.FloorToInt((toCanvas - fromCanvas) / step) + 2;
            if (count > 4096) return result;

            var first = Mathf.Floor(fromCanvas / step) * step;
            for (var tick = first; tick <= toCanvas + step * 0.5f; tick += step)
                result.Add(tick);
            return result;
        }

        /// <summary>Ruler label for a tick. Integers stay integers so the ruler does not read "100.0".</summary>
        public static string TickLabel(float value)
            => Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>
        /// Adds a guide unless one already sits within <paramref name="mergeTolerance"/> canvas units
        /// on the same axis - dragging out a second guide onto an existing one is a slip, not intent.
        /// Returns true when the list actually changed.
        /// </summary>
        public static bool Add(List<DesignerGuide> guides, DesignerGuide guide, float mergeTolerance = 0.5f)
        {
            if (guides == null) return false;
            for (int i = 0; i < guides.Count; i++)
                if (guides[i].Axis == guide.Axis && Mathf.Abs(guides[i].Position - guide.Position) <= mergeTolerance)
                    return false;
            guides.Add(guide);
            return true;
        }

        /// <summary>
        /// Index of the guide under a canvas-space point, or -1. <paramref name="zoom"/> converts the
        /// pixel grab threshold into canvas units so the hit area feels the same at every zoom.
        /// </summary>
        public static int IndexAt(IReadOnlyList<DesignerGuide> guides, DesignerGuideAxis axis, float canvasPosition, float zoom)
        {
            if (guides == null) return -1;
            var tolerance = GrabThresholdPixels / Mathf.Max(0.01f, zoom);
            var best = -1;
            var bestDistance = tolerance;
            for (int i = 0; i < guides.Count; i++)
            {
                if (guides[i].Axis != axis) continue;
                var distance = Mathf.Abs(guides[i].Position - canvasPosition);
                if (distance > bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return best;
        }

        /// <summary>
        /// Snaps a moving rect's edges and centre to user guides, mirroring how
        /// <see cref="NexUISmartGuideUtility"/> snaps to other elements. Returns the adjusted rect and
        /// reports which guide lines were hit so the viewport can highlight them.
        /// </summary>
        public static Rect Snap(Rect moving, IReadOnlyList<DesignerGuide> guides, float threshold,
            out float? verticalGuide, out float? horizontalGuide)
        {
            verticalGuide = null;
            horizontalGuide = null;
            if (guides == null || guides.Count == 0) return moving;

            // Rect.x/y are properties, so the snap helpers work on plain floats and the result is
            // written back once at the end.
            var x = moving.x;
            var y = moving.y;
            var bestX = threshold;
            var bestY = threshold;

            foreach (var guide in guides)
            {
                if (guide.Axis == DesignerGuideAxis.Vertical)
                {
                    TrySnap(ref x, moving.x, guide.Position, moving.xMin, ref bestX, ref verticalGuide);
                    TrySnap(ref x, moving.x, guide.Position, moving.center.x, ref bestX, ref verticalGuide);
                    TrySnap(ref x, moving.x, guide.Position, moving.xMax, ref bestX, ref verticalGuide);
                }
                else
                {
                    TrySnap(ref y, moving.y, guide.Position, moving.yMin, ref bestY, ref horizontalGuide);
                    TrySnap(ref y, moving.y, guide.Position, moving.center.y, ref bestY, ref horizontalGuide);
                    TrySnap(ref y, moving.y, guide.Position, moving.yMax, ref bestY, ref horizontalGuide);
                }
            }

            var rect = moving;
            rect.x = x;
            rect.y = y;
            return rect;
        }

        private static void TrySnap(ref float axisValue, float originalAxisValue, float guidePosition,
            float movingEdge, ref float best, ref float? hitGuide)
        {
            var delta = guidePosition - movingEdge;
            var distance = Mathf.Abs(delta);
            if (distance > best) return;
            best = distance;
            axisValue = originalAxisValue + delta;
            hitGuide = guidePosition;
        }

        // ---- Persistence -------------------------------------------------------------------
        // Guides are an editor-local working aid, like scroll position or zoom, so they live in
        // EditorPrefs rather than the metadata asset. That keeps them out of Git diffs and avoids a
        // schema migration for something the user expects to be scratch state.

        /// <summary>Serializes to a compact "V:120|H:64" form.</summary>
        public static string Serialize(IReadOnlyList<DesignerGuide> guides)
        {
            if (guides == null || guides.Count == 0) return string.Empty;
            var parts = new List<string>(guides.Count);
            foreach (var guide in guides)
                parts.Add((guide.Axis == DesignerGuideAxis.Vertical ? "V:" : "H:") +
                          guide.Position.ToString("0.##", CultureInfo.InvariantCulture));
            return string.Join("|", parts);
        }

        /// <summary>Parses <see cref="Serialize"/> output. Malformed entries are skipped, never thrown.</summary>
        public static List<DesignerGuide> Deserialize(string raw)
        {
            var guides = new List<DesignerGuide>();
            if (string.IsNullOrEmpty(raw)) return guides;

            foreach (var part in raw.Split('|'))
            {
                if (string.IsNullOrEmpty(part) || part.Length < 3) continue;
                var axis = part[0] == 'V' ? DesignerGuideAxis.Vertical
                    : part[0] == 'H' ? DesignerGuideAxis.Horizontal
                    : (DesignerGuideAxis?)null;
                if (axis == null || part[1] != ':') continue;
                if (!float.TryParse(part.Substring(2), NumberStyles.Float, CultureInfo.InvariantCulture, out var position)) continue;
                Add(guides, new DesignerGuide(axis.Value, position));
            }
            return guides;
        }
    }
}

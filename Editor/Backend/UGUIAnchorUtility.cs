using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Backend
{
    /// <summary>
    /// Applies <see cref="DesignerAnchorPreset"/> values to a uGUI <see cref="RectTransform"/>.
    /// Shared by the live preview backend and the prefab serializer so anchoring is
    /// identical whether the user is editing in the viewport or saving to disk.
    /// </summary>
    public static class UGUIAnchorUtility
    {
        /// <summary>
        /// Sets the anchor / pivot for the given preset. For non-stretch presets the
        /// current size (sizeDelta) is preserved; Stretch clears offsets so the element
        /// fills its parent.
        /// </summary>
        public static void Apply(RectTransform rt, DesignerAnchorPreset preset)
        {
            if (rt == null) return;

            var size = rt.rect.size;
            var anchoredPosition = rt.anchoredPosition;

            switch (preset)
            {
                case DesignerAnchorPreset.TopLeft: Set(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f)); break;
                case DesignerAnchorPreset.Top: Set(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f)); break;
                case DesignerAnchorPreset.TopRight: Set(rt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f)); break;
                case DesignerAnchorPreset.Left: Set(rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f)); break;
                case DesignerAnchorPreset.Center: Set(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)); break;
                case DesignerAnchorPreset.Right: Set(rt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f)); break;
                case DesignerAnchorPreset.BottomLeft: Set(rt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f)); break;
                case DesignerAnchorPreset.Bottom: Set(rt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)); break;
                case DesignerAnchorPreset.BottomRight: Set(rt, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f)); break;
                case DesignerAnchorPreset.Stretch:
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    return;
            }

            // Preserve visual size / position for non-stretch presets.
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPosition;
        }

        /// <summary>
        /// The preset an existing <see cref="RectTransform"/> corresponds to, for Prefab Import.
        /// </summary>
        /// <remarks>
        /// Only the nine corner/edge presets and full stretch are recognised. A hand-anchored rect
        /// (say, horizontal stretch with a fixed height) has no preset that describes it, so it is
        /// reported as TopLeft with <paramref name="exact"/> false rather than being silently
        /// re-anchored - the importer preserves the geometry and tells the user the anchoring is
        /// approximated.
        /// </remarks>
        public static DesignerAnchorPreset Detect(RectTransform rt, out bool exact)
        {
            exact = true;
            if (rt == null) return DesignerAnchorPreset.TopLeft;

            var min = rt.anchorMin;
            var max = rt.anchorMax;

            if (Approximately(min, Vector2.zero) && Approximately(max, Vector2.one))
                return DesignerAnchorPreset.Stretch;

            if (!Approximately(min, max)) { exact = false; return DesignerAnchorPreset.TopLeft; }

            if (Approximately(min, new Vector2(0f, 1f))) return DesignerAnchorPreset.TopLeft;
            if (Approximately(min, new Vector2(0.5f, 1f))) return DesignerAnchorPreset.Top;
            if (Approximately(min, new Vector2(1f, 1f))) return DesignerAnchorPreset.TopRight;
            if (Approximately(min, new Vector2(0f, 0.5f))) return DesignerAnchorPreset.Left;
            if (Approximately(min, new Vector2(0.5f, 0.5f))) return DesignerAnchorPreset.Center;
            if (Approximately(min, new Vector2(1f, 0.5f))) return DesignerAnchorPreset.Right;
            if (Approximately(min, Vector2.zero)) return DesignerAnchorPreset.BottomLeft;
            if (Approximately(min, new Vector2(0.5f, 0f))) return DesignerAnchorPreset.Bottom;
            if (Approximately(min, new Vector2(1f, 0f))) return DesignerAnchorPreset.BottomRight;

            exact = false;
            return DesignerAnchorPreset.TopLeft;
        }

        private static bool Approximately(Vector2 a, Vector2 b)
            => Mathf.Abs(a.x - b.x) < 0.001f && Mathf.Abs(a.y - b.y) < 0.001f;

        private static void Set(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
        }
    }
}

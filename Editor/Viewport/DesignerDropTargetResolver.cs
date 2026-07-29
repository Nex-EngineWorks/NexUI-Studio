using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Components;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Viewport
{
    /// <summary>
    /// Decides which container a dragged element would drop into on the canvas.
    ///
    /// Unity's Hierarchy reparents by dragging a row onto another row; on a canvas the equivalent is
    /// dragging an element over the container it should live in. The rules below are what make that
    /// feel predictable instead of surprising, so they live here as pure functions rather than inside
    /// the viewport's pointer handlers.
    /// </summary>
    public static class DesignerDropTargetResolver
    {
        /// <summary>
        /// The container under <paramref name="canvasPoint"/> that <paramref name="dragged"/> may be
        /// dropped into, or null for the screen root.
        ///
        /// Rules, in order:
        /// <list type="number">
        /// <item>Only elements that accept authored children are candidates.</item>
        /// <item>The dragged elements themselves and anything below them are excluded - dropping a
        /// node into its own subtree is the classic way to lose a branch.</item>
        /// <item>Editor-hidden and locked elements are skipped; you cannot see or edit them, so
        /// silently parenting into them would be a trap.</item>
        /// <item>The <b>deepest</b> containing candidate wins, so dropping onto a card inside a panel
        /// picks the card rather than the panel behind it.</item>
        /// <item>Ties at the same depth are broken by draw order (last drawn = on top).</item>
        /// </list>
        /// </summary>
        public static DesignerElementMetadata Resolve(DesignerMetadataAsset asset, Vector2 canvasPoint,
            IReadOnlyList<DesignerElementMetadata> dragged)
        {
            if (asset == null) return null;

            DesignerElementMetadata best = null;
            var bestDepth = -1;
            var bestIndex = -1;

            for (int i = 0; i < asset.elements.Count; i++)
            {
                var candidate = asset.elements[i];
                if (!IsCandidate(asset, candidate, dragged)) continue;
                if (!candidate.rect.Contains(canvasPoint)) continue;

                var depth = DesignerHierarchyUtility.GetDepth(asset, candidate);
                if (depth < bestDepth) continue;
                if (depth == bestDepth && i < bestIndex) continue;

                best = candidate;
                bestDepth = depth;
                bestIndex = i;
            }
            return best;
        }

        private static bool IsCandidate(DesignerMetadataAsset asset, DesignerElementMetadata candidate,
            IReadOnlyList<DesignerElementMetadata> dragged)
        {
            if (candidate == null || string.IsNullOrEmpty(candidate.elementId)) return false;
            if (candidate.hiddenInDesigner || candidate.locked) return false;
            if (!DesignerComponentRegistry.CanHaveChildren(candidate.elementType)) return false;

            if (dragged != null)
            {
                foreach (var mover in dragged)
                {
                    if (mover == null) continue;
                    if (mover == candidate || mover.elementId == candidate.elementId) return false;
                    if (DesignerHierarchyUtility.IsDescendant(asset, candidate.elementId, mover.elementId)) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Whether dropping into <paramref name="target"/> would actually change anything. Re-parenting
        /// an element into the parent it already has is a no-op, and reporting it as a move would put a
        /// pointless entry in the Undo history.
        /// </summary>
        public static bool WouldChangeParent(IReadOnlyList<DesignerElementMetadata> dragged, DesignerElementMetadata target)
        {
            if (dragged == null || dragged.Count == 0) return false;
            var targetId = target != null ? target.elementId : string.Empty;

            foreach (var mover in dragged)
            {
                if (mover == null) continue;
                var currentParent = mover.parentId ?? string.Empty;
                if (currentParent != targetId) return true;
            }
            return false;
        }

        /// <summary>Human label for the drop hint shown while dragging.</summary>
        public static string Describe(DesignerElementMetadata target)
            => target == null ? "Move to screen root" : $"Drop into '{target.elementId}'";
    }
}

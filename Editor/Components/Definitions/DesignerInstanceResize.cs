using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Components.Definitions
{
    /// <summary>
    /// Propagates an instance's size onto the elements a component definition contributes.
    /// </summary>
    /// <remarks>
    /// Expansion used to translate the definition sub-tree and nothing more, so resizing an instance
    /// moved only its root: a 200×100 card stretched to 400×100 kept its 200-wide background and left
    /// the right half empty. The definition author had no way to express intent either, which is why
    /// the documented workaround was "put an Auto Layout on the definition root".
    ///
    /// The intent is already authored, on each element, as its <see cref="DesignerAnchorPreset"/>.
    /// Reading it as a constraint is the same rule uGUI applies to anchors and the same one Figma
    /// applies to constraints, so what the canvas shows and what the prefab does agree by construction
    /// rather than by a second heuristic:
    /// <list type="bullet">
    /// <item>An edge anchor keeps its distance to that edge, and the element keeps its size.</item>
    /// <item>A centre anchor keeps its offset from the centre, and the element keeps its size.</item>
    /// <item><see cref="DesignerAnchorPreset.Stretch"/> keeps both margins, so the element grows.</item>
    /// </list>
    ///
    /// An element whose own Auto Layout is enabled positions its children itself. Scaling them here as
    /// well would compute a placement the layout then overwrites, so the recursion stops there.
    /// Everything is pure: rects in, rects out, no asset access.
    /// </remarks>
    public static class DesignerInstanceResize
    {
        /// <summary>
        /// Rewrites every element under <paramref name="rootElementId"/> for a root that changed from
        /// <paramref name="oldRootSize"/> to its current size.
        /// </summary>
        /// <param name="elements">
        /// The expanded sub-tree, root included. The root's rect is already the instance's; only its
        /// descendants are rewritten.
        /// </param>
        public static void Apply(IReadOnlyList<DesignerElementMetadata> elements, string rootElementId,
            Vector2 oldRootSize)
        {
            if (elements == null || elements.Count == 0 || string.IsNullOrEmpty(rootElementId)) return;

            var root = Find(elements, rootElementId);
            if (root == null) return;

            // A definition authored with a zero-sized root has no ratio to resize against, and an
            // instance that was never resized must stay byte-identical to what earlier builds produced.
            if (oldRootSize.x <= 0f || oldRootSize.y <= 0f) return;
            if (Mathf.Approximately(oldRootSize.x, root.rect.width) &&
                Mathf.Approximately(oldRootSize.y, root.rect.height)) return;

            var childrenByParent = new Dictionary<string, List<DesignerElementMetadata>>();
            foreach (var element in elements)
            {
                if (element == null || string.IsNullOrEmpty(element.parentId)) continue;
                if (!childrenByParent.TryGetValue(element.parentId, out var list))
                    childrenByParent[element.parentId] = list = new List<DesignerElementMetadata>();
                list.Add(element);
            }

            var oldRootRect = new Rect(root.rect.position, oldRootSize);
            Descend(root, oldRootRect, childrenByParent);
        }

        private static void Descend(DesignerElementMetadata parent, Rect parentOldRect,
            Dictionary<string, List<DesignerElementMetadata>> childrenByParent)
        {
            if (parent.autoLayout != null && parent.autoLayout.enabled) return;
            if (!childrenByParent.TryGetValue(parent.elementId, out var children)) return;

            foreach (var child in children)
            {
                var childOldRect = child.rect;
                child.rect = Resize(childOldRect, parentOldRect, parent.rect, child.anchorPreset);
                Descend(child, childOldRect, childrenByParent);
            }
        }

        /// <summary>
        /// Where <paramref name="childRect"/> lands when its parent goes from
        /// <paramref name="parentOld"/> to <paramref name="parentNew"/>. Pure; exposed for tests.
        /// </summary>
        public static Rect Resize(Rect childRect, Rect parentOld, Rect parentNew, DesignerAnchorPreset preset)
        {
            var (minX, maxX) = HorizontalAnchors(preset);
            var (minY, maxY) = VerticalAnchors(preset);

            var (x, width) = ResizeAxis(childRect.x - parentOld.x, childRect.width,
                parentOld.width, parentNew.width, minX, maxX);
            var (y, height) = ResizeAxis(childRect.y - parentOld.y, childRect.height,
                parentOld.height, parentNew.height, minY, maxY);

            return new Rect(parentNew.x + x, parentNew.y + y, width, height);
        }

        /// <summary>
        /// One axis of the constraint. <paramref name="anchorMin"/> and <paramref name="anchorMax"/> are
        /// fractions of the parent's extent, exactly like uGUI's anchorMin/anchorMax.
        /// </summary>
        private static (float Offset, float Size) ResizeAxis(float offset, float size,
            float parentOldSize, float parentNewSize, float anchorMin, float anchorMax)
        {
            var delta = parentNewSize - parentOldSize;

            // Stretched: both margins are fixed, so the element absorbs the whole change. Clamping at
            // zero keeps a shrunken parent from producing a negative-width rect, which every consumer
            // downstream would have to defend against.
            if (!Mathf.Approximately(anchorMin, anchorMax))
                return (offset, Mathf.Max(0f, size + delta));

            // Pinned: the distance from the anchor line is what stays constant.
            return (offset + delta * anchorMin, size);
        }

        private static (float Min, float Max) HorizontalAnchors(DesignerAnchorPreset preset)
        {
            switch (preset)
            {
                case DesignerAnchorPreset.TopLeft:
                case DesignerAnchorPreset.Left:
                case DesignerAnchorPreset.BottomLeft:
                    return (0f, 0f);
                case DesignerAnchorPreset.TopRight:
                case DesignerAnchorPreset.Right:
                case DesignerAnchorPreset.BottomRight:
                    return (1f, 1f);
                case DesignerAnchorPreset.Stretch:
                    return (0f, 1f);
                default:
                    return (0.5f, 0.5f);   // Top, Center, Bottom
            }
        }

        /// <summary>
        /// Vertical fractions measured from the <b>top</b>, because Designer rects use a top-left
        /// origin with y growing downward. Getting this backwards would make every bottom-anchored
        /// element drift the wrong way, and only on resize - the kind of bug that survives review.
        /// </summary>
        private static (float Min, float Max) VerticalAnchors(DesignerAnchorPreset preset)
        {
            switch (preset)
            {
                case DesignerAnchorPreset.TopLeft:
                case DesignerAnchorPreset.Top:
                case DesignerAnchorPreset.TopRight:
                    return (0f, 0f);
                case DesignerAnchorPreset.BottomLeft:
                case DesignerAnchorPreset.Bottom:
                case DesignerAnchorPreset.BottomRight:
                    return (1f, 1f);
                case DesignerAnchorPreset.Stretch:
                    return (0f, 1f);
                default:
                    return (0.5f, 0.5f);   // Left, Center, Right
            }
        }

        private static DesignerElementMetadata Find(IReadOnlyList<DesignerElementMetadata> elements, string elementId)
        {
            for (var i = 0; i < elements.Count; i++)
                if (elements[i] != null && elements[i].elementId == elementId) return elements[i];
            return null;
        }
    }
}

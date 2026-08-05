using System;
using System.Collections.Generic;
using emiteat.NexUI.Designer;
using emiteat.NexUI.Diagnostics;
using UnityEngine;

namespace emiteat.NexUI.Integrations.Figma
{
    /// <summary>What an import did, so the caller can report it without re-deriving anything.</summary>
    public readonly struct FigmaImportResult
    {
        public readonly int ElementCount;
        public readonly string FrameName;
        public readonly FigmaJsonShape Shape;

        /// <summary>Roots the input offered. Above 1 means only the first was imported.</summary>
        public readonly int AvailableRoots;

        public FigmaImportResult(int elementCount, string frameName, FigmaJsonShape shape, int availableRoots)
        {
            ElementCount = elementCount;
            FrameName = frameName;
            Shape = shape;
            AvailableRoots = availableRoots;
        }
    }

    /// <summary>Maps a Figma frame into Designer metadata without writing backend assets.</summary>
    /// <remarks>
    /// The mapping is shared by both import routes on purpose. Dev Mode's "Copy as JSON" and the
    /// REST API differ only in the wrapper around the node - <see cref="FigmaJsonReader"/> strips
    /// that - so a fix to how Auto Layout or fills are read lands on both at once.
    /// </remarks>
    public static class FigmaDocumentImporter
    {
        [Serializable] private sealed class FigmaNode
        {
            public string id;
            public string name;
            public string type;
            public string characters;
            public string layoutMode;
            public float itemSpacing;
            public float paddingLeft;
            public float paddingRight;
            public float paddingTop;
            public float paddingBottom;
            public FigmaRect absoluteBoundingBox;
            public FigmaStyle style;
            public FigmaPaint[] fills;
            public FigmaNode[] children;

            // Plugin and Dev Mode exports frequently carry geometry on the node instead of in
            // absoluteBoundingBox. Reading both is what keeps a pasted node from being rejected
            // for "no bounds" when the coordinates are right there.
            public float x;
            public float y;
            public float width;
            public float height;
        }
        [Serializable] private sealed class FigmaRect { public float x; public float y; public float width; public float height; }
        [Serializable] private sealed class FigmaStyle { public float fontSize; }
        [Serializable] private sealed class FigmaPaint { public string type; public bool visible = true; public float opacity = 1f; public FigmaColor color; }
        [Serializable] private sealed class FigmaColor { public float r; public float g; public float b; public float a = 1f; }

        /// <summary>
        /// The scope every Figma diagnostic is raised under.
        /// </summary>
        /// <remarks>
        /// An imported screen fails validation for reasons that look identical to a hand-authored
        /// one - a missing binding target reads the same either way - and the fix is not the same.
        /// Attributing them to the import is what tells the two apart afterwards.
        /// </remarks>
        public static IDisposable DiagnosticScope(NexDiagnosticBag diagnostics)
            => diagnostics?.Scope(NexDiagnosticFeatures.FigmaImport, nameof(FigmaDocumentImporter));

        /// <summary>Replaces <paramref name="target"/>'s elements with the first frame in the JSON.</summary>
        /// <remarks>
        /// Accepts any shape <see cref="FigmaJsonReader"/> recognises: a REST file response, a REST
        /// nodes response, a single copied node, or an array of copied nodes.
        /// </remarks>
        public static FigmaImportResult Import(string json, DesignerMetadataAsset target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            var source = FigmaJsonReader.Read(json);
            if (!source.IsValid)
                throw new InvalidOperationException(
                    "This does not look like Figma JSON. Use Dev Mode's \"Copy as JSON\", or a response from the Figma REST API.");

            var root = JsonUtility.FromJson<FigmaNode>(source.RootNodeJson);
            var frame = SelectFrame(root);
            if (frame == null)
                throw new InvalidOperationException(
                    "The Figma JSON contains no frame with bounds. Copy a frame or a group rather than a page.");

            var origin = Bounds(frame);
            var imported = new List<DesignerElementMetadata>();
            var usedIds = new HashSet<string>(StringComparer.Ordinal);
            Convert(frame, null, origin.x, origin.y, imported, usedIds);

            target.elements.Clear();
            target.elements.AddRange(imported);
            target.schemaVersion = DesignerMetadataAsset.CurrentSchemaVersion;
            if (string.IsNullOrWhiteSpace(target.screenId)) target.screenId = SafeId(frame.name, "FigmaScreen");

            return new FigmaImportResult(imported.Count, frame.name, source.Shape, source.AvailableRoots);
        }

        /// <summary>Import count only. Kept for callers that predate <see cref="FigmaImportResult"/>.</summary>
        public static int ImportFirstFrame(string json, DesignerMetadataAsset target)
            => Import(json, target).ElementCount;

        /// <summary>
        /// The node to treat as the screen root.
        /// </summary>
        /// <remarks>
        /// A pasted node is usually already the frame, so it is checked before descending. Searching
        /// first would walk past the thing the user actually selected and import a child of it.
        /// </remarks>
        private static FigmaNode SelectFrame(FigmaNode root)
        {
            if (root == null) return null;
            if (HasBounds(root) && IsContainer(root.type)) return root;

            var found = FindFirst(root, "FRAME") ?? FirstVisualChild(root);
            if (found != null) return found;

            return HasBounds(root) ? root : null;
        }

        private static bool IsContainer(string type)
            => type == "FRAME" || type == "GROUP" || type == "COMPONENT"
               || type == "COMPONENT_SET" || type == "INSTANCE" || type == "SECTION";

        private static void Convert(FigmaNode node, string parentId, float originX, float originY,
            List<DesignerElementMetadata> output, HashSet<string> usedIds)
        {
            if (!HasBounds(node)) return;
            var bounds = Bounds(node);
            var id = UniqueId(SafeId(node.name, node.type ?? "Element"), usedIds);
            var element = new DesignerElementMetadata
            {
                elementId = id,
                displayName = string.IsNullOrWhiteSpace(node.name) ? id : node.name,
                parentId = parentId,
                siblingIndex = output.Count,
                elementType = TypeOf(node),
                rect = new Rect(bounds.x - originX, bounds.y - originY,
                    Mathf.Max(1f, bounds.width), Mathf.Max(1f, bounds.height)),
                text = node.characters ?? string.Empty,
                fontSize = node.style != null && node.style.fontSize > 0f ? Mathf.RoundToInt(node.style.fontSize) : 14,
                tint = FillColor(node, new Color(.15f, .22f, .34f, 1f))
            };
            if (element.elementType == "Label") element.textColor = element.tint;
            if (node.layoutMode == "HORIZONTAL" || node.layoutMode == "VERTICAL")
            {
                element.autoLayout.enabled = true;
                element.autoLayout.direction = node.layoutMode == "HORIZONTAL"
                    ? DesignerAutoLayoutDirection.Row : DesignerAutoLayoutDirection.Column;
                element.autoLayout.spacing = node.itemSpacing;
                element.autoLayout.paddingLeft = node.paddingLeft;
                element.autoLayout.paddingRight = node.paddingRight;
                element.autoLayout.paddingTop = node.paddingTop;
                element.autoLayout.paddingBottom = node.paddingBottom;
            }
            output.Add(element);

            if (node.children == null) return;
            for (var i = 0; i < node.children.Length; i++)
            {
                var before = output.Count;
                Convert(node.children[i], id, originX, originY, output, usedIds);
                if (output.Count > before) output[before].siblingIndex = i;
            }
        }

        private static string TypeOf(FigmaNode node)
        {
            switch (node.type)
            {
                case "TEXT": return "Label";
                case "VECTOR":
                case "ELLIPSE":
                case "LINE":
                case "BOOLEAN_OPERATION": return "Image";
                case "COMPONENT":
                case "INSTANCE": return "Card";
                case "GROUP": return "Container";
                default: return "Panel";
            }
        }

        private static Color FillColor(FigmaNode node, Color fallback)
        {
            if (node.fills == null) return fallback;
            foreach (var fill in node.fills)
                if (fill != null && fill.visible && fill.type == "SOLID" && fill.color != null)
                    return new Color(fill.color.r, fill.color.g, fill.color.b,
                        Mathf.Clamp01(fill.color.a * fill.opacity));
            return fallback;
        }

        /// <summary>
        /// Geometry of a node, from <c>absoluteBoundingBox</c> or the node's own x/y/width/height.
        /// </summary>
        private static FigmaRect Bounds(FigmaNode node)
        {
            if (node == null) return null;
            if (node.absoluteBoundingBox != null && node.absoluteBoundingBox.width > 0f
                && node.absoluteBoundingBox.height > 0f)
                return node.absoluteBoundingBox;

            return node.width > 0f && node.height > 0f
                ? new FigmaRect { x = node.x, y = node.y, width = node.width, height = node.height }
                : null;
        }

        private static bool HasBounds(FigmaNode node) => Bounds(node) != null;

        private static FigmaNode FindFirst(FigmaNode node, string type)
        {
            if (node == null) return null;
            if (node.type == type && HasBounds(node)) return node;
            if (node.children == null) return null;
            foreach (var child in node.children)
            {
                var found = FindFirst(child, type);
                if (found != null) return found;
            }
            return null;
        }

        private static FigmaNode FirstVisualChild(FigmaNode node)
        {
            if (node?.children == null) return null;
            foreach (var child in node.children)
                if (HasBounds(child)) return child;
            foreach (var child in node.children)
            {
                var found = FirstVisualChild(child);
                if (found != null) return found;
            }
            return null;
        }

        private static string SafeId(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) value = fallback;
            var chars = value.Trim().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_') chars[i] = '_';
            var id = new string(chars).Trim('_');
            if (string.IsNullOrEmpty(id)) id = fallback;
            if (char.IsDigit(id[0])) id = "Element_" + id;
            return id;
        }

        private static string UniqueId(string baseId, HashSet<string> used)
        {
            var id = baseId;
            var suffix = 2;
            while (!used.Add(id)) id = baseId + "_" + suffix++;
            return id;
        }
    }
}

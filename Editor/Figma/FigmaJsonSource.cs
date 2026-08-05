using System;
using System.Text;

namespace emiteat.NexUI.Integrations.Figma
{
    /// <summary>What a piece of Figma JSON turned out to be.</summary>
    public enum FigmaJsonShape
    {
        /// <summary>Not recognisable as Figma JSON.</summary>
        Unknown,

        /// <summary><c>GET /v1/files/{key}</c> - the whole file under a <c>document</c> key.</summary>
        FileResponse,

        /// <summary><c>GET /v1/files/{key}/nodes?ids=</c> - selected nodes keyed by id.</summary>
        NodesResponse,

        /// <summary>One node object, which is what Dev Mode's "Copy as JSON" produces.</summary>
        SingleNode,

        /// <summary>An array of nodes - a multi-selection copy.</summary>
        NodeArray
    }

    /// <summary>Result of reducing arbitrary Figma JSON to one root node.</summary>
    public readonly struct FigmaJsonSource
    {
        /// <summary>JSON of the single node to import. Empty when <see cref="Shape"/> is Unknown.</summary>
        public readonly string RootNodeJson;

        public readonly FigmaJsonShape Shape;

        /// <summary>How many nodes the input offered. Above 1 means the rest were ignored.</summary>
        public readonly int AvailableRoots;

        public FigmaJsonSource(string rootNodeJson, FigmaJsonShape shape, int availableRoots)
        {
            RootNodeJson = rootNodeJson;
            Shape = shape;
            AvailableRoots = availableRoots;
        }

        public bool IsValid => Shape != FigmaJsonShape.Unknown && !string.IsNullOrEmpty(RootNodeJson);

        /// <summary>One line describing what was recognised, for the import window's status area.</summary>
        public string Describe()
        {
            switch (Shape)
            {
                case FigmaJsonShape.FileResponse:
                    return "Figma REST file response (document tree).";
                case FigmaJsonShape.NodesResponse:
                    return AvailableRoots > 1
                        ? $"Figma REST nodes response with {AvailableRoots} nodes - the first one is used."
                        : "Figma REST nodes response.";
                case FigmaJsonShape.SingleNode:
                    return "A single Figma node (Dev Mode \"Copy as JSON\").";
                case FigmaJsonShape.NodeArray:
                    return AvailableRoots > 1
                        ? $"{AvailableRoots} copied Figma nodes - the first one is used."
                        : "One copied Figma node.";
                default:
                    return "Not recognised as Figma JSON.";
            }
        }
    }

    /// <summary>
    /// Reduces the several JSON shapes Figma hands out to the one thing the importer wants: a
    /// single node object.
    /// </summary>
    /// <remarks>
    /// The REST API and Dev Mode's clipboard disagree about the wrapper, not about the nodes -
    /// a FRAME is the same object either way. Normalising here is what lets JSON import and API
    /// import share one mapper instead of drifting into two that handle Auto Layout differently.
    ///
    /// This is a scanner, not a JSON parser. It only needs to find one value by key at the top
    /// level, and <see cref="UnityEngine.JsonUtility"/> - which cannot express a dictionary with
    /// arbitrary keys, and so cannot read the <c>nodes</c> response at all - does the rest.
    /// Writing a full parser to solve a brace-matching problem would be the larger mistake.
    /// </remarks>
    public static class FigmaJsonReader
    {
        /// <summary>Finds the node to import. Never throws; check <see cref="FigmaJsonSource.IsValid"/>.</summary>
        public static FigmaJsonSource Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new FigmaJsonSource(null, FigmaJsonShape.Unknown, 0);

            var text = json.Trim();

            if (text[0] == '[')
            {
                var elements = TopLevelArrayElements(text);
                return elements.Count == 0
                    ? new FigmaJsonSource(null, FigmaJsonShape.Unknown, 0)
                    : new FigmaJsonSource(elements[0], FigmaJsonShape.NodeArray, elements.Count);
            }

            if (text[0] != '{')
                return new FigmaJsonSource(null, FigmaJsonShape.Unknown, 0);

            var document = ValueOf(text, "document");
            if (document != null)
                return new FigmaJsonSource(document, FigmaJsonShape.FileResponse, 1);

            var nodes = ValueOf(text, "nodes");
            if (nodes != null && nodes.Length > 0 && nodes[0] == '{')
            {
                var entries = TopLevelObjectValues(nodes);
                if (entries.Count > 0)
                {
                    // Each entry is { document: {...}, components: {...} }; older exports put the
                    // node straight in. Accept both rather than guessing from the endpoint used.
                    var first = ValueOf(entries[0], "document") ?? entries[0];
                    return new FigmaJsonSource(first, FigmaJsonShape.NodesResponse, entries.Count);
                }
            }

            // No wrapper: Dev Mode copies the node itself. Require something node-shaped so a
            // random JSON file reports "not Figma" instead of importing zero elements.
            return LooksLikeNode(text)
                ? new FigmaJsonSource(text, FigmaJsonShape.SingleNode, 1)
                : new FigmaJsonSource(null, FigmaJsonShape.Unknown, 0);
        }

        private static bool LooksLikeNode(string objectJson)
            => ValueOf(objectJson, "type") != null
               || ValueOf(objectJson, "children") != null
               || (ValueOf(objectJson, "name") != null && ValueOf(objectJson, "id") != null);

        /// <summary>Raw JSON of a top-level key's value, or null when the key is not at this level.</summary>
        private static string ValueOf(string objectJson, string key)
        {
            var index = 1;
            var depth = 0;

            while (index < objectJson.Length)
            {
                var c = objectJson[index];

                if (c == '"' && depth == 0)
                {
                    var nameEnd = EndOfString(objectJson, index);
                    if (nameEnd < 0) return null;
                    var name = objectJson.Substring(index + 1, nameEnd - index - 1);

                    var colon = SkipWhitespace(objectJson, nameEnd + 1);
                    if (colon >= objectJson.Length || objectJson[colon] != ':') { index = nameEnd + 1; continue; }

                    var valueStart = SkipWhitespace(objectJson, colon + 1);
                    if (valueStart >= objectJson.Length) return null;
                    var valueEnd = EndOfValue(objectJson, valueStart);
                    if (valueEnd < 0) return null;

                    if (string.Equals(name, key, StringComparison.Ordinal))
                        return objectJson.Substring(valueStart, valueEnd - valueStart + 1).Trim();

                    index = valueEnd + 1;
                    continue;
                }

                if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') { depth--; if (depth < 0) return null; }
                else if (c == '"') { var end = EndOfString(objectJson, index); if (end < 0) return null; index = end; }

                index++;
            }

            return null;
        }

        private static System.Collections.Generic.List<string> TopLevelArrayElements(string arrayJson)
        {
            var result = new System.Collections.Generic.List<string>();
            var index = SkipWhitespace(arrayJson, 1);

            while (index < arrayJson.Length && arrayJson[index] != ']')
            {
                var end = EndOfValue(arrayJson, index);
                if (end < 0) break;
                result.Add(arrayJson.Substring(index, end - index + 1).Trim());
                index = SkipWhitespace(arrayJson, end + 1);
                if (index < arrayJson.Length && arrayJson[index] == ',') index = SkipWhitespace(arrayJson, index + 1);
            }

            return result;
        }

        private static System.Collections.Generic.List<string> TopLevelObjectValues(string objectJson)
        {
            var result = new System.Collections.Generic.List<string>();
            var index = SkipWhitespace(objectJson, 1);

            while (index < objectJson.Length && objectJson[index] != '}')
            {
                if (objectJson[index] != '"') break;
                var nameEnd = EndOfString(objectJson, index);
                if (nameEnd < 0) break;

                var colon = SkipWhitespace(objectJson, nameEnd + 1);
                if (colon >= objectJson.Length || objectJson[colon] != ':') break;

                var valueStart = SkipWhitespace(objectJson, colon + 1);
                var valueEnd = EndOfValue(objectJson, valueStart);
                if (valueEnd < 0) break;

                result.Add(objectJson.Substring(valueStart, valueEnd - valueStart + 1).Trim());
                index = SkipWhitespace(objectJson, valueEnd + 1);
                if (index < objectJson.Length && objectJson[index] == ',') index = SkipWhitespace(objectJson, index + 1);
            }

            return result;
        }

        /// <summary>Index of the last character of the value starting at <paramref name="start"/>.</summary>
        private static int EndOfValue(string json, int start)
        {
            if (start >= json.Length) return -1;

            switch (json[start])
            {
                case '"': return EndOfString(json, start);
                case '{': return EndOfBracket(json, start, '{', '}');
                case '[': return EndOfBracket(json, start, '[', ']');
                default:
                    var index = start;
                    while (index < json.Length && json[index] != ',' && json[index] != '}' && json[index] != ']')
                        index++;
                    return index - 1;
            }
        }

        private static int EndOfBracket(string json, int start, char open, char close)
        {
            var depth = 0;
            for (var i = start; i < json.Length; i++)
            {
                var c = json[i];
                if (c == '"') { i = EndOfString(json, i); if (i < 0) return -1; continue; }
                if (c == open) depth++;
                else if (c == close && --depth == 0) return i;
            }
            return -1;
        }

        /// <summary>Index of the closing quote of the string starting at <paramref name="start"/>.</summary>
        private static int EndOfString(string json, int start)
        {
            for (var i = start + 1; i < json.Length; i++)
            {
                if (json[i] == '\\') { i++; continue; }
                if (json[i] == '"') return i;
            }
            return -1;
        }

        private static int SkipWhitespace(string json, int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
            return index;
        }

        /// <summary>Shortens JSON for a status line without cutting mid-escape.</summary>
        public static string Preview(string json, int maxLength)
        {
            if (string.IsNullOrEmpty(json)) return string.Empty;
            var text = json.Trim();
            if (text.Length <= maxLength) return text;

            var builder = new StringBuilder(maxLength + 3);
            builder.Append(text, 0, maxLength);
            if (builder[builder.Length - 1] == '\\') builder.Length--;
            return builder.Append("...").ToString();
        }
    }
}

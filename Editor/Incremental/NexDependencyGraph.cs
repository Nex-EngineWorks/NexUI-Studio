using System.Collections.Generic;

namespace emiteat.NexUI.Designer.Editor.Incremental
{
    /// <summary>
    /// Which elements are affected when another element changes.
    /// </summary>
    /// <remarks>
    /// Edges point from the thing depended on to the things that depend on it, because that is the
    /// direction every question is asked in: "I changed this - what else is now stale?" and
    /// "I am about to delete this - what breaks?" are the same traversal.
    ///
    /// Keyed by <c>stableId</c> throughout, even though the document stores references as
    /// <c>elementId</c>. Resolving once while building means a rename does not silently rewire the
    /// graph, and it puts this on the same key as <see cref="NexDocumentRevision"/> so a change set
    /// can be fed straight into <see cref="Closure"/>.
    ///
    /// Rebuilt from the document rather than maintained incrementally. Building is a linear pass
    /// over the elements, and a graph that has to be kept in sync with every edit is a second
    /// source of truth - which is how stale-dependency bugs happen.
    /// </remarks>
    public sealed class NexDependencyGraph
    {
        private readonly Dictionary<string, HashSet<string>> _dependents;

        public int NodeCount { get; }

        public int EdgeCount { get; }

        private NexDependencyGraph(Dictionary<string, HashSet<string>> dependents, int nodeCount, int edgeCount)
        {
            _dependents = dependents;
            NodeCount = nodeCount;
            EdgeCount = edgeCount;
        }

        /// <summary>
        /// Builds the graph for a document.
        /// </summary>
        /// <remarks>
        /// Four kinds of dependency are recorded today, chosen because each one is a real way an
        /// edit to one element invalidates another:
        ///
        /// <list type="bullet">
        /// <item><b>Parent → child.</b> Moving or resizing a parent moves its children; the
        /// compiler's placement pass reads the parent's rect.</item>
        /// <item><b>Interaction target → rule owner.</b> Renaming or deleting a targeted element
        /// breaks the rule that points at it (<c>NEX-BND-4003</c>).</item>
        /// <item><b>Focus link target → linker.</b> The same, for navigation.</item>
        /// <item><b>Component definition instance → nothing yet.</b> Definitions live in separate
        /// assets, so cross-asset edges need an asset-level graph this one deliberately is not.</item>
        /// </list>
        /// </remarks>
        public static NexDependencyGraph Build(DesignerMetadataAsset metadata)
        {
            var dependents = new Dictionary<string, HashSet<string>>();
            if (metadata == null || metadata.elements == null)
                return new NexDependencyGraph(dependents, 0, 0);

            var elements = metadata.elements;
            var stableByElementId = new Dictionary<string, string>(elements.Count);

            for (int i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                if (element == null || string.IsNullOrEmpty(element.elementId)) continue;
                stableByElementId[element.elementId] = element.stableId;
            }

            var edges = 0;

            for (int i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                if (element == null || string.IsNullOrEmpty(element.stableId)) continue;

                if (AddEdge(dependents, stableByElementId, element.parentId, element.stableId)) edges++;

                if (element.focus != null)
                {
                    if (AddEdge(dependents, stableByElementId, element.focus.upElementId, element.stableId)) edges++;
                    if (AddEdge(dependents, stableByElementId, element.focus.downElementId, element.stableId)) edges++;
                    if (AddEdge(dependents, stableByElementId, element.focus.leftElementId, element.stableId)) edges++;
                    if (AddEdge(dependents, stableByElementId, element.focus.rightElementId, element.stableId)) edges++;
                }

                if (element.interactions == null) continue;

                for (int r = 0; r < element.interactions.Count; r++)
                {
                    var rule = element.interactions[r];
                    if (rule?.actions == null) continue;

                    for (int a = 0; a < rule.actions.Count; a++)
                    {
                        var action = rule.actions[a];
                        if (action == null) continue;
                        if (AddEdge(dependents, stableByElementId, action.targetElementId, element.stableId)) edges++;
                    }
                }
            }

            return new NexDependencyGraph(dependents, stableByElementId.Count, edges);
        }

        /// <summary>Elements that directly depend on <paramref name="stableId"/>.</summary>
        public IReadOnlyCollection<string> DirectDependents(string stableId)
        {
            if (!string.IsNullOrEmpty(stableId) && _dependents.TryGetValue(stableId, out var set))
                return set;
            return System.Array.Empty<string>();
        }

        /// <summary>
        /// The seeds plus everything that transitively depends on them - the full set of elements
        /// a consumer has to redo after those seeds changed.
        /// </summary>
        /// <remarks>
        /// Breadth-first with a visited set, so a cyclic document (which the compiler rejects, but
        /// which the authoring model can hold mid-edit) terminates instead of hanging the editor.
        /// </remarks>
        public HashSet<string> Closure(IEnumerable<string> seeds)
        {
            var result = new HashSet<string>();
            if (seeds == null) return result;

            var pending = new Queue<string>();
            foreach (var seed in seeds)
            {
                if (string.IsNullOrEmpty(seed) || !result.Add(seed)) continue;
                pending.Enqueue(seed);
            }

            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                if (!_dependents.TryGetValue(current, out var set)) continue;

                foreach (var dependent in set)
                    if (result.Add(dependent)) pending.Enqueue(dependent);
            }

            return result;
        }

        /// <summary>
        /// Convenience for the common case: what has to be redone given a change set.
        /// Returns null when everything must be redone, which the caller must handle.
        /// </summary>
        public HashSet<string> Affected(NexChangeSet changes)
            => changes.Everything ? null : Closure(changes.ElementIds);

        private static bool AddEdge(Dictionary<string, HashSet<string>> dependents,
            Dictionary<string, string> stableByElementId, string dependsOnElementId, string dependentStableId)
        {
            if (string.IsNullOrEmpty(dependsOnElementId)) return false;
            if (!stableByElementId.TryGetValue(dependsOnElementId, out var dependsOnStableId)) return false;
            if (string.IsNullOrEmpty(dependsOnStableId) || dependsOnStableId == dependentStableId) return false;

            if (!dependents.TryGetValue(dependsOnStableId, out var set))
            {
                set = new HashSet<string>();
                dependents[dependsOnStableId] = set;
            }

            return set.Add(dependentStableId);
        }
    }
}

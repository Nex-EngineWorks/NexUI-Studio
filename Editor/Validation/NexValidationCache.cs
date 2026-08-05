using System.Collections.Generic;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Designer.Editor.Validation
{
    /// <summary>
    /// Lets a validation pass reuse the element-scoped issues it produced last time.
    /// </summary>
    /// <remarks>
    /// An interface rather than a direct dependency so the service keeps working with no cache at
    /// all - passing null is the old behaviour, exactly, and is what every non-interactive caller
    /// (menus, batch validation, tests) still does.
    /// </remarks>
    public interface IDesignerElementIssueCache
    {
        /// <summary>
        /// Called once before the elements are walked. Anything the element-scoped rules read
        /// besides the element itself arrives here, so the cache can drop everything when it moves.
        /// </summary>
        void BeginPass(UIRenderBackend backend, string screenId, HashSet<string> backendNames);

        /// <summary>Previously computed issues for this element, or null when they must be recomputed.</summary>
        List<DesignerValidationIssue> TryReuse(DesignerElementMetadata element);

        /// <summary>Records freshly computed issues for reuse by the next pass.</summary>
        void Store(DesignerElementMetadata element, List<DesignerValidationIssue> issues);
    }

    /// <summary>
    /// Remembers each element's validation issues so an edit to one element does not force the
    /// expensive per-element rules to run for every other element on the screen.
    /// </summary>
    /// <remarks>
    /// Reuse is sound only because <see cref="DesignerValidationService.ValidateElement"/> is a
    /// function of one element plus the pass environment, and nothing else. That property is
    /// asserted by <c>DesignerValidationElementScopeTests</c>; if a rule there ever starts reading
    /// a second element, this cache will serve stale issues for it and those tests are the thing
    /// that should fail first.
    ///
    /// Note what is deliberately <em>not</em> consulted here: the dependency graph. An element's
    /// own issues cannot depend on its parent or on a rule that targets it, so widening the dirty
    /// set through the graph would invalidate entries that are still perfectly valid. The graph is
    /// for consumers whose results genuinely do span elements.
    ///
    /// Instance state, owned by the designer context - never static. A static cache would survive
    /// document switches and domain reloads and start answering questions about a screen that is
    /// no longer open.
    /// </remarks>
    public sealed class NexValidationCache : IDesignerElementIssueCache
    {
        private readonly Dictionary<string, List<DesignerValidationIssue>> _byStableId =
            new Dictionary<string, List<DesignerValidationIssue>>();

        private UIRenderBackend _backend;
        private string _screenId;
        private int _backendNamesFingerprint;
        private bool _environmentKnown;

        /// <summary>How many elements were served from cache during the last pass.</summary>
        public int ReusedLastPass { get; private set; }

        /// <summary>How many elements had to be recomputed during the last pass.</summary>
        public int RecomputedLastPass { get; private set; }

        public int Count => _byStableId.Count;

        /// <summary>Drops everything. Used when the document is replaced or the change is unattributable.</summary>
        public void InvalidateAll()
        {
            _byStableId.Clear();
            _environmentKnown = false;
        }

        /// <summary>Drops the entries for elements that changed.</summary>
        public void Invalidate(IEnumerable<string> stableIds)
        {
            if (stableIds == null) return;
            foreach (var id in stableIds)
                if (!string.IsNullOrEmpty(id)) _byStableId.Remove(id);
        }

        public void BeginPass(UIRenderBackend backend, string screenId, HashSet<string> backendNames)
        {
            var fingerprint = FingerprintOf(backendNames);

            // The backend asset or the target backend moved, so every element's
            // missing-backend-element verdict is potentially different. Nothing survives.
            if (!_environmentKnown || _backend != backend || _screenId != screenId ||
                _backendNamesFingerprint != fingerprint)
            {
                _byStableId.Clear();
                _backend = backend;
                _screenId = screenId;
                _backendNamesFingerprint = fingerprint;
                _environmentKnown = true;
            }

            ReusedLastPass = 0;
            RecomputedLastPass = 0;
        }

        public List<DesignerValidationIssue> TryReuse(DesignerElementMetadata element)
        {
            var key = element != null ? element.stableId : null;
            if (string.IsNullOrEmpty(key)) return null;   // No identity, no reuse.

            if (!_byStableId.TryGetValue(key, out var issues)) return null;

            ReusedLastPass++;
            return issues;
        }

        public void Store(DesignerElementMetadata element, List<DesignerValidationIssue> issues)
        {
            RecomputedLastPass++;

            var key = element != null ? element.stableId : null;
            if (string.IsNullOrEmpty(key) || issues == null) return;

            _byStableId[key] = issues;
        }

        /// <summary>
        /// Order-independent fingerprint of the backend asset's element names.
        /// </summary>
        /// <remarks>
        /// XOR of hashes plus the count, because the source is a <c>HashSet</c> with no stable
        /// order. <c>string.GetHashCode</c> is only stable within one process, which is fine: this
        /// cache is in-memory and never outlives the process either.
        /// </remarks>
        private static int FingerprintOf(HashSet<string> names)
        {
            if (names == null) return 0;

            var hash = names.Count;
            foreach (var name in names)
                if (name != null) hash ^= name.GetHashCode();
            return hash;
        }
    }
}

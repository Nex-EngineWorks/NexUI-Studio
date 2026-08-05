using System.Collections.Generic;

namespace emiteat.NexUI.Designer.Editor.Incremental
{
    /// <summary>What kind of edit happened, which decides how much work it invalidates.</summary>
    public enum NexChangeKind
    {
        /// <summary>A field on one element changed. Only that element and its dependents are affected.</summary>
        Property = 0,

        /// <summary>
        /// An element was added, removed or re-parented. The hierarchy itself changed, so anything
        /// derived from the shape of the document is stale.
        /// </summary>
        Structure = 1
    }

    /// <summary>What changed between two revisions.</summary>
    public readonly struct NexChangeSet
    {
        /// <summary>
        /// True when the caller has to redo everything. Set by a structural edit, or when the
        /// requested revision is older than the retained history.
        /// </summary>
        public bool Everything { get; }

        /// <summary>Stable ids of individually changed elements. Empty when <see cref="Everything"/>.</summary>
        public IReadOnlyCollection<string> ElementIds { get; }

        public bool IsEmpty => !Everything && (ElementIds == null || ElementIds.Count == 0);

        public NexChangeSet(bool everything, IReadOnlyCollection<string> elementIds)
        {
            Everything = everything;
            ElementIds = elementIds ?? System.Array.Empty<string>();
        }

        public static readonly NexChangeSet Nothing = new NexChangeSet(false, null);
        public static readonly NexChangeSet All = new NexChangeSet(true, null);
    }

    /// <summary>
    /// Tracks what changed in an authoring document, so work derived from it can be redone for
    /// the changed part instead of the whole thing.
    /// </summary>
    /// <remarks>
    /// This replaces the single "something changed" boolean the designer used before, which forced
    /// every consumer - validation, preview, the flattened component tree - to redo all of its
    /// work on every keystroke.
    ///
    /// Two rules make it safe to build on:
    ///
    /// <b>It over-reports, never under-reports.</b> A structural edit invalidates everything rather
    /// than trying to work out the blast radius; asking about a revision older than the retained
    /// history answers "everything". A consumer that does too much work is slow, one that does too
    /// little is wrong, and only one of those is worth risking.
    ///
    /// <b>It is keyed by <c>stableId</c>, not <c>elementId</c>.</b> A rename is an ordinary property
    /// edit, and keying by the user-facing id would make a renamed element look like a deletion
    /// plus an insertion to every consumer.
    ///
    /// Pure and Unity-free on purpose: this is the piece the incremental story rests on, so it has
    /// to be testable without an editor, a document asset or a domain reload.
    /// </remarks>
    public sealed class NexDocumentRevision
    {
        /// <summary>
        /// How many individual edits are remembered. Beyond this the tracker answers "everything"
        /// for old revisions - bounded memory is worth more than perfect precision for a consumer
        /// that fell far behind, since that consumer is about to do a full pass anyway.
        /// </summary>
        public const int HistoryLimit = 256;

        private readonly Queue<Entry> _history = new Queue<Entry>();
        private int _oldestRetainedRevision;

        /// <summary>Increases on every recorded change. Consumers store this and ask what moved since.</summary>
        public int Revision { get; private set; }

        private readonly struct Entry
        {
            public readonly int Revision;
            public readonly string ElementId;
            public readonly bool Structural;

            public Entry(int revision, string elementId, bool structural)
            {
                Revision = revision;
                ElementId = elementId;
                Structural = structural;
            }
        }

        /// <summary>Records a field edit on one element.</summary>
        public void MarkProperty(string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                // An edit we cannot attribute is an edit we must assume touched everything.
                MarkStructure();
                return;
            }

            Push(new Entry(++Revision, stableId, false));
        }

        /// <summary>Records an edit that changed the shape of the document.</summary>
        public void MarkStructure() => Push(new Entry(++Revision, null, true));

        /// <summary>
        /// Drops the history and moves past it. Used when the document is replaced wholesale -
        /// opening another screen, reverting, reloading after a domain reload - where describing
        /// the difference would be more expensive and less reliable than redoing the work.
        /// </summary>
        public void Reset()
        {
            _history.Clear();
            Revision++;
            _oldestRetainedRevision = Revision;
        }

        /// <summary>True when anything at all happened after <paramref name="revision"/>.</summary>
        public bool HasChangedSince(int revision) => revision < Revision;

        /// <summary>
        /// What changed after <paramref name="revision"/>. Pass the value of <see cref="Revision"/>
        /// captured at the end of the consumer's last pass.
        /// </summary>
        public NexChangeSet Since(int revision)
        {
            if (revision >= Revision) return NexChangeSet.Nothing;

            // The caller is asking about edits we no longer remember, so we cannot describe the
            // difference - only that there is one.
            if (revision < _oldestRetainedRevision) return NexChangeSet.All;

            var ids = new HashSet<string>();
            foreach (var entry in _history)
            {
                if (entry.Revision <= revision) continue;
                if (entry.Structural) return NexChangeSet.All;
                ids.Add(entry.ElementId);
            }

            return ids.Count == 0 ? NexChangeSet.Nothing : new NexChangeSet(false, ids);
        }

        private void Push(Entry entry)
        {
            _history.Enqueue(entry);

            while (_history.Count > HistoryLimit)
            {
                var dropped = _history.Dequeue();
                _oldestRetainedRevision = dropped.Revision;
            }
        }
    }
}

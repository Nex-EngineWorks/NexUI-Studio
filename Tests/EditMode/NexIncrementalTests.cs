using System.Linq;
using emiteat.NexUI.Designer.Editor.Incremental;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Covers the change tracker's core promise: it may report too much work, never too little.
    /// </summary>
    public sealed class NexDocumentRevisionTests
    {
        private NexDocumentRevision _revision;

        [SetUp]
        public void SetUp() => _revision = new NexDocumentRevision();

        [Test]
        public void Since_ReportsNothingWhenNothingHappened()
        {
            var mark = _revision.Revision;

            Assert.IsTrue(_revision.Since(mark).IsEmpty);
            Assert.IsFalse(_revision.HasChangedSince(mark));
        }

        [Test]
        public void Since_ReportsOnlyTheEditedElement()
        {
            var mark = _revision.Revision;
            _revision.MarkProperty("stable-a");

            var changes = _revision.Since(mark);

            Assert.IsFalse(changes.Everything);
            Assert.AreEqual(new[] { "stable-a" }, changes.ElementIds.ToArray());
        }

        [Test]
        public void Since_CollapsesRepeatedEditsToTheSameElement()
        {
            var mark = _revision.Revision;
            for (int i = 0; i < 20; i++) _revision.MarkProperty("stable-a");

            Assert.AreEqual(1, _revision.Since(mark).ElementIds.Count,
                "Dragging one element must not queue twenty units of work.");
        }

        [Test]
        public void Since_ReportsEverythingAfterAStructuralEdit()
        {
            var mark = _revision.Revision;
            _revision.MarkProperty("stable-a");
            _revision.MarkStructure();

            Assert.IsTrue(_revision.Since(mark).Everything);
        }

        [Test]
        public void Since_ReportsEverythingWhenTheEditCannotBeAttributed()
        {
            var mark = _revision.Revision;
            _revision.MarkProperty(null);

            Assert.IsTrue(_revision.Since(mark).Everything,
                "An unattributable edit must invalidate everything, not nothing.");
        }

        [Test]
        public void Since_ReportsEverythingWhenTheCallerFellBehindTheHistory()
        {
            var mark = _revision.Revision;
            for (int i = 0; i < NexDocumentRevision.HistoryLimit + 10; i++)
                _revision.MarkProperty("stable-" + i);

            Assert.IsTrue(_revision.Since(mark).Everything,
                "Beyond the retained history the tracker must admit it cannot describe the difference.");
        }

        [Test]
        public void Since_StillDescribesRecentEditsAfterTheHistoryWrapped()
        {
            for (int i = 0; i < NexDocumentRevision.HistoryLimit + 10; i++)
                _revision.MarkProperty("stable-" + i);

            var mark = _revision.Revision;
            _revision.MarkProperty("stable-recent");

            var changes = _revision.Since(mark);
            Assert.IsFalse(changes.Everything);
            Assert.AreEqual(new[] { "stable-recent" }, changes.ElementIds.ToArray());
        }

        [Test]
        public void Reset_MakesEarlierRevisionsUnanswerable()
        {
            var mark = _revision.Revision;
            _revision.MarkProperty("stable-a");
            _revision.Reset();

            Assert.IsTrue(_revision.Since(mark).Everything);
        }
    }

    /// <summary>
    /// Covers the dependency graph: the edges that exist, and that a traversal terminates on a
    /// document the authoring model can hold mid-edit.
    /// </summary>
    public sealed class NexDependencyGraphTests
    {
        private DesignerMetadataAsset _metadata;

        [TearDown]
        public void TearDown()
        {
            if (_metadata != null) Object.DestroyImmediate(_metadata);
            _metadata = null;
        }

        private DesignerMetadataAsset NewScreen()
        {
            _metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            _metadata.screenId = "TestScreen";
            return _metadata;
        }

        private static DesignerElementMetadata Element(string id, string parentId = null)
            => new DesignerElementMetadata
            {
                elementId = id,
                stableId = "stable-" + id,
                elementType = "Panel",
                parentId = parentId
            };

        [Test]
        public void Build_RecordsChildrenAsDependentsOfTheirParent()
        {
            var screen = NewScreen();
            screen.elements.Add(Element("Root"));
            screen.elements.Add(Element("Child", "Root"));

            var graph = NexDependencyGraph.Build(screen);

            CollectionAssert.Contains(graph.DirectDependents("stable-Root").ToArray(), "stable-Child");
        }

        [Test]
        public void Build_RecordsAnInteractionRuleAsDependentOnItsTarget()
        {
            var screen = NewScreen();
            var button = Element("Start");
            var rule = new DesignerInteractionRule { trigger = DesignerInteractionTrigger.OnClick };
            rule.actions.Add(new DesignerInteractionAction
            {
                kind = DesignerInteractionActionKind.SetVisible,
                targetElementId = "Title"
            });
            button.interactions.Add(rule);

            screen.elements.Add(button);
            screen.elements.Add(Element("Title"));

            var graph = NexDependencyGraph.Build(screen);

            CollectionAssert.Contains(graph.DirectDependents("stable-Title").ToArray(), "stable-Start",
                "Deleting Title must be known to break the rule on Start.");
        }

        [Test]
        public void Build_RecordsFocusLinksAsDependencies()
        {
            var screen = NewScreen();
            var a = Element("A");
            a.focus.downElementId = "B";
            screen.elements.Add(a);
            screen.elements.Add(Element("B"));

            var graph = NexDependencyGraph.Build(screen);

            CollectionAssert.Contains(graph.DirectDependents("stable-B").ToArray(), "stable-A");
        }

        [Test]
        public void Build_IgnoresReferencesToElementsThatAreNotOnTheScreen()
        {
            var screen = NewScreen();
            screen.elements.Add(Element("Orphan", "Ghost"));

            var graph = NexDependencyGraph.Build(screen);

            Assert.AreEqual(0, graph.EdgeCount);
        }

        [Test]
        public void Closure_WidensToTransitiveDependents()
        {
            var screen = NewScreen();
            screen.elements.Add(Element("Root"));
            screen.elements.Add(Element("Mid", "Root"));
            screen.elements.Add(Element("Leaf", "Mid"));

            var affected = NexDependencyGraph.Build(screen).Closure(new[] { "stable-Root" });

            CollectionAssert.AreEquivalent(new[] { "stable-Root", "stable-Mid", "stable-Leaf" }, affected.ToArray());
        }

        [Test]
        public void Closure_TerminatesOnACyclicDocument()
        {
            // The compiler rejects cycles, but the authoring model can hold one mid-edit and the
            // editor must not hang while the user is halfway through re-parenting.
            var screen = NewScreen();
            screen.elements.Add(Element("A", "B"));
            screen.elements.Add(Element("B", "A"));

            var affected = NexDependencyGraph.Build(screen).Closure(new[] { "stable-A" });

            CollectionAssert.AreEquivalent(new[] { "stable-A", "stable-B" }, affected.ToArray());
        }

        [Test]
        public void Affected_ReturnsNullWhenEverythingChanged()
        {
            var graph = NexDependencyGraph.Build(NewScreen());

            Assert.IsNull(graph.Affected(NexChangeSet.All),
                "Callers must handle 'redo everything' explicitly rather than get an empty set.");
        }
    }
}

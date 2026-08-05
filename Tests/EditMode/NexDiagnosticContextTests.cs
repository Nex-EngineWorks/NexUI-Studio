using System.Linq;
using emiteat.NexUI.Diagnostics;
using NUnit.Framework;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Feature / origin / handler attribution: that a scope stamps what it should, that nesting
    /// inherits rather than replaces, and that a mis-nested dispose cannot misattribute the rest.
    /// </summary>
    public sealed class NexDiagnosticContextTests
    {
        private NexDiagnosticBag _bag;

        [SetUp]
        public void SetUp() => _bag = new NexDiagnosticBag();

        private NexDiagnostic Add(string code = NexDiagnosticCodes.EmptyScreen, string screen = "S")
            => _bag.Add(code, new NexSourceLocation(screen));

        [Test]
        public void AScopeStampsFeatureAndRoute()
        {
            using (_bag.Scope(NexDiagnosticFeatures.UGuiSave, origin: "Serializer", handler: "HealthBar"))
            {
                var diagnostic = Add();

                Assert.AreEqual(NexDiagnosticFeatures.UGuiSave, diagnostic.Context.Feature);
                Assert.AreEqual("Serializer", diagnostic.Context.Origin);
                Assert.AreEqual("HealthBar", diagnostic.Context.Handler);
                Assert.AreEqual("Serializer -> HealthBar", diagnostic.Context.Route());
            }
        }

        [Test]
        public void OutsideAnyScopeNothingIsAttributed()
        {
            Assert.IsTrue(Add().Context.IsNone);
        }

        [Test]
        public void ScopeEndsWhenDisposed()
        {
            using (_bag.Scope(NexDiagnosticFeatures.Compile)) { }

            Assert.IsTrue(Add().Context.IsNone, "A closed scope must not keep attributing.");
        }

        [Test]
        public void NestedScopeInheritsWhatItDoesNotSet()
        {
            // The point of nesting: a writer deep inside a save names only itself, and still
            // reports under the feature and operation the save established.
            using (_bag.Scope(NexDiagnosticFeatures.UGuiSave, origin: "Save", operationId: "op-1"))
            using (_bag.Scope(handler: "Label"))
            {
                var context = Add().Context;

                Assert.AreEqual(NexDiagnosticFeatures.UGuiSave, context.Feature);
                Assert.AreEqual("Save", context.Origin);
                Assert.AreEqual("Label", context.Handler);
                Assert.AreEqual("op-1", context.OperationId);
            }
        }

        [Test]
        public void InnerScopeOverridesTheFieldsItDoesSet()
        {
            using (_bag.Scope(NexDiagnosticFeatures.Compile, origin: "Compiler"))
            using (_bag.Scope(NexDiagnosticFeatures.Accessibility))
            {
                var context = Add().Context;

                Assert.AreEqual(NexDiagnosticFeatures.Accessibility, context.Feature);
                Assert.AreEqual("Compiler", context.Origin, "Origin was not overridden, so it is inherited.");
            }
        }

        [Test]
        public void LeavingTheInnerScopeRestoresTheOuterOne()
        {
            using (_bag.Scope(NexDiagnosticFeatures.Compile))
            {
                using (_bag.Scope(NexDiagnosticFeatures.Accessibility)) { }

                Assert.AreEqual(NexDiagnosticFeatures.Compile, Add().Context.Feature);
            }
        }

        [Test]
        public void DisposingOutOfOrderDoesNotStrandAScope()
        {
            // An early return inside a using can dispose the outer scope first. Popping by depth
            // rather than by reference makes that self-correcting instead of misattributing
            // everything that follows.
            var outer = _bag.Scope(NexDiagnosticFeatures.Compile);
            var inner = _bag.Scope(NexDiagnosticFeatures.Accessibility);

            outer.Dispose();
            inner.Dispose();

            Assert.IsTrue(Add().Context.IsNone);
        }

        [Test]
        public void ADiagnosticThatAlreadyCarriesAContextKeepsIt()
        {
            // Raised elsewhere and handed here: re-stamping would credit it to whatever scope
            // happens to be open at the moment it was collected.
            var raised = NexDiagnosticCodes
                .Create(NexDiagnosticCodes.EmptyScreen)
                .WithContext(new NexDiagnosticContext(NexDiagnosticFeatures.FigmaImport, "Importer"));

            using (_bag.Scope(NexDiagnosticFeatures.Compile, origin: "Compiler"))
                _bag.Add(raised);

            Assert.AreEqual(NexDiagnosticFeatures.FigmaImport, _bag.Items[0].Context.Feature);
            Assert.AreEqual("Importer", _bag.Items[0].Context.Origin);
        }

        [Test]
        public void TheSameProblemFromTwoFeaturesIsTwoRows()
        {
            // Genuinely two problems: the same code at the same element means something different
            // when an import produced it than when a save did, and the fixes differ.
            using (_bag.Scope(NexDiagnosticFeatures.FigmaImport)) Add();
            using (_bag.Scope(NexDiagnosticFeatures.UGuiSave)) Add();

            Assert.AreEqual(2, _bag.Count);
            CollectionAssert.AreEquivalent(
                new[] { NexDiagnosticFeatures.FigmaImport, NexDiagnosticFeatures.UGuiSave },
                _bag.Features().ToArray());
        }

        [Test]
        public void TheSameProblemTwiceInOneFeatureIsStillOneRow()
        {
            using (_bag.Scope(NexDiagnosticFeatures.UGuiSave))
            {
                Add();
                Add();
            }

            Assert.AreEqual(1, _bag.Count);
            Assert.AreEqual(2, _bag.OccurrenceCount(_bag.Items[0]));
        }

        [Test]
        public void FormatGroupsByFeatureAndShowsTheRoute()
        {
            using (_bag.Scope(NexDiagnosticFeatures.FigmaImport, origin: "Importer", handler: "Frame"))
                Add(screen: "A");
            using (_bag.Scope(NexDiagnosticFeatures.UGuiSave, origin: "Serializer"))
                Add(screen: "B");
            Add(screen: "C");

            var text = _bag.Format(NexSeverity.Trace);

            StringAssert.Contains("== " + NexDiagnosticFeatures.FigmaImport + " ==", text);
            StringAssert.Contains("== " + NexDiagnosticFeatures.UGuiSave + " ==", text);
            StringAssert.Contains("Importer -> Frame", text);
            StringAssert.Contains("== Uncategorized ==", text,
                "An unattributed diagnostic must stay visible rather than being dropped.");
        }

        [Test]
        public void UnattributedDiagnosticsAreReachableByFeatureLookup()
        {
            // NexDiagnosticContext.None is `default`, so its Feature is null rather than "".
            // Comparing it against string.Empty with string.Equals is false, which is how the
            // unattributed group came to be silently dropped from Format once already.
            Add(screen: "A");

            Assert.AreEqual(1, _bag.ForFeature(null).Count());
            Assert.AreEqual(1, _bag.ForFeature(string.Empty).Count(),
                "null and empty must select the same unattributed diagnostics.");
        }

        [Test]
        public void UnattributedDiagnosticsFallBackToTheCodeSubsystem()
        {
            var log = new NexDiagnosticLog();
            log.Record(NexDiagnosticCodes.Create(NexDiagnosticCodes.EmptyScreen));

            var entry = log.All().First();
            Assert.AreEqual("DOC", entry.Feature,
                "Without a scope the subsystem is a better grouping than nothing.");
        }

        [Test]
        public void TheLogCanFilterByFeature()
        {
            var log = new NexDiagnosticLog();
            log.Record(NexDiagnosticCodes.Create(NexDiagnosticCodes.EmptyScreen)
                .WithContext(new NexDiagnosticContext(NexDiagnosticFeatures.FigmaImport)));
            log.Record(NexDiagnosticCodes.Create(NexDiagnosticCodes.ScreenIdMissing)
                .WithContext(new NexDiagnosticContext(NexDiagnosticFeatures.UGuiSave)));

            var matched = log.Query(new NexDiagnosticQuery
            {
                MinSeverity = NexSeverity.Trace,
                IncludeResolved = true,
                Feature = NexDiagnosticFeatures.UGuiSave
            }).ToList();

            Assert.AreEqual(1, matched.Count);
            Assert.AreEqual(NexDiagnosticCodes.ScreenIdMissing, matched[0].Diagnostic.Code);
        }
    }
}

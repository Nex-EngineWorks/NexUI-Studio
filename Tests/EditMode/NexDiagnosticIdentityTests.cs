using System.Linq;
using emiteat.NexUI.Diagnostics;
using NUnit.Framework;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// What makes two diagnostics the same problem.
    /// </summary>
    /// <remarks>
    /// The deduplication key was built from <c>NexSourceLocation.ToString()</c>, which is a display
    /// string: it shows the most specific subject and drops the rest, so two elements sharing a
    /// node path rendered identically. Reports on the second element were folded into the first
    /// and disappeared - taking their severity with them, which is how an error could hide behind
    /// an earlier warning on a sibling.
    ///
    /// The equivalent PlayMode tests exist in the Core suite, but PlayMode batchmode is unreliable
    /// in this environment - three attempts, one completed run. These live in EditMode so the
    /// regression is actually guarded on every run rather than on the runs that happen to finish.
    /// </remarks>
    public sealed class NexDiagnosticIdentityTests
    {
        private static NexDiagnostic At(string screen, string node, string path, string member = null)
            => new NexDiagnostic("NEX-BND-4001", NexSeverity.Warning, "boom",
                new NexSourceLocation(screen, node, path, member));

        [Test]
        public void LocationsDifferingOnlyByNodeIdAreDistinctIdentities()
        {
            // The exact shape that collapsed: same screen, same path, different element.
            var first = new NexSourceLocation("MainMenu", "n-1", "Root/Button");
            var second = new NexSourceLocation("MainMenu", "n-2", "Root/Button");

            Assert.AreEqual(first.ToString(), second.ToString(),
                "Display deliberately renders these the same - which is why it cannot be the key.");
            Assert.AreNotEqual(first.ToIdentity(), second.ToIdentity());
        }

        [Test]
        public void IdentityDistinguishesEveryField()
        {
            var baseline = new NexSourceLocation("S", "n", "p", "m").ToIdentity();

            Assert.AreNotEqual(baseline, new NexSourceLocation("S2", "n", "p", "m").ToIdentity());
            Assert.AreNotEqual(baseline, new NexSourceLocation("S", "n2", "p", "m").ToIdentity());
            Assert.AreNotEqual(baseline, new NexSourceLocation("S", "n", "p2", "m").ToIdentity());
            Assert.AreNotEqual(baseline, new NexSourceLocation("S", "n", "p", "m2").ToIdentity());
        }

        [Test]
        public void FieldsCannotBleedIntoEachOther()
        {
            // Joining without a separator would make these equal: "ab"+"c" == "a"+"bc".
            Assert.AreNotEqual(
                new NexSourceLocation("ab", "c", "", "").ToIdentity(),
                new NexSourceLocation("a", "bc", "", "").ToIdentity());
        }

        [Test]
        public void TheLogKeepsReportsOnDifferentElementsApart()
        {
            var log = new NexDiagnosticLog();
            log.Record(At("MainMenu", "n-1", "Root/Button"));
            log.Record(At("MainMenu", "n-2", "Root/Button"));

            Assert.AreEqual(2, log.Count,
                "The same rule failing on two elements is two problems; collapsing hides one.");
        }

        [Test]
        public void TheLogStillCollapsesGenuineRepeats()
        {
            var log = new NexDiagnosticLog();
            for (var i = 0; i < 5; i++) log.Record(At("MainMenu", "n-1", "Root/Button"));

            Assert.AreEqual(1, log.Count, "A rule firing repeatedly is one problem with a count.");
            Assert.AreEqual(5, log.All().First().Occurrences);
        }

        [Test]
        public void AnErrorOnASiblingIsNotHiddenByAnEarlierWarning()
        {
            // The consequence that made this worth fixing rather than just noting.
            var log = new NexDiagnosticLog();
            log.Record(new NexDiagnostic("NEX-BND-4001", NexSeverity.Warning, "first",
                new NexSourceLocation("MainMenu", "n-1", "Root/Button")));
            log.Record(new NexDiagnostic("NEX-BND-4001", NexSeverity.Error, "second",
                new NexSourceLocation("MainMenu", "n-2", "Root/Button")));

            var errors = log.Query(new NexDiagnosticQuery { MinSeverity = NexSeverity.Error }).ToList();

            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual(NexSeverity.Error, errors[0].Diagnostic.Severity);
        }

        [Test]
        public void TheBagKeepsReportsOnDifferentElementsApart()
        {
            // The bag has the same key, so it had the same defect.
            var bag = new NexDiagnosticBag();
            bag.Add(At("MainMenu", "n-1", "Root/Button"));
            bag.Add(At("MainMenu", "n-2", "Root/Button"));

            Assert.AreEqual(2, bag.Count);
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using emiteat.NexUI.Diagnostics;
using NUnit.Framework;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Keeps the published error-code catalog and the codes NexUI actually raises in step.
    /// </summary>
    /// <remarks>
    /// <see cref="NexDiagnosticCodes"/> says the catalog is generated from its entries rather than
    /// maintained by hand. No generator exists - the document is written by hand - so this test is
    /// what makes the claim true in the way that matters: a code added without a catalog entry
    /// fails here rather than reaching a user as an unexplained string in a build report.
    ///
    /// Codes are permanent, so the check runs both ways. A catalog entry with no code behind it is
    /// just as wrong: it documents a check that no longer exists, and someone will go looking for
    /// why it never fires.
    /// </remarks>
    public sealed class ErrorCodeCatalogTests
    {
        private const string CatalogPath =
            "Packages/com.nexengineworks.nexui.studio/Documentation~/reference/error-code-catalog.md";

        private static string ReadCatalog()
        {
            Assert.IsTrue(File.Exists(CatalogPath), $"{CatalogPath} not found.");
            return File.ReadAllText(CatalogPath);
        }

        [Test]
        public void EveryRaisedCodeIsDocumented()
        {
            var catalog = ReadCatalog();

            var undocumented = NexDiagnosticCodes.All
                .Select(entry => entry.Code)
                .Where(code => !catalog.Contains(code))
                .ToArray();

            CollectionAssert.IsEmpty(undocumented,
                "These codes are raised but missing from the catalog: " + string.Join(", ", undocumented));
        }

        [Test]
        public void EveryDocumentedCodeStillExists()
        {
            var known = new HashSet<string>(NexDiagnosticCodes.All.Select(entry => entry.Code));

            var documented = System.Text.RegularExpressions.Regex
                .Matches(ReadCatalog(), @"NEX-[A-Z]+-\d+")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(match => match.Value)
                .Distinct();

            var orphaned = documented.Where(code => !known.Contains(code)).ToArray();

            CollectionAssert.IsEmpty(orphaned,
                "These codes are documented but no longer raised: " + string.Join(", ", orphaned));
        }

        [Test]
        public void CodesAreUnique()
        {
            // A reused number is the one mistake the catalog cannot recover from: it silently
            // changes what an already-filed bug report meant.
            var duplicates = NexDiagnosticCodes.All
                .GroupBy(entry => entry.Code)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            CollectionAssert.IsEmpty(duplicates, "Duplicate error codes: " + string.Join(", ", duplicates));
        }

        [Test]
        public void EveryEntryExplainsWhatToDo()
        {
            var unhelpful = NexDiagnosticCodes.All
                .Where(entry => string.IsNullOrWhiteSpace(entry.Summary)
                                || string.IsNullOrWhiteSpace(entry.Resolution))
                .Select(entry => entry.Code)
                .ToArray();

            CollectionAssert.IsEmpty(unhelpful,
                "These codes have no summary or no resolution: " + string.Join(", ", unhelpful));
        }
    }
}

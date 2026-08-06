using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using emiteat.NexUI.Designer.Editor.Productivity;
using NUnit.Framework;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Keeps the supported Unity version consistent between package.json, the setup check and
    /// every document that states it.
    /// </summary>
    /// <remarks>
    /// The floor was lowered to 2022.3 in package.json while six documents and both READMEs still
    /// said 6000.4, so the product simultaneously advertised two different minimums. Nobody spots
    /// that by reading - the statements are in files that are never open at the same time - which
    /// is exactly the kind of drift a test is cheap at and review is expensive at.
    /// </remarks>
    public sealed class DocumentedVersionFloorTests
    {
        private static readonly string[] SearchRoots =
        {
            "Packages/com.nexengineworks.nexui",
            "Packages/com.nexengineworks.nexui.studio"
        };

        /// <summary>
        /// Files allowed to name the development editor rather than the supported floor.
        /// </summary>
        /// <remarks>
        /// The compatibility matrix and the changelog exist precisely to talk about specific
        /// versions, and the refactor plan is a historical record of what the values used to be.
        /// </remarks>
        private static readonly string[] Exempt =
        {
            "compatibility.md",
            "CHANGELOG.md",
            "studio-refactor-plan.md"
        };

        [Test]
        public void NoDocumentAdvertisesAHigherFloorThanThePackage()
        {
            // "6000.4 이상", "6000.4+", "Unity 6000.4 or newer" - the shapes that state a minimum.
            var claimsFloor = new Regex(@"6000\.4\s*(이상|\+|or newer)", RegexOptions.IgnoreCase);

            var offenders = SearchRoots
                .Where(Directory.Exists)
                .SelectMany(root => Directory.GetFiles(root, "*.md", SearchOption.AllDirectories))
                .Where(path => !Exempt.Any(name => Path.GetFileName(path) == name))
                .Where(path => claimsFloor.IsMatch(File.ReadAllText(path)))
                .Select(path => path.Replace('\\', '/'))
                .ToArray();

            CollectionAssert.IsEmpty(offenders,
                "These documents still state Unity 6000.4 as the minimum, but package.json supports "
                + NexUISupportedVersions.MinimumDisplay + ":\n" + string.Join("\n", offenders));
        }
    }
}

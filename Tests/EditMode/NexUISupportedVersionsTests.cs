using System.IO;
using emiteat.NexUI.Designer.Editor.Productivity;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// The supported-version floor, and that it still matches what the packages advertise.
    /// </summary>
    /// <remarks>
    /// The setup check this covers was previously hardcoded to the exact editor NexUI was developed
    /// on, so every supported 2022.3 user was told their editor was unverified. The interesting
    /// cases are therefore the supported ones, not the rejected ones.
    /// </remarks>
    public sealed class NexUISupportedVersionsTests
    {
        [TestCase("2022.3.62f3")]
        [TestCase("2022.3.0f1")]
        [TestCase("2022.4.1f1")]
        [TestCase("2023.2.5f1")]
        [TestCase("6000.0.0f1")]
        [TestCase("6000.4.2f1")]
        public void Supported(string version)
            => Assert.IsTrue(NexUISupportedVersions.IsSupported(version), version);

        [TestCase("2022.2.21f1")]
        [TestCase("2022.1.0f1")]
        [TestCase("2021.3.40f1")]
        [TestCase("2019.4.40f1")]
        public void BelowTheFloor(string version)
            => Assert.IsFalse(NexUISupportedVersions.IsSupported(version), version);

        [TestCase("")]
        [TestCase(null)]
        [TestCase("garbage")]
        [TestCase("2022")]
        [TestCase("x.y.z")]
        public void UnreadableVersionsAreNotAssumedSupported(string version)
            => Assert.IsFalse(NexUISupportedVersions.IsSupported(version), version ?? "<null>");

        [Test]
        public void PackageManifestsAgreeWithTheFloor()
        {
            // A floor that drifts from package.json is how a package ends up installable on an
            // editor whose setup check then calls it unsupported.
            foreach (var package in new[] { "com.nexengineworks.nexui", "com.nexengineworks.nexui.studio" })
            {
                var path = Path.Combine("Packages", package, "package.json");
                Assert.IsTrue(File.Exists(path), $"{path} not found.");

                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
                Assert.AreEqual(
                    $"{NexUISupportedVersions.MinimumMajor}.{NexUISupportedVersions.MinimumMinor}",
                    manifest.unity,
                    $"{package} advertises a different minimum Unity version than the setup check enforces.");
            }
        }

        [System.Serializable]
        private sealed class Manifest
        {
#pragma warning disable CS0649 // assigned by JsonUtility
            public string unity;
#pragma warning restore CS0649
        }
    }
}

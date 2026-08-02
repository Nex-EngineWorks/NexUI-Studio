using System.IO;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Serialization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    public sealed class GeneratedAssetWriterTests
    {
        private const string Folder = "Assets/NexUIGeneratedWriterTests";
        private const string Uxml = "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\"><!-- NEXUI:GENERATED --></ui:UXML>";
        private const string Uss = "/* NEXUI:GENERATED */\n#root { width: 10px; }\n";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "NexUIGeneratedWriterTests");
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void WritesBothFiles_AndSkipsIdenticalContent()
        {
            var writer = new GeneratedAssetWriter();
            var files = Files(Uxml, Uss);
            var first = writer.Write(files);
            Assert.That(first.Success, Is.True);
            Assert.That(first.ChangedPaths.Count, Is.EqualTo(2));
            var second = writer.Write(files);
            Assert.That(second.Success, Is.True);
            Assert.That(second.ChangedPaths, Is.Empty);
            Assert.That(second.UnchangedPaths.Count, Is.EqualTo(2));
        }

        [Test]
        public void InvalidSecondFile_LeavesExistingPairUnchanged()
        {
            var writer = new GeneratedAssetWriter();
            Assert.That(writer.Write(Files(Uxml, Uss)).Success, Is.True);
            var oldUxml = File.ReadAllText(Folder + "/Test.g.uxml");
            var oldUss = File.ReadAllText(Folder + "/Test.g.uss");
            var failed = writer.Write(Files(Uxml.Replace("</ui:UXML>", "<ui:VisualElement /></ui:UXML>"), "/* NEXUI:GENERATED */ #root {"));
            Assert.That(failed.Success, Is.False);
            Assert.That(File.ReadAllText(Folder + "/Test.g.uxml"), Is.EqualTo(oldUxml));
            Assert.That(File.ReadAllText(Folder + "/Test.g.uss"), Is.EqualTo(oldUss));
        }

        [Test]
        public void FileWithoutGeneratedMarker_IsNeverOverwritten()
        {
            File.WriteAllText(Folder + "/Test.g.uxml", "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\" />");
            File.WriteAllText(Folder + "/Test.g.uss", Uss);
            var result = new GeneratedAssetWriter().Write(Files(Uxml, Uss));
            Assert.That(result.Success, Is.False);
            StringAssert.DoesNotContain("NEXUI:GENERATED", File.ReadAllText(Folder + "/Test.g.uxml"));
        }

        [Test]
        public void DryRun_ReportsChangesWithoutWriting()
        {
            var result = new GeneratedAssetWriter().Write(Files(Uxml, Uss), true);
            Assert.That(result.Success, Is.True);
            Assert.That(result.ChangedPaths.Count, Is.EqualTo(2));
            Assert.That(File.Exists(Folder + "/Test.g.uxml"), Is.False);
        }

        [Test]
        public void DirectoryTraversal_IsRejectedBeforeAnyWrite()
        {
            var escaped = Folder + "/../../Escaped.g.uxml";
            var result = new GeneratedAssetWriter().Write(new[] { new GeneratedAssetFile(escaped, Uxml) });

            Assert.That(result.Success, Is.False);
            StringAssert.Contains("traverse", result.Errors[0].ToLowerInvariant());
            Assert.That(File.Exists("Assets/Escaped.g.uxml"), Is.False);
        }

        [Test]
        public void BackslashAssetPathsAreCanonicalizedForMacAndLinux()
        {
            var path = Folder.Replace('/', '\\') + "\\Portable.g.uxml";
            var result = new GeneratedAssetWriter().Write(new[] { new GeneratedAssetFile(path, Uxml) });

            Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
            CollectionAssert.Contains(result.ChangedPaths, Folder + "/Portable.g.uxml");
            Assert.That(File.Exists(Folder + "/Portable.g.uxml"), Is.True);
        }

        [Test]
        public void FileSystemContainmentHonorsHostCaseRulesAndSegmentBoundaries()
        {
            var root = Path.GetFullPath(Path.Combine("Temp", "NexUI", "Assets"));
            var child = Path.Combine(root, "UI", "Screen.uxml");
            var siblingPrefix = Path.GetFullPath(Path.Combine("Temp", "NexUI", "AssetsBackup", "Screen.uxml"));

            Assert.IsTrue(DesignerPlatformUtility.IsSameOrChildPath(root, child, System.StringComparison.Ordinal));
            Assert.IsFalse(DesignerPlatformUtility.IsSameOrChildPath(root, siblingPrefix, System.StringComparison.Ordinal));
            Assert.IsTrue(DesignerPlatformUtility.IsSameOrChildPath(
                root.ToUpperInvariant(), child.ToLowerInvariant(), System.StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(DesignerPlatformUtility.IsSameOrChildPath(
                root.ToUpperInvariant(), child.ToLowerInvariant(), System.StringComparison.Ordinal));
        }

        [Test]
        public void StructuredSaveReport_SeparatesEveryImpactCategory()
        {
            var report = new DesignerSaveReport { IsPreview = true };
            report.MarkCreated("asset", "create");
            report.MarkModified("asset", "modify");
            report.MarkSkipped("skip");
            report.MarkUnsupported("property", "unsupported");
            report.MarkPreviewOnly("property", "preview");
            report.MarkConflict("identity", "conflict");
            report.MarkOrphan("element", "orphan");
            report.MarkUserImpact("fallback", "impact");

            foreach (DesignerSaveImpactKind kind in System.Enum.GetValues(typeof(DesignerSaveImpactKind)))
                Assert.That(report.Count(kind), Is.EqualTo(1), kind.ToString());
            Assert.That(report.IsPreview, Is.True);
            Assert.That(report.HasErrors, Is.True);
            StringAssert.Contains("Save preview", report.Summary());
        }

        [Test]
        public void GeneratedControlPropertyAttributesImportAndInstantiate()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var toggle = new DesignerElementMetadata
            {
                elementId = "music", elementType = "UITK.Toggle", text = "Music",
                rect = new Rect(0, 0, 180, 28)
            };
            DesignerComponentPropertyAccess.Set(toggle, "interactable",
                new DesignerPropertyValue { type = DesignerPropertyValueType.Boolean, boolValue = false });
            DesignerComponentPropertyAccess.Set(toggle, "toggle.isOn",
                new DesignerPropertyValue { type = DesignerPropertyValueType.Boolean, boolValue = true });
            metadata.elements.Add(toggle);

            var path = Folder + "/Properties.g.uxml";
            var result = new GeneratedAssetWriter().Write(new[]
            {
                new GeneratedAssetFile(path, UIToolkitCodeGenerator.GenerateUxml(metadata))
            });

            Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            Assert.That(asset, Is.Not.Null);
            var root = asset.CloneTree();
            var control = root.Q<UnityEngine.UIElements.Toggle>("music");
            Assert.That(control, Is.Not.Null);
            Assert.That(control.value, Is.True);
            Assert.That(control.enabledSelf, Is.False);
            Object.DestroyImmediate(metadata);
        }

        private static GeneratedAssetFile[] Files(string uxml, string uss) => new[]
        {
            new GeneratedAssetFile(Folder + "/Test.g.uxml", uxml),
            new GeneratedAssetFile(Folder + "/Test.g.uss", uss)
        };
    }
}

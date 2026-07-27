using System.IO;
using emiteat.NexUI.Designer.Editor.Serialization;
using NUnit.Framework;
using UnityEditor;

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

        private static GeneratedAssetFile[] Files(string uxml, string uss) => new[]
        {
            new GeneratedAssetFile(Folder + "/Test.g.uxml", uxml),
            new GeneratedAssetFile(Folder + "/Test.g.uss", uss)
        };
    }
}

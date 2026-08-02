using System.Collections.Generic;
using System.IO;
using System.Linq;
using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor.Serialization;
using emiteat.NexUI.Designer.Editor.Validation;
using NUnit.Framework;
using UnityEditor;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    public sealed class DesignerSampleSmokeTests
    {
        private const string Source = "Packages/com.nexengineworks.nexui.studio/Samples~/DesignerSample/Screens";
        private const string Temp = "Assets/NexUIDesignerSampleSmokeTemp";
        private static readonly string[] ScreenNames = { "Settings", "Inventory", "ConfirmDialog", "Loading", "HUD" };

        [OneTimeSetUp]
        public void ImportSamples()
        {
            AssetDatabase.DeleteAsset(Temp);
            FileUtil.CopyFileOrDirectory(Source, Temp);
            foreach (var script in Directory.GetFiles(Temp, "*.cs", SearchOption.AllDirectories))
            {
                File.Delete(script);
                if (File.Exists(script + ".meta")) File.Delete(script + ".meta");
            }
            AssetDatabase.ImportAsset(Temp, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
        }

        [OneTimeTearDown]
        public void RemoveSamples()
        {
            AssetDatabase.DeleteAsset(Temp);
        }

        [TestCaseSource(nameof(ScreenCases))]
        public void Sample_LoadsGeneratesAndHasNoValidationErrors(string screenName)
        {
            var folder = Temp + "/" + screenName;
            var metadata = AssetDatabase.LoadAssetAtPath<DesignerMetadataAsset>(folder + "/" + screenName + ".Metadata.asset");
            Assert.That(metadata, Is.Not.Null, "Metadata load failed: " + screenName);
            Assert.That(metadata.elements.Where(e => e != null).GroupBy(e => e.elementId).Any(g => g.Count() > 1), Is.False);
            Assert.That(metadata.elements.Where(e => e != null && !string.IsNullOrEmpty(e.parentId)).All(e => metadata.Find(e.parentId) != null), Is.True);
            Assert.That(UIToolkitCodeGenerator.GenerateUxml(metadata), Does.Contain("NEXUI:GENERATED"));
            Assert.That(UIToolkitCodeGenerator.GenerateUss(metadata), Does.Contain("NEXUI:GENERATED"));

            foreach (var backend in new[] { "UIToolkit", "UGUI" })
            {
                var screen = AssetDatabase.LoadAssetAtPath<UIScreenDefinition>(folder + "/" + screenName + "." + backend + ".asset");
                Assert.That(screen, Is.Not.Null, $"{screenName}.{backend} failed to load");
                var issues = DesignerValidationService.Validate(screen, metadata);
                Assert.That(issues.Where(i => i.Severity == DesignerValidationSeverity.Error).ToList(), Is.Empty,
                    string.Join("\n", issues.Select(i => i.ToString())));
            }
        }

        private static IEnumerable<string> ScreenCases() => ScreenNames;
    }
}

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using emiteat.NexUI.Core;
using emiteat.NexUI.Core.Validation;
using emiteat.NexUI.Designer.Editor.AgentHandoff;
using emiteat.NexUI.Designer.Editor.BindingProfiler;
using emiteat.NexUI.Designer.Editor.Cleaner;
using emiteat.NexUI.Designer.Editor.Contracts;
using emiteat.NexUI.Designer.Editor.PromptGlyph;
using emiteat.NexUI.Designer.Editor.Responsive;
using emiteat.NexUI.Designer.Editor.Snapshot;
using emiteat.NexUI.Designer.Editor.Variants;

namespace emiteat.NexUI.Designer.Editor.Menu
{
    /// <summary>
    /// Aggregate menu commands (§26): runs the runtime <see cref="ProjectValidator"/> over
    /// all screen definitions plus every Designer-side service validation over all metadata
    /// assets, exports the AI handoff manifest, and runs snapshot baselines as tests.
    /// </summary>
    public static class AdvancedValidationMenu
    {
        [MenuItem("Tools/Nex/NexUI Studio/QA/Run Advanced Validation", priority = NexUIDesignerMenu.PriorityQa)]
        public static void RunAdvancedValidation()
        {
            var sb = new StringBuilder();
            int errors = 0, warnings = 0;

            var defs = LoadAll<UIScreenDefinition>();
            var report = new ProjectValidator().Validate(new UIValidationContext(defs));
            errors += report.ErrorCount;
            warnings += report.WarningCount;
            sb.AppendLine($"[Runtime] {report.ErrorCount} error(s), {report.WarningCount} warning(s) over {defs.Count} screen definition(s).");
            foreach (var r in report.Results)
                sb.AppendLine($"  [{r.Severity}] ({r.ValidatorId}) {r.Message}");

            foreach (var asset in LoadAll<DesignerMetadataAsset>())
            {
                var lines = new List<string>();
                lines.AddRange(VariantService.Validate(asset));
                lines.AddRange(ResponsiveService.Validate(asset));
                lines.AddRange(ContractService.Validate(asset));
                lines.AddRange(ContractService.CheckSatisfaction(asset));
                lines.AddRange(PromptGlyphService.ValidateMissing(asset));
                BindingProfilerService.Analyze(asset, out _);
                foreach (var d in CleanerService.Find(asset)) lines.Add("dead ref: " + d.description);

                if (lines.Count > 0)
                {
                    warnings += lines.Count;
                    sb.AppendLine($"[Designer] {asset.screenId} — {lines.Count} finding(s):");
                    foreach (var l in lines) sb.AppendLine("  " + l);
                }
            }

            Debug.Log("[NexUI Studio] Advanced Validation\n" + sb);
            EditorUtility.DisplayDialog("NexUI Studio Advanced Validation",
                $"{errors} error(s), {warnings} finding(s).\nSee Console for details.", "OK");
        }

        [MenuItem("Tools/Nex/NexUI Studio/QA/Export Agent Manifest", priority = NexUIDesignerMenu.PriorityQa + 3)]
        public static void ExportAgentManifest()
        {
            string folder = EditorUtility.SaveFolderPanel("Export Agent Manifest", "", "");
            if (string.IsNullOrEmpty(folder)) return;

            var manifest = AgentHandoffService.Collect();
            File.WriteAllText(Path.Combine(folder, "nexui-agent-manifest.json"), AgentHandoffService.ToJson(manifest));
            File.WriteAllText(Path.Combine(folder, "nexui-agent-brief.md"), AgentHandoffService.ToMarkdown(manifest));
            EditorUtility.RevealInFinder(folder);
            Debug.Log($"[NexUI Studio] Agent manifest exported to {folder}");
        }

        [MenuItem("Tools/Nex/NexUI Studio/QA/Run Snapshot Tests", priority = NexUIDesignerMenu.PriorityQa + 1)]
        public static void RunSnapshotTests()
        {
            var sb = new StringBuilder();
            int tested = 0, failed = 0;

            foreach (var asset in LoadAll<DesignerMetadataAsset>())
            {
                if (asset.snapshots == null || asset.snapshots.baselines.Count == 0) continue;
                tested++;
                var current = SnapshotService.Capture(asset);
                var diff = SnapshotService.Compare(asset.snapshots.baselines[0], current);
                if (diff.Count > 0)
                {
                    failed++;
                    sb.AppendLine($"[FAIL] {asset.screenId}:");
                    foreach (var d in diff) sb.AppendLine("  " + d);
                }
                else sb.AppendLine($"[PASS] {asset.screenId}");
            }

            Debug.Log($"[NexUI Studio] Snapshot Tests: {tested - failed}/{tested} passed.\n" + sb);
            EditorUtility.DisplayDialog("NexUI Snapshot Tests",
                tested == 0 ? "No baselines found." : $"{tested - failed}/{tested} passed.", "OK");
        }

        private static List<T> LoadAll<T>() where T : Object
        {
            var list = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null) list.Add(a);
            }
            return list;
        }
    }
}

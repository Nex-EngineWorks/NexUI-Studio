using System.Collections.Generic;
using System.IO;
using emiteat.NexUI.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Compiler
{
    /// <summary>
    /// Compile, publish, report - the one entry point menus, tests and the CLI all go through.
    /// </summary>
    /// <remarks>
    /// The three steps stay separately callable (a test compiles without writing anything; a
    /// preview compiles and throws the result away), but everything that publishes for real goes
    /// through here so the output path convention and the report are decided in one place rather
    /// than re-derived by each caller.
    /// </remarks>
    public static class NexScreenBuildPipeline
    {
        /// <summary>Where compiled programs live. Under Assets/ because a player build has to load them.</summary>
        public const string OutputFolder = "Assets/NexUI/Compiled";

        public struct Outcome
        {
            public NexCompileResult Compile;
            public NexPublishResult Publish;
            public string ReportPath;

            public bool Succeeded => Publish.Published;
        }

        public static string OutputPathFor(string screenId)
            => OutputFolder + "/" + (string.IsNullOrEmpty(screenId) ? "Unnamed" : screenId) + ".asset";

        public static Outcome CompileAndPublish(DesignerMetadataAsset metadata)
            => CompileAndPublish(metadata, true);

        /// <summary>
        /// Compiles a screen and publishes it if the result differs from what is already there.
        /// </summary>
        /// <param name="log">
        /// False for batch runs, which report once at the end instead of once per screen.
        /// </param>
        public static Outcome CompileAndPublish(DesignerMetadataAsset metadata, bool log)
        {
            var compile = NexScreenCompiler.Compile(metadata);
            var screenId = compile.Program != null ? compile.Program.ScreenId : null;
            var assetPath = OutputPathFor(screenId);

            EnsureFolder(OutputFolder);
            var publish = NexScreenPublisher.Publish(compile, assetPath);

            // Everything a compile found goes to the console, whether or not this caller logs.
            // A batch run reports one summary line; the per-screen detail has to survive somewhere
            // the user can still read it afterwards.
            Diagnostics.NexDiagnosticSession.Log.RecordAll(compile.Diagnostics.Items);
            if (publish.Diagnostic != null) Diagnostics.NexDiagnosticSession.Log.Record(publish.Diagnostic);

            // An unchanged screen leaves the previous report standing. Rewriting an identical
            // report would churn the file and make its timestamp meaningless as a record of when
            // the output actually last changed.
            var reportPath = publish.Skipped ? string.Empty : NexBuildReport.Write(compile, publish, assetPath);

            if (log) LogOutcome(compile, publish, assetPath, reportPath);

            return new Outcome { Compile = compile, Publish = publish, ReportPath = reportPath };
        }

        /// <summary>
        /// Reports the outcome once, at the right severity, with the cause chain intact.
        /// </summary>
        /// <remarks>
        /// Deliberately one console entry rather than one per diagnostic: a screen with 30
        /// warnings should not push everything else out of the console, and the report file holds
        /// the full list for anyone who wants it.
        /// </remarks>
        private static void LogOutcome(NexCompileResult compile, NexPublishResult publish,
            string assetPath, string reportPath)
        {
            var summary = compile.Diagnostics.Format(NexSeverity.Warning);
            var reportLine = string.IsNullOrEmpty(reportPath) ? "" : "\nReport: " + reportPath;

            if (publish.Skipped)
            {
                Debug.Log("[NexUI] " + assetPath + " is up to date; nothing was written." +
                          (string.IsNullOrEmpty(summary) ? "" : "\n\n" + summary));
                return;
            }

            if (publish.Published)
            {
                var message = "[NexUI] Published " + assetPath + " (" + compile.Program.Nodes.Length + " nodes, hash " +
                              compile.Program.ContentHash.Substring(0, 8) + ")" + reportLine;

                if (!string.IsNullOrEmpty(summary)) Debug.LogWarning(message + "\n\n" + summary);
                else Debug.Log(message);
                return;
            }

            var failure = publish.Diagnostic ?? compile.Summarize(compile.Program != null ? compile.Program.ScreenId : "");
            Debug.LogError("[NexUI] " + (failure != null ? failure.ToDetailedString() : "Publish failed.") +
                           reportLine + (string.IsNullOrEmpty(summary) ? "" : "\n\n" + summary));
        }

        /// <summary>What a batch run did, per screen.</summary>
        public struct BatchSummary
        {
            public int Published;
            public int UpToDate;
            public int Failed;
            public bool Cancelled;

            public int Total => Published + UpToDate + Failed;
        }

        /// <summary>
        /// Compiles every authoring screen in the project, publishing only the ones that changed.
        /// </summary>
        /// <remarks>
        /// This is the operation that makes the content hash worth having. Compiling one screen by
        /// hand is fast either way; compiling forty of them where two changed is the difference
        /// between two asset imports and forty.
        ///
        /// Cancellable, and cancelling is safe at any point: each screen publishes atomically and
        /// independently, so stopping half way leaves the finished ones correct and the rest
        /// exactly as they were.
        /// </remarks>
        public static BatchSummary CompileAll()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(DesignerMetadataAsset));
            var summary = new BatchSummary();
            var failures = new List<string>();

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var metadata = AssetDatabase.LoadAssetAtPath<DesignerMetadataAsset>(path);
                    if (metadata == null) continue;

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "NexUI — Compile All Screens",
                            (metadata.screenId ?? path) + "  (" + (i + 1) + "/" + guids.Length + ")",
                            guids.Length == 0 ? 1f : (float)i / guids.Length))
                    {
                        summary.Cancelled = true;
                        break;
                    }

                    var outcome = CompileAndPublish(metadata, false);

                    if (!outcome.Publish.Published)
                    {
                        summary.Failed++;
                        failures.Add(metadata.screenId + " — " +
                                     (outcome.Publish.Diagnostic != null
                                         ? outcome.Publish.Diagnostic.Code + " " + outcome.Publish.Diagnostic.Message
                                         : "compile failed"));
                    }
                    else if (outcome.Publish.Skipped) summary.UpToDate++;
                    else summary.Published++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            LogBatch(summary, failures);
            return summary;
        }

        private static void LogBatch(BatchSummary summary, List<string> failures)
        {
            var message = "[NexUI] Compile All: " + summary.Published + " published, " +
                          summary.UpToDate + " up to date, " + summary.Failed + " failed" +
                          (summary.Cancelled ? " (cancelled)" : "") + ".";

            if (summary.Failed == 0)
            {
                Debug.Log(message);
                return;
            }

            Debug.LogError(message + "\n" + string.Join("\n", failures));
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parts = folder.Split('/');
            var current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }

    /// <summary>Menu entries for the compile pipeline.</summary>
    internal static class NexCompilerMenu
    {
        private const string CompileSelected = "Tools/NexUI/Compile Selected Screen";
        private const string CompileAll = "Tools/NexUI/Compile All Screens";
        private const string OpenReports = "Tools/NexUI/Open Build Reports Folder";

        [MenuItem(CompileAll)]
        private static void DoCompileAll() => NexScreenBuildPipeline.CompileAll();

        [MenuItem(CompileSelected, true)]
        private static bool ValidateCompileSelected() => Selection.activeObject is DesignerMetadataAsset;

        [MenuItem(CompileSelected)]
        private static void DoCompileSelected()
        {
            var metadata = Selection.activeObject as DesignerMetadataAsset;
            if (metadata == null) return;

            var outcome = NexScreenBuildPipeline.CompileAndPublish(metadata);
            if (!outcome.Succeeded) return;

            var published = AssetDatabase.LoadAssetAtPath<emiteat.NexUI.Compiled.NexScreenProgram>(
                outcome.Publish.AssetPath);
            if (published != null) EditorGUIUtility.PingObject(published);
        }

        [MenuItem(OpenReports)]
        private static void DoOpenReports()
        {
            Directory.CreateDirectory(NexBuildReport.ReportDirectory);
            EditorUtility.RevealInFinder(NexBuildReport.ReportDirectory);
        }
    }
}

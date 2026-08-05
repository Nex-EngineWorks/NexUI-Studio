using System;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Compiler
{
    /// <summary>Outcome of a publish attempt.</summary>
    public struct NexPublishResult
    {
        /// <summary>True when the path now holds this program - whether it was written or already did.</summary>
        public bool Published;

        /// <summary>
        /// True when the compile produced output identical to what was already published, so
        /// nothing was written. Callers that report progress should say "up to date", not "failed".
        /// </summary>
        public bool Skipped;

        /// <summary>Asset path that now holds the program, when <see cref="Published"/>.</summary>
        public string AssetPath;

        /// <summary>Set when the publish did not happen. The previous output is intact either way.</summary>
        public NexDiagnostic Diagnostic;
    }

    /// <summary>Whether a compiled program has to be written, and why.</summary>
    public struct NexPublishDecision
    {
        public bool ShouldWrite;

        /// <summary>Short explanation, written into the log and the build report.</summary>
        public string Reason;

        public static NexPublishDecision Write(string reason)
            => new NexPublishDecision { ShouldWrite = true, Reason = reason };

        public static NexPublishDecision UpToDate(string reason)
            => new NexPublishDecision { ShouldWrite = false, Reason = reason };
    }

    /// <summary>
    /// Writes a compiled program to disk, or leaves the previous one exactly as it was.
    /// </summary>
    /// <remarks>
    /// The contract is the whole reason this is a separate type from the compiler: <b>a failed
    /// publish never degrades what is already there</b>. A half-written screen asset is worse
    /// than a stale one - stale still runs, and the user knows what version they have. So the
    /// sequence is move-aside, write, verify, delete-backup, and any failure restores the
    /// backup before reporting.
    ///
    /// Unity's asset database has no transaction, so "atomic" here means atomic from the point of
    /// view of anything that loads the asset by path: at every moment that path holds either the
    /// old valid program or the new valid program, never a partial one. The import is batched so
    /// no importer runs against an intermediate state.
    /// </remarks>
    public static class NexScreenPublisher
    {
        private const string TempSuffix = ".nex-publishing.asset";
        private const string BackupSuffix = ".nex-previous.asset";

        /// <summary>
        /// Decides whether a freshly compiled program has to replace what is already published.
        /// </summary>
        /// <remarks>
        /// The published asset is its own cache. It already carries the content hash of the
        /// compile that produced it, so asking "is this different?" needs no side-car cache file
        /// and cannot drift out of sync with reality - a whole class of stale-cache bugs that
        /// simply cannot happen here.
        ///
        /// This is deliberately a pure function of two programs: it is where the correctness of
        /// skipping lives, so it is the part that must be testable without an AssetDatabase.
        ///
        /// Note what this does <em>not</em> save. The compile still runs; only the disk write and
        /// the asset import are skipped. That is the right side to optimise - in Unity the
        /// importer is the expensive part, and re-deriving the program is what proves the hash.
        /// </remarks>
        public static NexPublishDecision Decide(NexScreenProgram existing, NexScreenProgram candidate)
        {
            if (candidate == null) return NexPublishDecision.Write("nothing compiled to compare against");
            if (existing == null) return NexPublishDecision.Write("no previously published screen");

            if (existing.CompilerVersion != candidate.CompilerVersion)
                return NexPublishDecision.Write("compiler version changed from " + existing.CompilerVersion +
                                                " to " + candidate.CompilerVersion);

            if (string.IsNullOrEmpty(existing.ContentHash))
                return NexPublishDecision.Write("published screen has no content hash");

            if (!string.Equals(existing.ContentHash, candidate.ContentHash, StringComparison.Ordinal))
                return NexPublishDecision.Write("content changed");

            return NexPublishDecision.UpToDate("content hash unchanged (" + Short(candidate.ContentHash) + ")");
        }

        private static string Short(string hash)
            => string.IsNullOrEmpty(hash) ? string.Empty : hash.Substring(0, Math.Min(8, hash.Length));

        public static NexPublishResult Publish(NexCompileResult result, string assetPath)
        {
            if (result == null || result.Program == null)
                return Fail(NexDiagnosticCodes.Create(NexDiagnosticCodes.PublishFailed, default,
                    "There is no compiled program to publish."));

            var screenId = result.Program.ScreenId;

            if (!result.Succeeded)
                return Fail(result.Summarize(screenId));

            if (string.IsNullOrEmpty(assetPath) ||
                !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !assetPath.EndsWith(".asset", StringComparison.Ordinal))
            {
                return Fail(NexDiagnosticCodes.Create(NexDiagnosticCodes.PublishPathInvalid,
                    new NexSourceLocation(screenId), detail: "Path: " + (assetPath ?? "<null>")));
            }

            var basePath = assetPath.Substring(0, assetPath.Length - ".asset".Length);
            var tempPath = basePath + TempSuffix;
            var backupPath = basePath + BackupSuffix;

            var previous = AssetDatabase.LoadAssetAtPath<NexScreenProgram>(assetPath);
            var hadPrevious = previous != null;

            var decision = Decide(previous, result.Program);
            if (!decision.ShouldWrite)
            {
                return new NexPublishResult
                {
                    Published = true,
                    Skipped = true,
                    AssetPath = assetPath
                };
            }

            try
            {
                CleanLeftovers(tempPath, backupPath);

                AssetDatabase.CreateAsset(result.Program, tempPath);
                AssetDatabase.SaveAssets();

                if (hadPrevious)
                {
                    var moveError = AssetDatabase.MoveAsset(assetPath, backupPath);
                    if (!string.IsNullOrEmpty(moveError))
                    {
                        AssetDatabase.DeleteAsset(tempPath);
                        return Fail(NexDiagnosticCodes.Create(NexDiagnosticCodes.PublishFailed,
                            new NexSourceLocation(screenId),
                            "Could not set the previous screen asset aside; nothing was changed.",
                            moveError));
                    }
                }

                var publishError = AssetDatabase.MoveAsset(tempPath, assetPath);
                if (!string.IsNullOrEmpty(publishError))
                {
                    // Put the previous output back before reporting: the user's project must look
                    // exactly as it did before the failed publish.
                    if (hadPrevious) AssetDatabase.MoveAsset(backupPath, assetPath);
                    AssetDatabase.DeleteAsset(tempPath);

                    return Fail(NexDiagnosticCodes.Create(NexDiagnosticCodes.PublishFailed,
                        new NexSourceLocation(screenId),
                        "Could not write the compiled screen; the previous version was restored.",
                        publishError));
                }

                if (hadPrevious) AssetDatabase.DeleteAsset(backupPath);

                return new NexPublishResult { Published = true, AssetPath = assetPath };
            }
            catch (Exception ex)
            {
                if (hadPrevious && AssetDatabase.LoadAssetAtPath<NexScreenProgram>(backupPath) != null &&
                    AssetDatabase.LoadAssetAtPath<NexScreenProgram>(assetPath) == null)
                    AssetDatabase.MoveAsset(backupPath, assetPath);

                AssetDatabase.DeleteAsset(tempPath);

                return Fail(NexDiagnosticCodes.Create(NexDiagnosticCodes.PublishFailed,
                    new NexSourceLocation(screenId),
                    "Publishing threw " + ex.GetType().Name + "; the previous version was restored.",
                    ex.ToString()));
            }
            finally
            {
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// Clears temp / backup files a previous crashed publish may have left behind.
        /// </summary>
        /// <remarks>
        /// If a backup survives with no live asset at the target path, the editor died between
        /// the two moves. Restoring is the right call: that backup is a program that compiled and
        /// verified, and losing it because the process was killed would be exactly the data loss
        /// this whole dance exists to prevent.
        /// </remarks>
        private static void CleanLeftovers(string tempPath, string backupPath)
        {
            if (AssetDatabase.LoadAssetAtPath<NexScreenProgram>(tempPath) != null)
                AssetDatabase.DeleteAsset(tempPath);

            var backup = AssetDatabase.LoadAssetAtPath<NexScreenProgram>(backupPath);
            if (backup == null) return;

            var target = backupPath.Substring(0, backupPath.Length - BackupSuffix.Length) + ".asset";
            if (AssetDatabase.LoadAssetAtPath<NexScreenProgram>(target) == null)
                AssetDatabase.MoveAsset(backupPath, target);
            else
                AssetDatabase.DeleteAsset(backupPath);
        }

        /// <summary>
        /// Wraps a failure, attributing it to Publish.
        /// </summary>
        /// <remarks>
        /// Stamped here rather than through a diagnostic bag scope because publishing returns a
        /// single diagnostic instead of collecting a bag. One place produces every publish failure,
        /// so one place can label them all.
        /// </remarks>
        private static NexPublishResult Fail(NexDiagnostic diagnostic)
            => new NexPublishResult
            {
                Published = false,
                Diagnostic = diagnostic?.WithContext(
                    new NexDiagnosticContext(NexDiagnosticFeatures.Publish, nameof(NexScreenPublisher)))
            };
    }
}

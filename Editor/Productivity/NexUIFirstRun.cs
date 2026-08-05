using System;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Productivity
{
    /// <summary>
    /// Decides when the setup window should appear by itself.
    /// </summary>
    /// <remarks>
    /// Kept apart from the checks on purpose. The two halves have different lifetimes: what a
    /// healthy project looks like is stable, while "how did NexUI arrive here" depends on the
    /// distribution channel and will change at least once.
    ///
    /// Today a .unitypackage import raises <c>importPackageCompleted</c>. Switching to a UPM
    /// package - which is the plan once Asset Store UPM access is available - stops raising it
    /// entirely, and only the domain-reload path below will fire. Both are wired now so that
    /// change is a packaging decision rather than a code change.
    ///
    /// The window is the only thing this opens. No browser, no download, no project modification:
    /// a package that makes any of those happen as a side effect of being imported is against the
    /// Asset Store submission rules, and is obnoxious besides.
    /// </remarks>
    [InitializeOnLoad]
    internal static class NexUIFirstRun
    {
        private const string PrefPrefix = "NexUI.Studio.FirstRunShown.";

        static NexUIFirstRun()
        {
            AssetDatabase.importPackageCompleted += OnPackageImported;

            // Covers the UPM and git-URL installs, where no package import event is ever raised.
            // delayCall rather than the static constructor itself: opening a window while the
            // domain is still reloading throws, and the check reads assets that are not loaded yet.
            EditorApplication.delayCall += Consider;
        }

        private static void OnPackageImported(string packageName) => EditorApplication.delayCall += Consider;

        /// <summary>Shows the setup window once per project per NexUI version.</summary>
        private static void Consider()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;

            var key = PreferenceKey();
            if (EditorPrefs.GetBool(key, false)) return;

            EditorPrefs.SetBool(key, true);
            NexUISetupDoctorWindow.OpenFirstRun();
        }

        /// <summary>Forgets that the window was shown, so the next reload shows it again.</summary>
        internal static void Reset() => EditorPrefs.DeleteKey(PreferenceKey());

        internal static bool AlreadyShown => EditorPrefs.GetBool(PreferenceKey(), false);

        /// <summary>
        /// Per project and per NexUI version.
        /// </summary>
        /// <remarks>
        /// EditorPrefs is machine-wide, so the project has to be part of the key or installing
        /// NexUI into a second project would silently skip its setup. The version is in there so an
        /// upgrade gets one more chance to point out something new; that is worth one window, and
        /// tying it to the version stops it from being worth one window every reload.
        /// </remarks>
        private static string PreferenceKey()
        {
            var identity = Application.dataPath + "|" + PackageVersion();
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(identity));

            var builder = new StringBuilder(PrefPrefix, PrefPrefix.Length + 32);
            for (var i = 0; i < 16; i++) builder.Append(hash[i].ToString("x2"));
            return builder.ToString();
        }

        internal static string PackageVersion()
        {
            try
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(NexUIFirstRun).Assembly);
                return package?.version ?? "unknown";
            }
            catch (Exception)
            {
                // Resolution fails for a package embedded straight into Assets/. Not knowing the
                // version is not a reason to skip setup, so fall back to a constant and show it once.
                return "unknown";
            }
        }
    }
}

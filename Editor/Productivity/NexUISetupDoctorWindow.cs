using System;
using System.Collections.Generic;
using System.IO;
using emiteat.NexUI.Core;
using emiteat.NexUI.Core.Registry;
using emiteat.NexUI.Integrations.UGUI;
using emiteat.NexUI.Integrations.UIToolkit;
using emiteat.NexUI.Motion;
using emiteat.NexUI.Theme;
using System.Linq;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace emiteat.NexUI.Designer.Editor.Productivity
{
    /// <summary>
    /// Read-only project readiness scan with explicit actions for common NexUI setup gaps.
    /// The doctor never changes project assets until the user presses an action.
    /// </summary>
    public sealed class NexUISetupDoctorWindow : EditorWindow
    {
        private const string RuntimePackageName = "com.nexengineworks.nexui";

        private enum CheckSeverity { Ready, Warning, Error }

        private sealed class Check
        {
            public readonly CheckSeverity Severity;
            public readonly string Title;
            public readonly string Detail;
            public readonly string ActionLabel;
            public readonly Action Action;

            public Check(CheckSeverity severity, string title, string detail, string actionLabel = null, Action action = null)
            {
                Severity = severity;
                Title = title;
                Detail = detail;
                ActionLabel = actionLabel;
                Action = action;
            }
        }

        private readonly List<Check> _checks = new List<Check>();
        private Vector2 _scroll;

        /// <summary>
        /// True when this was opened by itself right after install, rather than from the menu.
        /// </summary>
        /// <remarks>
        /// Same checks either way - only the framing differs. Someone who has just imported the
        /// package needs to know what to do next; someone who opened it from the menu already knows
        /// and came to look at a specific problem, so the greeting is only in the way.
        /// </remarks>
        private bool _firstRun;

        [MenuItem("Tools/Nex/NexUI Studio/Setup Doctor", priority = NexUIDesignerMenu.PriorityWindows + 3)]
        public static void Open() => ShowWindow(firstRun: false);

        /// <summary>Opens in welcome mode. Called by <see cref="NexUIFirstRun"/>, not from a menu.</summary>
        internal static void OpenFirstRun() => ShowWindow(firstRun: true);

        // Not named Show: EditorWindow already has Show(bool), and hiding it would make
        // window.Show(true) mean something different depending on the static type at the call site.
        private static void ShowWindow(bool firstRun)
        {
            var window = GetWindow<NexUISetupDoctorWindow>();
            window.titleContent = new GUIContent(firstRun ? "Welcome to NexUI Studio" : "NexUI Studio Setup Doctor");
            window.minSize = new Vector2(540, 420);
            window._firstRun = firstRun;
            window.Scan();
            window.Show();
        }

        private void OnEnable() => Scan();

        private void OnGUI()
        {
            DrawHeader();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Again", GUILayout.Height(28))) Scan();
                if (GUILayout.Button("Create New Screen", GUILayout.Height(28))) DesignerScreenCreationWindow.Open();
                if (GUILayout.Button("Open Designer", GUILayout.Height(28))) NexUIDesigner.Open();
            }

            var errors = _checks.FindAll(x => x.Severity == CheckSeverity.Error).Count;
            var warnings = _checks.FindAll(x => x.Severity == CheckSeverity.Warning).Count;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Ready: {_checks.Count - errors - warnings}   Warnings: {warnings}   Errors: {errors}", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var check in _checks) Draw(check);
            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        private void DrawHeader()
        {
            if (_firstRun)
            {
                EditorGUILayout.LabelField("Welcome to NexUI Studio", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "This window opens once after install. Everything below is a check, not a change - "
                    + "NexUI does not touch your project until you press an action button.\n\n"
                    + "Work top to bottom: fix anything marked as an error, then press Create New Screen.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("NexUI Setup Doctor", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Checks packages, project assets and the open scene. Nothing changes unless you press an action button.",
                MessageType.Info);
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space();
            DrawDefaultBackend();

            EditorGUILayout.Space();
            NexUILinks.DrawRow();

            if (!_firstRun) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                $"NexUI Studio {NexUIFirstRun.PackageVersion()} - reopen from Tools > Nex > NexUI Studio > Setup Doctor.",
                EditorStyles.miniLabel);
        }

        private static void Draw(Check check)
        {
            var messageType = check.Severity switch
            {
                CheckSeverity.Error => MessageType.Error,
                CheckSeverity.Warning => MessageType.Warning,
                _ => MessageType.Info
            };

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox($"{check.Title}\n{check.Detail}", messageType);
                if (check.Action != null && GUILayout.Button(check.ActionLabel, GUILayout.Width(116), GUILayout.Height(48)))
                    check.Action();
            }
        }

        private void Scan()
        {
            _checks.Clear();
            CheckPackages();
            CheckProjectAssets();
            CheckScene();
            CheckWritableAssets();
            CheckSamples();
            Repaint();
        }

        private void CheckPackages()
        {
            var runtimePackage = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UIManager).Assembly);
            Add(runtimePackage != null, "NexUI Runtime package",
                runtimePackage == null ? "The Runtime package could not be resolved." : $"Installed: {runtimePackage.version}", true);

            var uniTask = Type.GetType("Cysharp.Threading.Tasks.UniTask, UniTask") != null;
            Add(uniTask, "UniTask dependency",
                uniTask ? "UniTask is available." : "Install UniTask 2.5.10 or newer before using NexUI.", true);

            CheckUnityVersion();
            CheckRenderPipeline();
        }

        /// <summary>
        /// Compares against the floor in package.json rather than the line NexUI is developed on.
        /// </summary>
        /// <remarks>
        /// This used to test for the exact development version, which warned every supported
        /// 2022.3 user that their editor was unverified. A setup check that cries wolf on a
        /// configuration the package explicitly claims to support teaches people to ignore it.
        /// </remarks>
        private void CheckUnityVersion()
        {
            var version = Application.unityVersion;
            var supported = NexUISupportedVersions.IsSupported(version);

            _checks.Add(new Check(supported ? CheckSeverity.Ready : CheckSeverity.Error,
                "Unity version",
                supported
                    ? $"{version} is within the supported range ({NexUISupportedVersions.MinimumDisplay} and newer)."
                    : $"{version} is below the supported floor. NexUI requires Unity {NexUISupportedVersions.MinimumDisplay} or newer."));
        }

        /// <summary>
        /// URP is the only pipeline NexUI targets, so this reports rather than blocks.
        /// </summary>
        /// <remarks>
        /// Layout, binding and motion do not care about the pipeline at all; only shader-backed
        /// visuals do. Failing the whole setup because a project is on Built-in would be wrong for
        /// the majority of NexUI that works there anyway, so this is a warning that names what
        /// will actually differ.
        /// </remarks>
        private void CheckRenderPipeline()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline ?? GraphicsSettings.defaultRenderPipeline;
            var name = pipeline == null ? "Built-in Render Pipeline" : pipeline.GetType().FullName;
            // IndexOf rather than Contains(string, StringComparison): the overload taking a
            // comparison is not on every API compatibility level a 2022.3 project can be set to.
            var universal = name != null && name.IndexOf("Universal", StringComparison.Ordinal) >= 0;

            _checks.Add(new Check(universal ? CheckSeverity.Ready : CheckSeverity.Warning,
                "Render pipeline",
                universal
                    ? "Universal Render Pipeline detected."
                    : $"{name} detected. NexUI targets URP; layout, binding and motion still work, "
                      + "but shader-backed visuals are only verified on URP."));
        }

        private void CheckProjectAssets()
        {
            AddAssetCheck("NexUISettings", "Runtime settings", OpenProjectSetup);
            AddAssetCheck<UIScreenRegistryAsset>("Screen registry", OpenProjectSetup);
            AddAssetCheck<UIMotionRegistryAsset>("Motion registry", OpenProjectSetup);
            AddAssetCheck<UIThemeRegistryAsset>("Theme registry", OpenProjectSetup);

            var screens = AssetDatabase.FindAssets("t:UIScreenDefinition").Length;
            _checks.Add(new Check(screens > 0 ? CheckSeverity.Ready : CheckSeverity.Warning,
                "Screen definitions",
                screens > 0 ? $"Found {screens} screen definition(s)." : "No screen definition exists yet.",
                screens > 0 ? null : "Create Screen",
                screens > 0 ? null : DesignerScreenCreationWindow.Open));
        }

        private void CheckScene()
        {
            // The two-argument overload is the one that exists on both 2022.3 and Unity 6 - the
            // shorter FindObjectsByType<T>(FindObjectsInactive) was added later. Sorting is skipped
            // because only the count is used.
            var toolkit = UnityEngine.Object
                .FindObjectsByType<UIToolkitIntegrationBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            var ugui = UnityEngine.Object
                .FindObjectsByType<UGUIIntegrationBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            var total = toolkit + ugui;
            _checks.Add(new Check(total > 0 ? CheckSeverity.Ready : CheckSeverity.Warning,
                "Current scene backend",
                total > 0
                    ? $"Found UI Toolkit: {toolkit}, uGUI: {ugui}."
                    : "No NexUI backend bootstrap exists in the currently open scene."));
        }

        /// <summary>
        /// Offers the runtime package's samples, which are the fastest way to see a working screen.
        /// </summary>
        /// <remarks>
        /// Importing is a user action, never automatic: samples land in <c>Assets/</c> and a package
        /// that writes files into a project during install is exactly what the Asset Store rules
        /// are about. When the package resolves as embedded rather than installed, Unity reports no
        /// samples at all - that is a fine state, not a problem, so it reports as ready.
        /// </remarks>
        private void CheckSamples()
        {
            UnityEditor.PackageManager.UI.Sample[] samples;
            try
            {
                samples = UnityEditor.PackageManager.UI.Sample
                    .FindByPackage(RuntimePackageName, string.Empty).ToArray();
            }
            catch (Exception)
            {
                samples = Array.Empty<UnityEditor.PackageManager.UI.Sample>();
            }

            if (samples.Length == 0)
            {
                _checks.Add(new Check(CheckSeverity.Ready, "Samples",
                    "No importable samples were reported. This is normal for an embedded package."));
                return;
            }

            var imported = samples.Count(sample => sample.isImported);
            _checks.Add(new Check(imported > 0 ? CheckSeverity.Ready : CheckSeverity.Warning,
                "Samples",
                imported > 0
                    ? $"{imported} of {samples.Length} sample(s) imported."
                    : $"{samples.Length} sample(s) available. Importing one is the quickest way to see a working screen.",
                imported > 0 ? null : "Import First",
                imported > 0 ? (Action)null : () => ImportSample(samples[0])));
        }

        private static void ImportSample(UnityEditor.PackageManager.UI.Sample sample)
        {
            if (!EditorUtility.DisplayDialog("Import NexUI sample",
                    $"'{sample.displayName}' will be copied into your Assets folder.", "Import", "Cancel"))
                return;

            if (!sample.Import(UnityEditor.PackageManager.UI.Sample.ImportOptions.OverridePreviousImports))
                Debug.LogWarning($"[NexUI] Could not import the sample '{sample.displayName}'.");
        }

        /// <summary>
        /// Lets the default backend be picked here rather than sending the user hunting for the
        /// settings asset - it is the one decision a new project has to make before anything else.
        /// </summary>
        private void DrawDefaultBackend()
        {
            var guid = AssetDatabase.FindAssets("t:NexUISettings").FirstOrDefault();
            if (guid == null)
            {
                EditorGUILayout.LabelField("Default backend", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Create the runtime settings asset first - use the Project Setup action above.",
                    EditorStyles.wordWrappedMiniLabel);
                return;
            }

            var settings = AssetDatabase.LoadAssetAtPath<NexUISettings>(AssetDatabase.GUIDToAssetPath(guid));
            if (settings == null) return;

            EditorGUILayout.LabelField("Default backend", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var backend = (UIRenderBackend)EditorGUILayout.EnumPopup(
                new GUIContent("Backend", "Which renderer new screens target by default."),
                settings.defaultBackend);
            if (!EditorGUI.EndChangeCheck()) return;

            Undo.RecordObject(settings, "Set NexUI Default Backend");
            settings.defaultBackend = backend;
            EditorUtility.SetDirty(settings);
        }

        private void CheckWritableAssets()
        {
            var assets = new DirectoryInfo(Application.dataPath);
            var writable = assets.Exists && (assets.Attributes & FileAttributes.ReadOnly) == 0;
            Add(writable, "Generated asset location",
                writable ? "The project Assets folder is writable." : "The project Assets folder is read-only.", true);
        }

        private void AddAssetCheck<T>(string title, Action action) where T : UnityEngine.Object
            => AddAssetCheck(typeof(T).Name, title, action);

        private void AddAssetCheck(string typeName, string title, Action action)
        {
            var count = AssetDatabase.FindAssets($"t:{typeName}").Length;
            _checks.Add(new Check(count > 0 ? CheckSeverity.Ready : CheckSeverity.Warning,
                title,
                count > 0 ? $"Found {count} asset(s)." : $"No {typeName} asset exists.",
                count > 0 ? null : "Project Setup",
                count > 0 ? null : action));
        }

        private void Add(bool ready, string title, string detail, bool errorWhenMissing)
        {
            _checks.Add(new Check(ready ? CheckSeverity.Ready : errorWhenMissing ? CheckSeverity.Error : CheckSeverity.Warning,
                title, detail));
        }

        private static void OpenProjectSetup()
        {
            var type = Type.GetType("emiteat.NexUI.Editor.ProjectSetup.NexUIProjectSetupWindow, emiteat.NexUI.Editor.ProjectSetup");
            if (type != null) GetWindow(type).Show();
            else EditorUtility.DisplayDialog("NexUI", "Project Setup tool is unavailable.", "OK");
        }
    }
}

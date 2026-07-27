using System;
using System.Collections.Generic;
using System.IO;
using emiteat.NexUI.Core;
using emiteat.NexUI.Core.Registry;
using emiteat.NexUI.Integrations.UGUI;
using emiteat.NexUI.Integrations.UIToolkit;
using emiteat.NexUI.Motion;
using emiteat.NexUI.Theme;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Productivity
{
    /// <summary>
    /// Read-only project readiness scan with explicit actions for common NexUI setup gaps.
    /// The doctor never changes project assets until the user presses an action.
    /// </summary>
    public sealed class NexUISetupDoctorWindow : EditorWindow
    {
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

        [MenuItem("Tools/NexUI/Setup Doctor", priority = NexUIDesignerMenu.PriorityWindows + 3)]
        public static void Open()
        {
            var window = GetWindow<NexUISetupDoctorWindow>();
            window.titleContent = new GUIContent("NexUI Setup Doctor");
            window.minSize = new Vector2(540, 420);
            window.Scan();
            window.Show();
        }

        private void OnEnable() => Scan();

        private void OnGUI()
        {
            EditorGUILayout.LabelField("NexUI Setup Doctor", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Checks packages, project assets and the open scene. Nothing changes unless you press an action button.",
                MessageType.Info);

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

            var expected = Application.unityVersion.StartsWith("6000.4.", StringComparison.Ordinal);
            _checks.Add(new Check(expected ? CheckSeverity.Ready : CheckSeverity.Warning,
                "Unity compatibility",
                expected
                    ? $"Verified development line: {Application.unityVersion}."
                    : $"Running {Application.unityVersion}; this checkout is currently verified only on 6000.4.2f1."));
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
            var toolkit = UnityEngine.Object.FindObjectsByType<UIToolkitIntegrationBootstrap>(FindObjectsInactive.Include).Length;
            var ugui = UnityEngine.Object.FindObjectsByType<UGUIIntegrationBootstrap>(FindObjectsInactive.Include).Length;
            var total = toolkit + ugui;
            _checks.Add(new Check(total > 0 ? CheckSeverity.Ready : CheckSeverity.Warning,
                "Current scene backend",
                total > 0
                    ? $"Found UI Toolkit: {toolkit}, uGUI: {ugui}."
                    : "No NexUI backend bootstrap exists in the currently open scene."));
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

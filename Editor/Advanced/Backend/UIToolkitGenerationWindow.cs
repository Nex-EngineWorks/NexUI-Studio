using System.IO;
using System.Linq;
using emiteat.NexUI.Designer.Editor.Common;
using emiteat.NexUI.Designer.Editor.Serialization;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Backend
{
    /// <summary>
    /// Dry-run preview + explicit write of generated UXML/USS from the open screen's Designer metadata
    /// (brief §31/§39.2). Generation is automatic; the file write is a deliberate button press and
    /// always targets SEPARATE generated files (<c>&lt;screen&gt;.g.uxml</c> / <c>.g.uss</c>) next to
    /// the metadata asset — the hand-authored UXML is never touched. Writing refuses to overwrite any
    /// existing file that does not carry the generated banner, so a user's file can never be clobbered.
    /// </summary>
    public sealed class UIToolkitGenerationWindow : NexUIToolWindow
    {
        private string _uxml;
        private string _uss;
        private bool _generated;
        private bool _dryRun;
        private GeneratedAssetWriteResult _lastWrite;
        private Vector2 _uxmlScroll;
        private Vector2 _ussScroll;

        protected override string TitleKey => "uxmlGen.window.title";
        protected override string TooltipKey => "uxmlGen.window.description";

        public static void Open()
        {
            var window = GetWindow<UIToolkitGenerationWindow>();
            window.minSize = new Vector2(520f, 480f);
            window.Show();
        }

        private static NexUIDesignerContext ResolveContext()
            => DesignerSessions.ActiveContext;

        protected override void DrawBody()
        {
            var context = ResolveContext();
            var metadata = context?.Metadata;

            if (metadata == null)
            {
                Badge(T("uxmlGen.status.disconnected"), BadgeKind.Warning);
                EditorGUILayout.HelpBox(T("uxmlGen.help.noScreen"), MessageType.Info);
                if (GUILayout.Button(T("uxmlGen.status.openDesigner"), GUILayout.ExpandWidth(false)))
                    EditorApplication.ExecuteMenuItem("Tools/Nex/NexUI Studio");
                return;
            }

            Badge($"{T("uxmlGen.status.target")}: {metadata.screenId}", BadgeKind.Ok);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(LC("uxmlGen.button.generate", "tooltip.uxmlGen.generate")))
                {
                    _uxml = UIToolkitCodeGenerator.GenerateUxml(metadata);
                    _uss = UIToolkitCodeGenerator.GenerateUss(metadata);
                    _generated = true;
                }

                using (new EditorGUI.DisabledScope(!_generated))
                {
                    if (GUILayout.Button(LC("uxmlGen.button.write", "tooltip.uxmlGen.write")))
                        WriteFiles(metadata);
                }
            }

            _dryRun = EditorGUILayout.ToggleLeft(T("uxmlGen.button.dryRun"), _dryRun);

            EditorGUILayout.HelpBox(T("uxmlGen.help.safety"), MessageType.Info);

            if (!_generated) return;

            var (uxmlPath, ussPath) = TargetPaths(metadata);
            EditorGUILayout.LabelField(T("uxmlGen.field.uxmlPath"), uxmlPath, EditorStyles.miniLabel);
            EditorGUILayout.LabelField(T("uxmlGen.field.ussPath"), ussPath, EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(T("uxmlGen.button.copyUxml"))) EditorGUIUtility.systemCopyBuffer = _uxml;
                if (GUILayout.Button(T("uxmlGen.button.copyUss"))) EditorGUIUtility.systemCopyBuffer = _uss;
                if (GUILayout.Button(T("uxmlGen.button.openFolder"))) EditorUtility.RevealInFinder(Path.GetDirectoryName(uxmlPath));
                using (new EditorGUI.DisabledScope(!File.Exists(uxmlPath)))
                    if (GUILayout.Button(T("uxmlGen.button.ping"))) EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(uxmlPath));
            }
            if (_lastWrite != null)
                EditorGUILayout.HelpBox(_lastWrite.Success
                    ? T(_lastWrite.DryRun ? "uxmlGen.result.dryRun" : "uxmlGen.result.write",
                        _lastWrite.ChangedPaths.Count, _lastWrite.UnchangedPaths.Count)
                    : string.Join("\n", _lastWrite.Errors), _lastWrite.Success ? MessageType.Info : MessageType.Error);

            Section("uxmlGen.section.uxml");
            _uxmlScroll = EditorGUILayout.BeginScrollView(_uxmlScroll, GUILayout.MaxHeight(180f));
            EditorGUILayout.TextArea(_uxml, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            Section("uxmlGen.section.uss");
            _ussScroll = EditorGUILayout.BeginScrollView(_ussScroll, GUILayout.MaxHeight(180f));
            EditorGUILayout.TextArea(_uss, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private static (string uxml, string uss) TargetPaths(DesignerMetadataAsset metadata)
        {
            var assetPath = AssetDatabase.GetAssetPath(metadata);
            var dir = string.IsNullOrEmpty(assetPath) ? "Assets" : Path.GetDirectoryName(assetPath);
            var baseName = string.IsNullOrEmpty(metadata.screenId) ? metadata.name : metadata.screenId;
            baseName = Sanitize(baseName);
            return (
                (dir + "/" + baseName + ".g.uxml").Replace('\\', '/'),
                (dir + "/" + baseName + ".g.uss").Replace('\\', '/'));
        }

        private void WriteFiles(DesignerMetadataAsset metadata)
        {
            var (uxmlPath, ussPath) = TargetPaths(metadata);
            _lastWrite = new GeneratedAssetWriter().Write(new[]
            {
                new GeneratedAssetFile(uxmlPath, _uxml),
                new GeneratedAssetFile(ussPath, _uss)
            }, _dryRun);
            if (!_lastWrite.Success)
            {
                EditorUtility.DisplayDialog(T("uxmlGen.window.title"), string.Join("\n", _lastWrite.Errors), "OK");
                return;
            }

            if (!_dryRun && _lastWrite.ChangedPaths.Count > 0)
            {
                var written = AssetDatabase.LoadAssetAtPath<Object>(uxmlPath);
                if (written != null) EditorGUIUtility.PingObject(written);
                Debug.Log($"[NexUI Studio] Generated {uxmlPath} + {ussPath}");
            }
        }

        /// <summary>Only write to a path that is empty or already a NexUI-generated file — never over a
        /// user's own UXML/USS.</summary>
        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Screen";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}

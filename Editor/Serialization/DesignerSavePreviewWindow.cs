using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Serialization
{
    /// <summary>Human-readable view over the structured, mutation-free save plan.</summary>
    public sealed class DesignerSavePreviewWindow : EditorWindow
    {
        private NexUIDesignerContext _context;
        private DesignerSaveReport _report;
        private Vector2 _scroll;
        private readonly Dictionary<DesignerSaveImpactKind, bool> _expanded = new Dictionary<DesignerSaveImpactKind, bool>();

        [MenuItem("Tools/Nex/NexUI Studio/Screen/Save Preview", priority = NexUIDesignerMenu.PriorityScreen + 3)]
        public static void OpenFromMenu() => Open(DesignerSessions.ActiveContext);

        public static void Open(NexUIDesignerContext context)
        {
            var window = GetWindow<DesignerSavePreviewWindow>();
            window.titleContent = new GUIContent("NexUI Studio Save Preview");
            window.minSize = new Vector2(560f, 360f);
            window._context = context;
            window.Refresh();
            window.Show();
        }

        private void OnEnable()
        {
            foreach (DesignerSaveImpactKind kind in System.Enum.GetValues(typeof(DesignerSaveImpactKind)))
                if (!_expanded.ContainsKey(kind)) _expanded[kind] = true;
        }

        private void OnGUI()
        {
            if (_context == null || _context.IsDisposed) _context = DesignerSessions.ActiveContext;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(_context?.CurrentScreen != null ? _context.CurrentScreen.ScreenId : "No screen", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(72f))) Refresh();
                using (new EditorGUI.DisabledScope(_context?.CurrentScreen == null || (_report?.HasErrors ?? true)))
                    if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                    {
                        _context.Save();
                        Refresh();
                    }
            }

            if (_report == null)
            {
                EditorGUILayout.HelpBox("Open a screen to inspect its next save.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(_report.Summary(), _report.HasErrors ? MessageType.Error : MessageType.Info);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (DesignerSaveImpactKind kind in System.Enum.GetValues(typeof(DesignerSaveImpactKind)))
                DrawGroup(kind);
            EditorGUILayout.EndScrollView();
        }

        private void DrawGroup(DesignerSaveImpactKind kind)
        {
            var count = _report.Count(kind);
            if (count == 0) return;
            _expanded[kind] = EditorGUILayout.Foldout(_expanded[kind], $"{Label(kind)} ({count})", true);
            if (!_expanded[kind]) return;
            EditorGUI.indentLevel++;
            foreach (var impact in _report.Impacts)
            {
                if (impact.Kind != kind) continue;
                var subject = string.IsNullOrEmpty(impact.Subject) ? string.Empty : impact.Subject + " — ";
                EditorGUILayout.LabelField(subject + impact.Message, EditorStyles.wordWrappedLabel);
                if (!string.IsNullOrEmpty(impact.Path)) EditorGUILayout.SelectableLabel(impact.Path, EditorStyles.miniLabel, GUILayout.Height(17f));
                GUILayout.Space(3f);
            }
            EditorGUI.indentLevel--;
        }

        private void Refresh()
        {
            _report = _context?.PreviewSave();
            Repaint();
        }

        private static string Label(DesignerSaveImpactKind kind)
        {
            switch (kind)
            {
                case DesignerSaveImpactKind.Created: return "Create";
                case DesignerSaveImpactKind.Modified: return "Modify";
                case DesignerSaveImpactKind.PreviewOnly: return "Preview Only";
                case DesignerSaveImpactKind.UserImpact: return "User Impact / Fallback";
                case DesignerSaveImpactKind.Ownership: return "Overwrite Scope";
                default: return kind.ToString();
            }
        }
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using emiteat.NexUI.Designer.Editor.Common;
using emiteat.NexUI.Designer.Editor.Commands;

namespace emiteat.NexUI.Designer.Editor.Refactor
{
    /// <summary>Full UI for the rename refactor tool (§9).</summary>
    public sealed class RenameEditorWindow : NexUIToolWindow
    {
        [SerializeField] private DesignerMetadataAsset _asset;
        private RenameKind _kind;
        private string _old = "";
        private string _new = "";
        private List<string> _preview;
        private int _lastApplied = -1;

        protected override string TitleKey => "panel.refactor";
        protected override string TooltipKey => "tooltip.refactor";

        public static void Open() => GetWindow<RenameEditorWindow>();

        protected override void DrawBody()
        {
            _asset = (DesignerMetadataAsset)EditorGUILayout.ObjectField(
                LC("panel.hierarchy"), _asset, typeof(DesignerMetadataAsset), false);
            if (_asset == null)
            {
                EditorGUILayout.HelpBox(T("message.noScreenSelected"), MessageType.Info);
                return;
            }

            Section("panel.refactor");
            _kind = (RenameKind)EditorGUILayout.EnumPopup("kind", _kind);
            _old = EditorGUILayout.TextField("old name", _old);
            _new = EditorGUILayout.TextField("new name", _new);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Find references", GUILayout.Height(24)))
            {
                _preview = RenameService.Find(_asset, _kind, _old);
                _lastApplied = -1;
            }
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_old) || string.IsNullOrEmpty(_new)))
                if (GUILayout.Button(LC("button.rename", "tooltip.rename"), GUILayout.Height(24), GUILayout.Width(120)))
                {
                    Undo.RecordObject(_asset, "Rename " + _kind);
                    _lastApplied = RenameService.Apply(_asset, _kind, _old, _new);
                    MarkDirty(_asset);
                    _preview = null;
                }
            EditorGUILayout.EndHorizontal();

            if (_lastApplied >= 0)
                EditorGUILayout.HelpBox($"renamed {_lastApplied} reference(s). {UIDesignerShortcut.PrimaryModifierLabel}+Z to rollback.", MessageType.Info);

            if (_preview != null)
                EditorGUILayout.HelpBox(_preview.Count == 0
                    ? "(no references found)"
                    : $"{_preview.Count} reference(s):\n" + string.Join("\n", _preview), MessageType.None);
        }
    }
}

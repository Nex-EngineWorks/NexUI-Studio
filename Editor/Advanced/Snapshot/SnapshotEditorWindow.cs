using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using emiteat.NexUI.Designer.Editor.Common;

namespace emiteat.NexUI.Designer.Editor.Snapshot
{
    /// <summary>Full UI for UI snapshot capture / compare (§7).</summary>
    public sealed class SnapshotEditorWindow : NexUIToolWindow
    {
        [SerializeField] private DesignerMetadataAsset _asset;
        private UISnapshot _current;
        private int _baselineIndex;
        private List<string> _diff;

        protected override string TitleKey => "panel.snapshot";
        protected override string TooltipKey => "tooltip.snapshot";

        [MenuItem("Tools/NexUI/QA/Runtime Snapshot & Diff", priority = NexUIDesignerMenu.PriorityQa + 2)]
        public static void Open() => GetWindow<SnapshotEditorWindow>();

        protected override void DrawBody()
        {
            _asset = (DesignerMetadataAsset)EditorGUILayout.ObjectField(
                LC("panel.hierarchy"), _asset, typeof(DesignerMetadataAsset), false);
            if (_asset == null)
            {
                EditorGUILayout.HelpBox(T("message.noScreenSelected"), MessageType.Info);
                return;
            }

            Section("panel.snapshot");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(LC("button.captureSnapshot", "tooltip.captureSnapshot"), GUILayout.Height(22)))
                _current = SnapshotService.Capture(_asset);
            var context = DesignerSessions.ActiveContext;
            using (new EditorGUI.DisabledScope(context?.Metadata != _asset || context.PreviewSurface == null))
                if (GUILayout.Button("Capture Live Preview", GUILayout.Height(22), GUILayout.Width(150)))
                    _current = SnapshotService.Capture(context);
            using (new EditorGUI.DisabledScope(_current == null))
                if (GUILayout.Button("Save as baseline", GUILayout.Height(22), GUILayout.Width(140)))
                {
                    Undo.RecordObject(_asset, "Save Snapshot Baseline");
                    _asset.snapshots.baselines.Add(_current);
                    _asset.snapshots.screenId = _asset.screenId;
                    MarkDirty(_asset);
                }
            EditorGUILayout.EndHorizontal();

            if (_current != null)
                EditorGUILayout.HelpBox($"captured '{_current.snapshotId}' — {_current.elements.Count} element(s)", MessageType.None);

            var baselines = _asset.snapshots.baselines;
            if (baselines.Count > 0 && _current != null)
            {
                Section("panel.diff");
                var names = new string[baselines.Count];
                for (int i = 0; i < names.Length; i++) names[i] = $"{i}: {baselines[i].snapshotId}";
                EditorGUILayout.BeginHorizontal();
                _baselineIndex = EditorGUILayout.Popup(_baselineIndex, names);
                if (GUILayout.Button(LC("button.compare", "tooltip.compare"), GUILayout.Width(90)))
                    _diff = SnapshotService.Compare(baselines[Mathf.Clamp(_baselineIndex, 0, names.Length - 1)], _current);
                EditorGUILayout.EndHorizontal();

                if (_diff != null)
                    EditorGUILayout.HelpBox(_diff.Count == 0 ? "(no change)" : string.Join("\n", _diff),
                        _diff.Count == 0 ? MessageType.Info : MessageType.Warning);
            }
        }
    }
}

using System.Linq;
using emiteat.NexUI.MotionClip;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Productivity
{
    /// <summary>Preset preview and apply window backed by the existing Motion Clip asset model.</summary>
    public sealed class DesignerTransitionPresetWindow : EditorWindow
    {
        private NexUIDesignerContext context;
        [SerializeField] private DesignerTransitionPreset preset = DesignerTransitionPreset.Fade;
        [SerializeField] private DesignerTransitionSettings settings = new DesignerTransitionSettings();
        private UIMotionClip previewOpen;
        private UIMotionClip previewClose;

        public static void Open(NexUIDesignerContext context)
            => Open(context, DesignerTransitionPreset.Fade);

        public static void Open(NexUIDesignerContext context, DesignerTransitionPreset initialPreset)
        {
            var window = GetWindow<DesignerTransitionPresetWindow>(true, DesignerLocalization.T("productivity.transition"), true);
            window.context = context;
            window.preset = initialPreset;
            window.minSize = new Vector2(420, 490);
            window.RebuildPreview();
            window.Show();
        }

        private void OnDisable()
        {
            if (previewOpen != null && !AssetDatabase.Contains(previewOpen)) DestroyImmediate(previewOpen);
            if (previewClose != null && !AssetDatabase.Contains(previewClose)) DestroyImmediate(previewClose);
        }

        private void OnGUI()
        {
            if (context?.Metadata == null)
            {
                EditorGUILayout.HelpBox("Designer에서 화면과 Metadata를 먼저 여세요.", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField("전환 프리셋", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"대상: {context.SelectedElements.Count}개 요소");
            EditorGUI.BeginChangeCheck();
            preset = (DesignerTransitionPreset)EditorGUILayout.EnumPopup("프리셋", preset);
            settings.Duration = EditorGUILayout.Slider("지속 시간", settings.Duration, .05f, 3f);
            settings.Delay = EditorGUILayout.Slider("시작 지연", settings.Delay, 0f, 2f);
            settings.Easing = (UIMotionEasing)EditorGUILayout.EnumPopup("Easing", settings.Easing);
            settings.Distance = EditorGUILayout.FloatField("이동 거리", settings.Distance);
            settings.StartAlpha = EditorGUILayout.Slider("시작 Alpha", settings.StartAlpha, 0f, 1f);
            settings.StartScale = EditorGUILayout.Slider("시작 Scale", settings.StartScale, .1f, 1.5f);
            settings.Overshoot = EditorGUILayout.Slider("Overshoot", settings.Overshoot, 1f, 1.4f);
            settings.IncludeChildren = EditorGUILayout.Toggle("자식 포함", settings.IncludeChildren);
            settings.StaggerInterval = EditorGUILayout.Slider("Stagger 간격", settings.StaggerInterval, 0f, .5f);
            settings.Order = (DesignerStaggerOrder)EditorGUILayout.EnumPopup("Stagger 순서", settings.Order);
            settings.ReverseOrder = EditorGUILayout.Toggle("순서 뒤집기", settings.ReverseOrder);
            if (EditorGUI.EndChangeCheck()) RebuildPreview();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(previewOpen == null))
                    if (GUILayout.Button("열기 미리보기")) DesignerTransitionPresetService.Preview(context, previewOpen);
                using (new EditorGUI.DisabledScope(previewClose == null))
                    if (GUILayout.Button("닫기 미리보기")) DesignerTransitionPresetService.Preview(context, previewClose);
            }
            EditorGUILayout.HelpBox("적용하면 기존 Motion Clip 모델로 Open/Close 에셋을 만들고 Screen Motion에 연결합니다. Ctrl+Z로 연결을 되돌릴 수 있습니다.", MessageType.None);
            using (new EditorGUI.DisabledScope(context.SelectedElements.Count == 0))
                if (GUILayout.Button("생성하고 화면 전환으로 연결", GUILayout.Height(32))) Apply();
        }

        private void RebuildPreview()
        {
            if (previewOpen != null && !AssetDatabase.Contains(previewOpen)) DestroyImmediate(previewOpen);
            if (previewClose != null && !AssetDatabase.Contains(previewClose)) DestroyImmediate(previewClose);
            if (context?.Metadata == null || context.SelectedElements.Count == 0) return;
            previewOpen = DesignerTransitionPresetService.Build(context.Metadata, context.SelectedElements, preset, settings, false);
            previewClose = DesignerTransitionPresetService.Reverse(previewOpen);
        }

        private void Apply()
        {
            if ((context.Metadata.screenMotion?.entryClip != null || context.Metadata.screenMotion?.exitClip != null) &&
                !EditorUtility.DisplayDialog("기존 전환 연결 교체", "기존 Open/Close 에셋 파일은 보존하고, 화면 연결만 새 프리셋 에셋으로 교체합니다.", "교체", "취소"))
                return;
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Transition Preset");
            var metadataPath = AssetDatabase.GetAssetPath(context.Metadata);
            var folder = string.IsNullOrEmpty(metadataPath) ? "Assets" : System.IO.Path.GetDirectoryName(metadataPath)?.Replace('\\', '/');
            var baseName = string.IsNullOrEmpty(context.Metadata.screenId) ? "Screen" : context.Metadata.screenId;
            EnsureFolder(folder + "/Motions");
            var pair = DesignerTransitionPresetService.CreateAssetPair(context.Metadata, context.SelectedElements.ToList(), preset,
                $"{folder}/Motions/{baseName}.{preset}", settings);
            context.UpdateScreenMotion(m => { m.entryClip = pair.Open; m.exitClip = pair.Close; }, "Apply Transition Preset");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);
            EditorGUIUtility.PingObject(pair.Open);
            Close();
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}

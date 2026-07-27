using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Productivity
{
    /// <summary>Shows detected layout values before applying them through existing metadata.</summary>
    public sealed class DesignerLayoutConversionWindow : EditorWindow
    {
        private NexUIDesignerContext context;
        private DesignerLayoutAnalysis analysis;

        public static void Open(NexUIDesignerContext context)
        {
            var window = GetWindow<DesignerLayoutConversionWindow>(true, DesignerLocalization.T("productivity.layout"), true);
            window.context = context;
            window.analysis = DesignerLayoutAnalysisService.Analyze(context?.SelectedElements);
            if (context?.Metadata != null && context.SelectedElements != null)
            {
                foreach (var element in context.SelectedElements)
                {
                    var parent = string.IsNullOrEmpty(element.parentId) ? null : context.Metadata.Find(element.parentId);
                    if (parent?.autoLayout?.enabled == true)
                    {
                        var nested = "선택 요소가 이미 Auto Layout 아래에 있습니다. 중첩 Layout이 만들어질 수 있습니다.";
                        window.analysis.Warning = string.IsNullOrEmpty(window.analysis.Warning) ? nested : window.analysis.Warning + "\n" + nested;
                        break;
                    }
                }
            }
            window.minSize = new Vector2(410, 360);
            window.Show();
        }

        private void OnGUI()
        {
            if (analysis == null) return;
            EditorGUILayout.LabelField("선택 요소를 Auto Layout으로 정리", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("현재 시각적 배치를 분석한 값입니다. 적용 전 수정할 수 있고, 한 번의 Undo로 되돌릴 수 있습니다.", MessageType.Info);
            analysis.Layout = (DesignerDetectedLayout)EditorGUILayout.EnumPopup("감지 Layout", analysis.Layout);
            analysis.Spacing = EditorGUILayout.FloatField("Spacing", analysis.Spacing);
            analysis.PaddingLeft = EditorGUILayout.FloatField("Padding Left", analysis.PaddingLeft);
            analysis.PaddingRight = EditorGUILayout.FloatField("Padding Right", analysis.PaddingRight);
            analysis.PaddingTop = EditorGUILayout.FloatField("Padding Top", analysis.PaddingTop);
            analysis.PaddingBottom = EditorGUILayout.FloatField("Padding Bottom", analysis.PaddingBottom);
            EditorGUILayout.Toggle("동일한 크기", analysis.SameSize);
            EditorGUILayout.LabelField("감지 정렬", analysis.Alignment ?? "Mixed");
            if (!string.IsNullOrEmpty(analysis.Warning)) EditorGUILayout.HelpBox(analysis.Warning, MessageType.Warning);
            EditorGUILayout.HelpBox("uGUI Publish: Horizontal/Vertical/Grid Layout Group으로 변환\nUI Toolkit Publish: Flex Direction/Wrap/Padding/Spacing으로 변환", MessageType.None);
            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("추천 Anchor 적용"))
                    DesignerAnchorRecommendationService.Apply(context, context.Resolution);
                if (GUILayout.Button("Auto Layout 적용", GUILayout.Height(30)))
                {
                    DesignerLayoutAnalysisService.Apply(context, analysis);
                    Close();
                }
            }
        }
    }
}

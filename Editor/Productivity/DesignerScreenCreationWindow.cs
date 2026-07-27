using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Productivity
{
    /// <summary>Compact wizard for creating a connected Screen, metadata and backend asset bundle.</summary>
    public sealed class DesignerScreenCreationWindow : EditorWindow
    {
        [SerializeField] private DesignerScreenCreationRequest request = new DesignerScreenCreationRequest();

        [MenuItem("Tools/NexUI/Screen/New Screen", priority = NexUIDesignerMenu.PriorityScreen)]
        public static void Open()
        {
            var window = GetWindow<DesignerScreenCreationWindow>(true, DesignerLocalization.T("productivity.newScreen"), true);
            window.minSize = new Vector2(460, 520);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("새 화면 만들기", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Screen Definition, Designer Metadata와 선택한 Backend 에셋을 한 번에 연결합니다.", MessageType.Info);
            request.ScreenName = EditorGUILayout.TextField("화면 이름", request.ScreenName);
            request.ScreenId = EditorGUILayout.TextField("Screen ID", request.ScreenId);
            request.Backend = (UIRenderBackend)EditorGUILayout.EnumPopup("Backend", request.Backend);
            request.Template = (DesignerScreenTemplate)EditorGUILayout.EnumPopup("템플릿", request.Template);
            request.ReferenceResolution = EditorGUILayout.Vector2IntField("기준 해상도", request.ReferenceResolution);
            using (new EditorGUILayout.HorizontalScope())
            {
                request.Folder = EditorGUILayout.TextField("저장 폴더", request.Folder);
                if (GUILayout.Button("찾기", GUILayout.Width(52))) PickFolder();
            }
            EditorGUILayout.Space();
            request.CreateMetadata = EditorGUILayout.ToggleLeft("Designer Metadata 생성", request.CreateMetadata);
            request.CreateRoot = EditorGUILayout.ToggleLeft("Root 요소 생성", request.CreateRoot);
            request.CreateTransition = EditorGUILayout.ToggleLeft("기본 열기/닫기 전환 생성", request.CreateTransition);
            request.CreateSampleElements = EditorGUILayout.ToggleLeft("예제 Title/Button 추가", request.CreateSampleElements);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("생성 요약", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox($"{request.Template} / {request.Backend}\n{request.Folder}/{request.ScreenId}\n기준 해상도 {request.ReferenceResolution.x} × {request.ReferenceResolution.y}", MessageType.None);
            var valid = DesignerScreenCreationService.Validate(request, out var error);
            if (!valid) EditorGUILayout.HelpBox(error, MessageType.Warning);

            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button("생성하고 Designer에서 열기", GUILayout.Height(34))) CreateAndOpen();
            }
        }

        private void PickFolder()
        {
            var absolute = EditorUtility.OpenFolderPanel("화면 저장 폴더", Application.dataPath, string.Empty);
            if (string.IsNullOrEmpty(absolute)) return;
            absolute = absolute.Replace('\\', '/');
            var assets = Application.dataPath.Replace('\\', '/');
            if (absolute == assets || absolute.StartsWith(assets + "/"))
                request.Folder = "Assets" + absolute.Substring(assets.Length);
            else EditorUtility.DisplayDialog("NexUI", "Assets 폴더 안을 선택해 주세요.", "확인");
        }

        private void CreateAndOpen()
        {
            var result = DesignerScreenCreationService.Create(request);
            if (!result.Success)
            {
                EditorUtility.DisplayDialog("화면 생성 실패", result.Error, "확인");
                return;
            }
            var designer = NexUIDesigner.Open(result.Screen);
            if (result.Metadata != null) designer.Context.SetMetadata(result.Metadata);
            Selection.activeObject = result.Screen;
            Close();
        }
    }
}

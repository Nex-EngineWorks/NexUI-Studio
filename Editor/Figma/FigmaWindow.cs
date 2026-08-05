using System.IO;
using UnityEditor;
using UnityEngine;
using emiteat.NexUI.Designer.Editor;

namespace emiteat.NexUI.Integrations.Figma
{
    /// <summary>
    /// Brings a Figma frame into the active Designer metadata.
    /// </summary>
    /// <remarks>
    /// Two routes, and the order matters. Pasting Dev Mode's "Copy as JSON" needs no account, no
    /// token and no network, so it is the one presented first and the one the documentation points
    /// at. The REST API is kept because live sync will need it, but it is not a good default: its
    /// rate limit is charged to the plan of the file's owner, and a file on a free Figma plan
    /// allows only a handful of fetches a month - which reads as "the importer is broken" to
    /// someone who has no way to see why.
    ///
    /// Both routes hand the same JSON to the same importer, so neither can quietly develop its own
    /// idea of how a frame maps.
    /// </remarks>
    public sealed class FigmaWindow : EditorWindow
    {
        private string _json = string.Empty;
        private Vector2 _jsonScroll;
        private FigmaJsonSource _source;
        private bool _inspected;

        private bool _showApi;
        private string _token;
        private string _fileKey = string.Empty;
        private bool _busy;

        private string _statusMessage;
        private MessageType _statusType = MessageType.None;

        public static void Open() => GetWindow<FigmaWindow>("NexUI Figma Bridge");

        private void OnEnable()
        {
            _token = FigmaCredentials.Token;
            minSize = new Vector2(420f, 460f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("NexUI Figma Bridge", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "가져오기는 첫 번째 Frame의 계층, 좌표, Text, Solid Fill, Auto Layout을 현재 Designer Metadata로 변환합니다. " +
                "Component Variant, Effect, 이미지 다운로드는 아직 변환하지 않습니다. " +
                "가져온 결과는 저장 전에 Designer와 Validation에서 검토하세요.", MessageType.Info);

            DrawJsonImport();
            EditorGUILayout.Space();
            DrawApiSection();

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_statusMessage, _statusType);
            }
        }

        // ---- JSON import (primary) ---------------------------------------------------

        private void DrawJsonImport()
        {
            EditorGUILayout.LabelField("Figma JSON 붙여넣기", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Figma에서 Frame을 선택하고 Dev Mode의 \"Copy as JSON\"으로 복사한 뒤 아래에 붙여넣으세요. " +
                "계정 연결이나 토큰이 필요 없습니다. .json 파일을 이 영역에 끌어다 놓아도 됩니다.",
                MessageType.None);

            using (var scroll = new EditorGUILayout.ScrollViewScope(_jsonScroll, GUILayout.Height(120f)))
            {
                _jsonScroll = scroll.scrollPosition;
                EditorGUI.BeginChangeCheck();
                _json = EditorGUILayout.TextArea(_json, GUILayout.ExpandHeight(true));
                if (EditorGUI.EndChangeCheck()) _inspected = false;
            }

            HandleFileDrop(GUILayoutUtility.GetLastRect());

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("파일에서 불러오기", GUILayout.Height(22)))
                    LoadFromFile();

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_json)))
                {
                    if (GUILayout.Button("검사", GUILayout.Height(22)))
                        Inspect();
                    if (GUILayout.Button("지우기", GUILayout.Width(60), GUILayout.Height(22)))
                    {
                        _json = string.Empty;
                        _inspected = false;
                        SetStatus(null, MessageType.None);
                    }
                }
            }

            if (_inspected)
            {
                EditorGUILayout.LabelField("인식 결과", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(_source.Describe(), EditorStyles.wordWrappedMiniLabel);
            }

            var context = DesignerSessions.ActiveContext;
            if (context?.Metadata == null)
            {
                EditorGUILayout.HelpBox("Designer에 열린 화면이 없습니다. 화면을 열면 가져오기가 활성화됩니다.", MessageType.Warning);
                return;
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_json)))
            {
                if (GUILayout.Button("현재 Designer로 가져오기", GUILayout.Height(28)))
                    ImportIntoDesigner(context, _json);
            }
        }

        /// <summary>Accepts a dragged .json file over the paste area.</summary>
        private void HandleFileDrop(Rect area)
        {
            var evt = Event.current;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return;
            if (!area.Contains(evt.mousePosition)) return;

            var path = FirstJsonPath(DragAndDrop.paths);
            if (path == null) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type != EventType.DragPerform) return;

            DragAndDrop.AcceptDrag();
            ReadInto(path);
            evt.Use();
        }

        private static string FirstJsonPath(string[] paths)
        {
            if (paths == null) return null;
            foreach (var path in paths)
                if (!string.IsNullOrEmpty(path) &&
                    path.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
                    return path;
            return null;
        }

        private void LoadFromFile()
        {
            var path = EditorUtility.OpenFilePanel("Figma JSON 선택", string.Empty, "json");
            if (!string.IsNullOrEmpty(path)) ReadInto(path);
        }

        private void ReadInto(string path)
        {
            try
            {
                _json = File.ReadAllText(path);
                _inspected = false;
                Inspect();
            }
            catch (System.Exception ex)
            {
                SetStatus($"파일을 읽지 못했습니다: {ex.Message}", MessageType.Error);
            }
        }

        private void Inspect()
        {
            _source = FigmaJsonReader.Read(_json);
            _inspected = true;
            SetStatus(
                _source.IsValid
                    ? $"{_source.Describe()} 가져오기를 실행할 수 있습니다."
                    : "Figma JSON으로 인식되지 않습니다. Dev Mode의 \"Copy as JSON\" 결과인지 확인하세요.",
                _source.IsValid ? MessageType.Info : MessageType.Warning);
        }

        // ---- REST API (secondary) ----------------------------------------------------

        private void DrawApiSection()
        {
            _showApi = EditorGUILayout.Foldout(_showApi, "Figma REST API로 가져오기 (선택)", true);
            if (!_showApi) return;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.HelpBox(
                    "Personal Access Token이 필요합니다. Figma의 요청 한도는 파일 소유자의 Figma 요금제에 부과되며, " +
                    "무료 플랜 파일은 월 요청 수가 매우 적습니다. 정기적으로 가져올 계획이라면 위의 JSON 붙여넣기를 사용하세요.",
                    MessageType.Warning);

                EditorGUILayout.LabelField("Personal Access Token", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _token = EditorGUILayout.PasswordField(_token);
                    if (GUILayout.Button("저장", GUILayout.Width(52)))
                    {
                        FigmaCredentials.Token = _token;
                        SetStatus("토큰을 EditorPrefs에 저장했습니다. 버전 관리에는 포함되지 않습니다.", MessageType.Info);
                    }
                    if (GUILayout.Button("삭제", GUILayout.Width(52)))
                    {
                        FigmaCredentials.Clear();
                        _token = string.Empty;
                        SetStatus("토큰을 삭제했습니다.", MessageType.Info);
                    }
                }

                using (new EditorGUI.DisabledScope(_busy || string.IsNullOrEmpty(_token)))
                {
                    if (GUILayout.Button("연결 확인")) TestConnection();
                }

                EditorGUILayout.LabelField("File Key", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField("figma.com/file/<fileKey>/... 의 fileKey", EditorStyles.miniLabel);
                _fileKey = EditorGUILayout.TextField(_fileKey);

                using (new EditorGUI.DisabledScope(_busy || string.IsNullOrEmpty(_token) || string.IsNullOrEmpty(_fileKey)))
                {
                    if (GUILayout.Button("가져와서 위 입력란에 넣기"))
                        FetchFile();
                }
            }
        }

        private async void TestConnection()
        {
            _busy = true;
            SetStatus("연결 중...", MessageType.None);
            try
            {
                var user = await FigmaApiClient.GetAuthenticatedUserAsync(_token);
                SetStatus($"{user.handle} ({user.email}) 로 연결되었습니다.", MessageType.Info);
            }
            catch (System.Exception ex)
            {
                SetStatus($"연결 실패: {ex.Message}", MessageType.Error);
            }
            finally
            {
                _busy = false;
                Repaint();
            }
        }

        /// <summary>
        /// Fetches into the same text area the paste route uses, so the review step is identical
        /// whichever way the JSON arrived.
        /// </summary>
        private async void FetchFile()
        {
            _busy = true;
            SetStatus("파일을 가져오는 중...", MessageType.None);
            try
            {
                _json = await FigmaApiClient.GetFileJsonAsync(_token, _fileKey);
                _inspected = false;
                Inspect();
            }
            catch (System.Exception ex)
            {
                SetStatus($"가져오기 실패: {ex.Message}", MessageType.Error);
            }
            finally
            {
                _busy = false;
                Repaint();
            }
        }

        // ---- shared ------------------------------------------------------------------

        private void ImportIntoDesigner(NexUIDesignerContext context, string json)
        {
            if (context?.Metadata == null || string.IsNullOrWhiteSpace(json)) return;
            if (!EditorUtility.DisplayDialog("Figma Frame 가져오기",
                    "현재 Metadata의 Element를 Figma Frame으로 교체합니다. 이 작업은 Undo할 수 있습니다.",
                    "가져오기", "취소")) return;

            try
            {
                Undo.RecordObject(context.Metadata, "Import Figma Frame");
                var result = FigmaDocumentImporter.Import(json, context.Metadata);
                EditorUtility.SetDirty(context.Metadata);
                context.SetMetadata(context.Metadata);

                var message = $"'{result.FrameName}' 에서 {result.ElementCount}개 Element를 가져왔습니다. Validation 후 저장하세요.";
                if (result.AvailableRoots > 1)
                    message += $" (JSON에 {result.AvailableRoots}개 노드가 있어 첫 번째만 사용했습니다.)";
                SetStatus(message, MessageType.Info);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                SetStatus($"가져오기 실패: {ex.Message}", MessageType.Error);
            }
        }

        private void SetStatus(string message, MessageType type)
        {
            _statusMessage = message;
            _statusType = type;
            Repaint();
        }
    }
}

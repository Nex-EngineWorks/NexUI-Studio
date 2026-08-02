using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.ScreenFlow
{
    /// <summary>
    /// Standalone editor window hosting <see cref="ScreenFlowView"/> for a single
    /// <see cref="DesignerScreenFlowAsset"/> (brief §30/§33). Authoring + validation: nodes are
    /// screens, edges are typed transitions; Validate runs <see cref="ScreenFlowValidator"/> and lists
    /// broken/dangling/unreachable findings. Follows the standalone-window convention of the Motion
    /// Graph v2 editor.
    /// </summary>
    public sealed class ScreenFlowWindow : EditorWindow
    {
        [SerializeField] private DesignerScreenFlowAsset _asset;

        private ScreenFlowView _graphView;
        private VisualElement _statusRow;
        private VisualElement _validationRow;

        /// <summary>Opens the window. Reached through Tools/NexUI/Utilities.</summary>
        public static void OpenFromMenu()
        {
            var window = GetWindow<ScreenFlowWindow>();
            window.titleContent = new GUIContent("Screen Flow");
            window.minSize = new Vector2(680f, 460f);
            window.BuildUI();
        }

        public static void Open(DesignerScreenFlowAsset asset)
        {
            var window = GetWindow<ScreenFlowWindow>();
            window.titleContent = new GUIContent("Screen Flow");
            window._asset = asset;
            window.minSize = new Vector2(680f, 460f);
            window.BuildUI();
        }

        private void CreateGUI()
        {
            BuildUI();
            DesignerLocalization.LanguageChanged += BuildUI;
        }

        private void OnDisable()
        {
            DesignerLocalization.LanguageChanged -= BuildUI;
        }

        private void BuildUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("nexui-designer-root");
            root.AddToClassList("nexui-tool-window-root");
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.nexengineworks.nexui.studio/Editor/Styles/NexUIDesigner.uss");
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
                root.styleSheets.Add(styleSheet);

            root.Add(BuildToolbar());

            _statusRow = new VisualElement();
            _statusRow.AddToClassList("nexui-status-row");
            root.Add(_statusRow);

            _validationRow = new VisualElement();
            root.Add(_validationRow);

            _graphView = new ScreenFlowView();
            _graphView.AddToClassList("nexui-graph-surface");
            _graphView.GraphEdited += RefreshStatus;
            _graphView.Populate(_asset);
            root.Add(_graphView);

            RefreshStatus();
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("nexui-tool-window-toolbar");

            var assetField = new ObjectField(DesignerLocalization.T("screenFlow.toolbar.asset"))
            { objectType = typeof(DesignerScreenFlowAsset), allowSceneObjects = false, value = _asset };
            assetField.AddToClassList("nexui-metadata-field");
            DesignerTooltip.Set(assetField, "tooltip.screenFlow.asset");
            assetField.RegisterValueChangedCallback(evt =>
            {
                _asset = evt.newValue as DesignerScreenFlowAsset;
                BuildUI();
            });
            toolbar.Add(assetField);

            toolbar.Add(MakeButton(CreateAsset, DesignerLocalization.T("screenFlow.toolbar.create"), "tooltip.screenFlow.create"));
            toolbar.Add(MakeButton(() => { _graphView?.AddNodeAtCenter(); }, DesignerLocalization.T("screenFlow.toolbar.addNode"), "tooltip.screenFlow.addNode"));
            toolbar.Add(MakeButton(() => { _graphView?.AutoLayout(); }, DesignerLocalization.T("screenFlow.toolbar.autoLayout"), "tooltip.screenFlow.autoLayout"));
            toolbar.Add(MakeButton(SetEntryFromSelection, DesignerLocalization.T("screenFlow.toolbar.setEntry"), "tooltip.screenFlow.setEntry"));
            toolbar.Add(MakeButton(RunValidation, DesignerLocalization.T("screenFlow.toolbar.validate"), "tooltip.screenFlow.validate"));
            toolbar.Add(MakeButton(() => { _graphView?.SaveNow(); }, DesignerLocalization.T("screenFlow.toolbar.save"), "tooltip.screenFlow.save"));

            return toolbar;
        }

        private void CreateAsset()
        {
            var asset = ScriptableObject.CreateInstance<DesignerScreenFlowAsset>();
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/NewScreenFlow.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(asset);
            _asset = asset;
            BuildUI();
        }

        private void SetEntryFromSelection()
        {
            var id = _graphView?.SelectedNodeId();
            if (!string.IsNullOrEmpty(id)) { _graphView.SetEntryNode(id); RefreshStatus(); }
        }

        private void RunValidation()
        {
            _validationRow.Clear();
            var issues = ScreenFlowValidator.Validate(_asset);
            if (issues.Count == 0)
            {
                var ok = new Label(DesignerLocalization.T("screenFlow.validation.passed"));
                ok.AddToClassList("nexui-toolbar-status");
                ok.AddToClassList("is-ok");
                _validationRow.Add(ok);
                return;
            }
            foreach (var issue in issues)
            {
                var type = issue.Level == ScreenFlowIssue.Severity.Error ? HelpBoxMessageType.Error
                    : issue.Level == ScreenFlowIssue.Severity.Warning ? HelpBoxMessageType.Warning
                    : HelpBoxMessageType.Info;
                _validationRow.Add(new HelpBox(issue.Message, type));
            }
        }

        private void RefreshStatus()
        {
            if (_statusRow == null) return;
            _statusRow.Clear();

            if (_asset == null)
            {
                _statusRow.Add(Status(DesignerLocalization.T("screenFlow.status.noAsset"), "is-warning"));
                return;
            }

            _statusRow.Add(Status($"{DesignerLocalization.T("screenFlow.status.flow")}: {_asset.flowName}", "is-muted"));
            _statusRow.Add(Status($"{DesignerLocalization.T("screenFlow.status.nodes")}: {_asset.nodes.Count}", "is-muted"));
            var entry = string.IsNullOrEmpty(_asset.entryNodeId) ? DesignerLocalization.T("screenFlow.status.none") : _asset.entryNodeId;
            _statusRow.Add(Status($"{DesignerLocalization.T("screenFlow.status.entry")}: {entry}",
                string.IsNullOrEmpty(_asset.entryNodeId) ? "is-warning" : "is-ok"));
        }

        private static Label Status(string text, string statusClass)
        {
            var label = new Label(text);
            label.AddToClassList("nexui-toolbar-status");
            label.AddToClassList(statusClass);
            return label;
        }

        private static Button MakeButton(System.Action action, string text, string tooltipKey)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("nexui-toolbar-button");
            button.AddToClassList("nexui-button-secondary");
            DesignerTooltip.Set(button, tooltipKey);
            return button;
        }
    }
}

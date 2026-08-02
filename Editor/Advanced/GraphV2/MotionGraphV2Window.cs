using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.MotionGraph;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.GraphV2
{
    /// <summary>
    /// Standalone editor window hosting <see cref="MotionGraphV2View"/> for a single
    /// <see cref="UIMotionGraphAsset"/>. "Preview" actually runs the graph (via
    /// <see cref="UIGraphExecutor"/>) against the connected NexUI Designer preview surface -
    /// live execution, not a read-only visualization, matching the Motion Clip Editor/Motion
    /// State Machine window's Preview button precedent.
    /// </summary>
    public sealed class MotionGraphV2Window : EditorWindow
    {
        [SerializeField] private UIMotionGraphAsset _asset;

        private MotionGraphV2View _graphView;
        private VisualElement _statusRow;
        private CancellationTokenSource _previewCts;

        /// <summary>Opens the window. Reached through Tools/NexUI/Utilities.</summary>
        public static void OpenFromMenu()
        {
            var window = GetWindow<MotionGraphV2Window>();
            window.titleContent = new GUIContent("Motion Graph (v2)");
            window.minSize = new Vector2(640f, 420f);
            window.BuildUI();
        }

        public static void Open(UIMotionGraphAsset asset)
        {
            var window = GetWindow<MotionGraphV2Window>();
            window.titleContent = new GUIContent("Motion Graph (v2)");
            window._asset = asset;
            window.minSize = new Vector2(640f, 420f);
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
            _previewCts?.Cancel();
            _previewCts?.Dispose();
            _previewCts = null;
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

            _statusRow = new VisualElement { name = "PreviewStatusRow" };
            _statusRow.AddToClassList("nexui-status-row");
            root.Add(_statusRow);
            RefreshStatusRow();

            if (_asset == null)
            {
                root.Add(new HelpBox(DesignerLocalization.T("graphV2.toolbar.noAsset"), HelpBoxMessageType.Info));
                return;
            }

            _graphView = new MotionGraphV2View();
            _graphView.AddToClassList("nexui-graph-surface");
            _graphView.GraphEdited += RefreshStatusRow;
            root.Add(_graphView);
            _graphView.Populate(_asset);
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("nexui-tool-window-toolbar");

            var assetField = new ObjectField(DesignerLocalization.T("graphV2.toolbar.asset"))
            { objectType = typeof(UIMotionGraphAsset), allowSceneObjects = false, value = _asset };
            assetField.AddToClassList("nexui-metadata-field");
            assetField.RegisterValueChangedCallback(evt =>
            {
                _asset = evt.newValue as UIMotionGraphAsset;
                BuildUI();
            });
            toolbar.Add(assetField);

            var createButton = MakeButton(CreateAsset, DesignerLocalization.T("graphV2.toolbar.create"), "nexui-button-secondary");
            toolbar.Add(createButton);

            if (_asset != null)
            {
                toolbar.Add(MakeButton(() => _graphView?.AutoLayout(), DesignerLocalization.T("graphV2.toolbar.autoLayout"), "nexui-button-secondary"));

                var eventChoices = new System.Collections.Generic.List<string>();
                if (_asset.entryPoints != null)
                    foreach (var entry in _asset.entryPoints)
                        if (!string.IsNullOrEmpty(entry.eventName) && !eventChoices.Contains(entry.eventName))
                            eventChoices.Add(entry.eventName);
                if (eventChoices.Count == 0) eventChoices.Add(string.Empty);

                var eventPopup = new PopupField<string>(eventChoices, 0);
                eventPopup.AddToClassList("nexui-compact-popup");
                toolbar.Add(eventPopup);

                var previewButton = MakeButton(() => PreviewEvent(eventPopup.value), DesignerLocalization.T("graphV2.toolbar.preview"), "nexui-button-primary");
                DesignerTooltip.Set(previewButton, "tooltip.graphV2.preview", null,
                    ResolvePreviewSurface() == null ? DesignerLocalization.T("tooltip.motionClip.reason.noElementSelected") : null);
                toolbar.Add(previewButton);

                toolbar.Add(MakeButton(() => _graphView?.SaveNow(), DesignerLocalization.T("motionClip.toolbar.save"), "nexui-button-secondary"));
            }

            return toolbar;
        }

        private void RefreshStatusRow()
        {
            if (_statusRow == null) return;
            _statusRow.Clear();

            var connected = ResolvePreviewSurface() != null;
            var statusLabel = new Label($"{DesignerLocalization.T("motionClip.status.label")}: " +
                DesignerLocalization.T(connected ? "motionClip.status.connected" : "motionClip.status.disconnected"));
            statusLabel.AddToClassList("nexui-toolbar-status");
            statusLabel.AddToClassList(connected ? "is-ok" : "is-warning");
            _statusRow.Add(statusLabel);
        }

        private void CreateAsset()
        {
            var asset = ScriptableObject.CreateInstance<UIMotionGraphAsset>();
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/NewMotionGraph.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _asset = asset;
            EditorGUIUtility.PingObject(asset);
            BuildUI();
        }

        private void PreviewEvent(string eventName)
        {
            if (_asset == null || string.IsNullOrEmpty(eventName)) return;
            var surface = ResolvePreviewSurface();
            if (surface == null) return;

            _previewCts?.Cancel();
            _previewCts?.Dispose();
            _previewCts = new CancellationTokenSource();

            var executor = new UIGraphExecutor(_asset);
            var context = new UIGraphExecutionContext
            {
                Surface = surface,
                EventTargetElementId = ResolveDesignerContext()?.SelectedMetadata?.elementId
            };
            executor.RunEventAsync(eventName, context, _previewCts.Token).Forget();
        }

        private IUISurface ResolvePreviewSurface() => ResolveDesignerContext()?.PreviewSurface;

        private NexUIDesignerContext ResolveDesignerContext()
        {
            return DesignerSessions.ActiveContext;
        }

        private static Button MakeButton(System.Action action, string text, string className)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("nexui-toolbar-button");
            button.AddToClassList(className);
            return button;
        }
    }
}

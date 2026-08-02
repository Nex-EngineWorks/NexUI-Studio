using emiteat.NexUI.Motion;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Graph
{
    /// <summary>
    /// Standalone editor window hosting the visual Motion Graph editor for a single
    /// <see cref="UIMotionPreset"/>. Opened either from the Motion inspector's "Open Motion
    /// Graph" button (preset pre-filled) or directly via Tools/NexUI/Utilities > Motion Graph
    /// (pick any preset from the toolbar). The preset is stored in a serialized field so the
    /// window survives domain reloads.
    /// </summary>
    public sealed class MotionGraphWindow : EditorWindow
    {
        [SerializeField] private UIMotionPreset _preset;
        private MotionGraphPanel _panel;

        public static void OpenFromMenu()
        {
            var window = GetWindow<MotionGraphWindow>();
            window.titleContent = new GUIContent("Motion Graph");
            window.minSize = new Vector2(560f, 360f);
            window.BuildUI();
        }

        public static void Open(UIMotionPreset preset)
        {
            var window = GetWindow<MotionGraphWindow>();
            window.titleContent = new GUIContent("Motion Graph");
            window._preset = preset;
            window.minSize = new Vector2(560f, 360f);
            window.BuildUI();
        }

        private void CreateGUI() => BuildUI();

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

            var toolbar = new Toolbar();
            toolbar.AddToClassList("nexui-tool-window-toolbar");
            var presetField = new ObjectField("Preset") { objectType = typeof(UIMotionPreset), allowSceneObjects = false, value = _preset };
            presetField.AddToClassList("nexui-metadata-field");
            presetField.RegisterValueChangedCallback(evt =>
            {
                _preset = evt.newValue as UIMotionPreset;
                BuildUI();
            });
            toolbar.Add(presetField);
            toolbar.Add(MakeButton(() => _panel?.GraphView.AddNodeAtCenter(), "Add Node", "nexui-button-secondary"));
            toolbar.Add(MakeButton(() => _panel?.GraphView.AutoLayout(), "Auto Layout", "nexui-button-secondary"));
            toolbar.Add(MakeButton(() => _panel?.Save(), "Save Now", "nexui-button-primary"));
            root.Add(toolbar);

            if (_preset == null)
            {
                root.Add(new HelpBox("No motion preset assigned. Pick one above, or open this window from the Motion inspector's \"Open Motion Graph\" button.", HelpBoxMessageType.Info));
                return;
            }

            _panel = new MotionGraphPanel(_preset);
            root.Add(_panel);
        }

        private static ToolbarButton MakeButton(System.Action action, string text, string className)
        {
            var button = new ToolbarButton(action) { text = text };
            button.AddToClassList("nexui-toolbar-button");
            button.AddToClassList(className);
            return button;
        }
    }
}

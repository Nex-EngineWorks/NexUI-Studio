using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Designer.Editor.Commands;
using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.MotionClip;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.StateMachine
{
    /// <summary>
    /// Standalone authoring window for <see cref="UIMotionStateMachine"/> assets: default state,
    /// a flat list of transitions (from/to/clip/interrupt policy), and a Preview button per
    /// transition that plays its clip against the connected NexUI Designer preview surface via the
    /// same <see cref="UIMotionClipPlayer"/> the Motion Clip Editor uses. MVP: buttons-first UI,
    /// matching <c>MotionClipEditorWindow</c>/<c>MotionGraphWindow</c>'s established convention -
    /// not a visual state graph (that's future work once the Motion Graph v2 engine from
    /// Architecture-Audit.md exists to host it).
    /// </summary>
    public sealed class MotionStateMachineWindow : EditorWindow
    {
        [SerializeField] private UIMotionStateMachine _machine;

        private readonly UIMotionClipPlayer _previewPlayer = new UIMotionClipPlayer();
        private VisualElement _transitionsHost;
        private VisualElement _statusRow;

        /// <summary>Opens the window. Reached through Tools/NexUI/Utilities.</summary>
        public static void OpenFromMenu()
        {
            var window = GetWindow<MotionStateMachineWindow>();
            window.titleContent = new GUIContent("Motion State Machine");
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
            _previewPlayer.Stop();
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

            _transitionsHost = new VisualElement { name = "TransitionsHost" };
            _transitionsHost.style.flexGrow = 1;
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.Add(_transitionsHost);
            root.Add(scroll);

            RefreshTransitions();
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("nexui-tool-window-toolbar");

            var machineField = new ObjectField(DesignerLocalization.T("stateMachine.toolbar.asset"))
            { objectType = typeof(UIMotionStateMachine), allowSceneObjects = false, value = _machine };
            machineField.AddToClassList("nexui-metadata-field");
            DesignerTooltip.Set(machineField, "tooltip.stateMachine.asset");
            machineField.RegisterValueChangedCallback(evt =>
            {
                _machine = evt.newValue as UIMotionStateMachine;
                BuildUI();
            });
            toolbar.Add(machineField);

            var createButton = MakeButton(CreateMachine, DesignerLocalization.T("stateMachine.toolbar.create"), "nexui-button-secondary");
            DesignerTooltip.Set(createButton, "tooltip.stateMachine.create");
            toolbar.Add(createButton);

            if (_machine != null)
            {
                var defaultState = new EnumField(DesignerLocalization.T("stateMachine.toolbar.defaultState"), _machine.defaultState);
                defaultState.AddToClassList("nexui-compact-popup");
                DesignerTooltip.Set(defaultState, "tooltip.stateMachine.defaultState");
                defaultState.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(_machine, "Edit Motion State Machine Default State");
                    _machine.defaultState = (UIMotionState)evt.newValue;
                    EditorUtility.SetDirty(_machine);
                });
                toolbar.Add(defaultState);

                var addTransitionButton = MakeButton(AddTransition, DesignerLocalization.T("stateMachine.toolbar.addTransition"), "nexui-button-secondary");
                DesignerTooltip.Set(addTransitionButton, "tooltip.stateMachine.addTransition");
                toolbar.Add(addTransitionButton);

                var saveButton = MakeButton(() => AssetDatabase.SaveAssets(), DesignerLocalization.T("motionClip.toolbar.save"), "nexui-button-secondary");
                DesignerTooltip.Set(saveButton, "tooltip.motionClip.save",
                    UIDesignerShortcut.PrimaryModifierLabel + "+S");
                toolbar.Add(saveButton);
            }

            return toolbar;
        }

        private void RefreshStatusRow()
        {
            if (_statusRow == null) return;
            _statusRow.Clear();

            var surface = ResolvePreviewSurface();
            var connected = surface != null;
            var statusLabel = new Label($"{DesignerLocalization.T("motionClip.status.label")}: " +
                DesignerLocalization.T(connected ? "motionClip.status.connected" : "motionClip.status.disconnected"));
            statusLabel.AddToClassList("nexui-toolbar-status");
            statusLabel.AddToClassList(connected ? "is-ok" : "is-warning");
            _statusRow.Add(statusLabel);

            if (!connected)
            {
                var openButton = MakeButton(() => EditorApplication.ExecuteMenuItem("Tools/Nex/NexUI Studio"),
                    DesignerLocalization.T("motionClip.status.openDesigner"), "nexui-button-secondary");
                DesignerTooltip.Set(openButton, "tooltip.motionClip.openDesigner");
                _statusRow.Add(openButton);
            }
        }

        private void CreateMachine()
        {
            var asset = ScriptableObject.CreateInstance<UIMotionStateMachine>();
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/NewMotionStateMachine.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _machine = asset;
            EditorGUIUtility.PingObject(asset);
            BuildUI();
        }

        private void AddTransition()
        {
            if (_machine == null) return;
            Undo.RecordObject(_machine, "Add Motion State Transition");
            var transitions = new List<UIMotionStateTransition>(_machine.transitions ?? System.Array.Empty<UIMotionStateTransition>())
            {
                new UIMotionStateTransition { from = UIMotionState.Normal, to = UIMotionState.Hover }
            };
            _machine.transitions = transitions.ToArray();
            EditorUtility.SetDirty(_machine);
            RefreshTransitions();
        }

        private void RefreshTransitions()
        {
            if (_transitionsHost == null) return;
            _transitionsHost.Clear();
            RefreshStatusRow();

            if (_machine == null)
            {
                _transitionsHost.Add(new HelpBox(DesignerLocalization.T("stateMachine.toolbar.noMachine"), HelpBoxMessageType.Info));
                return;
            }

            if (_machine.transitions == null || _machine.transitions.Length == 0)
            {
                _transitionsHost.Add(new HelpBox(DesignerLocalization.T("stateMachine.toolbar.noTransitions"), HelpBoxMessageType.Info));
                return;
            }

            for (var i = 0; i < _machine.transitions.Length; i++)
                _transitionsHost.Add(BuildTransitionRow(_machine.transitions[i]));
        }

        private VisualElement BuildTransitionRow(UIMotionStateTransition transition)
        {
            var box = new VisualElement();
            box.AddToClassList("nexui-panel");

            var row = new VisualElement();
            row.AddToClassList("nexui-inline-row");

            var anyState = new Toggle(DesignerLocalization.T("stateMachine.toolbar.anyState")) { value = transition.fromAnyState };
            anyState.AddToClassList("nexui-toolbar-toggle");
            DesignerTooltip.Set(anyState, "tooltip.stateMachine.anyState");
            row.Add(anyState);

            var from = new EnumField(DesignerLocalization.T("stateMachine.toolbar.from"), transition.from);
            from.style.width = 130f;
            from.SetEnabled(!transition.fromAnyState);
            row.Add(from);

            anyState.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_machine, "Edit Motion State Transition");
                transition.fromAnyState = evt.newValue;
                EditorUtility.SetDirty(_machine);
                from.SetEnabled(!evt.newValue);
            });
            from.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_machine, "Edit Motion State Transition");
                transition.from = (UIMotionState)evt.newValue;
                EditorUtility.SetDirty(_machine);
            });

            var to = new EnumField(DesignerLocalization.T("stateMachine.toolbar.to"), transition.to);
            to.style.width = 130f;
            to.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_machine, "Edit Motion State Transition");
                transition.to = (UIMotionState)evt.newValue;
                EditorUtility.SetDirty(_machine);
            });
            row.Add(to);

            var interruptPolicy = new EnumField(DesignerLocalization.T("stateMachine.toolbar.interruptPolicy"), transition.interruptPolicy);
            interruptPolicy.style.width = 180f;
            DesignerTooltip.Set(interruptPolicy, "tooltip.stateMachine.interruptPolicy");
            interruptPolicy.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_machine, "Edit Motion State Transition");
                transition.interruptPolicy = (UIMotionStateInterruptPolicy)evt.newValue;
                EditorUtility.SetDirty(_machine);
            });
            row.Add(interruptPolicy);

            var deleteButton = MakeButton(() => DeleteTransition(transition), DesignerLocalization.T("motionClip.toolbar.deleteTrack"), "nexui-button-secondary");
            row.Add(deleteButton);

            box.Add(row);

            var clipRow = new VisualElement();
            clipRow.AddToClassList("nexui-inline-row");
            var clipField = new ObjectField(DesignerLocalization.T("stateMachine.toolbar.clip"))
            { objectType = typeof(UIMotionClip), allowSceneObjects = false, value = transition.clip };
            clipField.style.flexGrow = 1;
            clipField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_machine, "Edit Motion State Transition");
                transition.clip = evt.newValue as UIMotionClip;
                EditorUtility.SetDirty(_machine);
                RefreshTransitions();
            });
            clipRow.Add(clipField);

            var canPreview = transition.clip != null;
            var previewButton = MakeButton(() => PreviewTransition(transition), DesignerLocalization.T("stateMachine.toolbar.preview"), "nexui-button-primary");
            previewButton.SetEnabled(canPreview && ResolvePreviewSurface() != null);
            DesignerTooltip.Set(previewButton, "tooltip.stateMachine.preview", null,
                !canPreview ? DesignerLocalization.T("tooltip.stateMachine.reason.noClip")
                : ResolvePreviewSurface() == null ? DesignerLocalization.T("tooltip.motionClip.reason.noElementSelected") : null);
            clipRow.Add(previewButton);
            box.Add(clipRow);

            return box;
        }

        private void DeleteTransition(UIMotionStateTransition transition)
        {
            Undo.RecordObject(_machine, "Delete Motion State Transition");
            var transitions = new List<UIMotionStateTransition>(_machine.transitions);
            transitions.Remove(transition);
            _machine.transitions = transitions.ToArray();
            EditorUtility.SetDirty(_machine);
            RefreshTransitions();
        }

        private void PreviewTransition(UIMotionStateTransition transition)
        {
            if (transition?.clip == null) return;
            var surface = ResolvePreviewSurface();
            if (surface == null) return;
            _previewPlayer.PlayAsync(surface, transition.clip).Forget();
        }

        private IUISurface ResolvePreviewSurface()
        {
            return DesignerSessions.ActiveContext?.PreviewSurface;
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

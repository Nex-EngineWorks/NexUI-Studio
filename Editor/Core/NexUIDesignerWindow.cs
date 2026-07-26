using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.UI.Shell;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor
{
    public sealed class NexUIDesignerWindow : EditorWindow
    {
        public NexUIDesignerContext Context { get; private set; }

        private void OnEnable()
        {
            if (Context == null) Context = new NexUIDesignerContext();
            DesignerSessions.Registry.Register(this, Context);
            Context.DirtyStateChanged += OnDirtyStateChanged;
            saveChangesMessage = "NexUI Designer에 저장하지 않은 변경 사항이 있습니다.";
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);
            EditorApplication.delayCall += RestoreLastSession;
            // Every localized string (panel titles, tooltips, ...) is only read at construction
            // time, so re-run Rebuild() on language switch instead of requiring the window to be
            // closed and reopened.
            DesignerLocalization.LanguageChanged += Rebuild;
        }

        private void OnDisable()
        {
            DesignerLocalization.LanguageChanged -= Rebuild;
            if (Context != null) Context.DirtyStateChanged -= OnDirtyStateChanged;
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            EditorApplication.delayCall -= RestoreLastSession;
            DesignerSessions.Registry.Unregister(this, Context);
            Context?.Dispose();
            Context = null;
        }

        private void OnFocus() => DesignerSessions.Registry.SetActive(this);

        public void CreateGUI() => Rebuild();

        public override void SaveChanges()
        {
            Context?.Save();
            hasUnsavedChanges = Context?.HasUnsavedChanges ?? false;
            if (!hasUnsavedChanges) base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            hasUnsavedChanges = false;
            base.DiscardChanges();
        }

        private void Rebuild()
        {
            titleContent.text = DesignerLocalization.T("designer.title");
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("nexui-designer-root");
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.emiteat.nexui.designer/Editor/Styles/NexUIDesigner.uss");
            if (styleSheet != null && !rootVisualElement.styleSheets.Contains(styleSheet))
                rootVisualElement.styleSheets.Add(styleSheet);
            // EditorStyles can be temporarily unavailable while an open Designer window is
            // reconstructed during domain reload. USS still supplies a valid fallback font.
            var labelStyle = EditorStyles.label;
            if (labelStyle != null && labelStyle.font != null)
                rootVisualElement.style.unityFont = labelStyle.font;

            rootVisualElement.Add(new NexUIDesignerShell(Context));
        }

        private void OnDirtyStateChanged(bool dirty) => hasUnsavedChanges = dirty;

        private void RestoreLastSession()
        {
            if (this != null && Context != null) Context.RestoreLastSession();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!evt.actionKey || evt.keyCode != KeyCode.S) return;
            Context?.Save();
            evt.StopImmediatePropagation();
        }
    }
}

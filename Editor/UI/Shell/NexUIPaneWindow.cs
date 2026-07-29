using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.Styles;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.UI.Shell
{
    /// <summary>
    /// A single Designer pane living in its own <c>EditorWindow</c>, so it can be docked, tabbed,
    /// floated and resized with Unity's own window handling - and so the arrangement is saved in the
    /// editor layout like every other window.
    ///
    /// The window holds no state of its own: it follows <see cref="DesignerSessions.ActiveContext"/>,
    /// so focusing a different Designer window re-points every detached pane at that screen instead
    /// of leaving stale views behind.
    /// </summary>
    public sealed class NexUIPaneWindow : EditorWindow
    {
        [SerializeField] private DesignerPaneKind _kind;

        private NexUIDesignerContext _boundContext;
        private bool _subscribed;

        /// <summary>Opens (or focuses) the window for a pane. Region panes are removed from the shell.</summary>
        public static NexUIPaneWindow Open(DesignerPaneKind kind)
        {
            // One window per kind: a second Inspector would be two views fighting over the same
            // selection, which is confusing rather than useful.
            foreach (var existing in Resources.FindObjectsOfTypeAll<NexUIPaneWindow>())
            {
                if (existing._kind != kind) continue;
                existing.Show();
                existing.Focus();
                return existing;
            }

            var window = CreateInstance<NexUIPaneWindow>();
            window._kind = kind;
            window.ApplyTitle();
            window.Show();
            DesignerPaneLayout.SetDetached(kind, true);
            return window;
        }

        private void OnEnable()
        {
            ApplyTitle();
            minSize = new Vector2(220f, 160f);

            DesignerSessions.Provider.ActiveContextChanged += OnActiveContextChanged;
            DesignerLocalization.LanguageChanged += ApplyTitle;
            DesignerPaneLayout.Changed += ApplyTitle;
            _subscribed = true;

            // A domain reload re-enables the window; mark the region detached again so the shell does
            // not draw it twice.
            DesignerPaneLayout.SetDetached(_kind, true);
            Rebuild();
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            _subscribed = false;
            DesignerSessions.Provider.ActiveContextChanged -= OnActiveContextChanged;
            DesignerLocalization.LanguageChanged -= ApplyTitle;
            DesignerPaneLayout.Changed -= ApplyTitle;
        }

        private void OnDestroy()
        {
            // Closing the window puts the region back in the Designer shell rather than leaving a
            // hole the user has to go find a menu item to fill.
            DesignerPaneLayout.SetDetached(_kind, false);
        }

        private void ApplyTitle()
        {
            var title = DesignerPaneLayout.Title(_kind);
            titleContent = new GUIContent(title, DesignerPaneLayout.Detail(_kind));
        }

        private void OnActiveContextChanged(NexUIDesignerContext context)
        {
            if (context == _boundContext) return;
            Rebuild();
        }

        private void Update()
        {
            // The registry raises its event only when the *active* window changes; opening the first
            // Designer window after this pane leaves the pane empty until something re-binds it.
            if (DesignerSessions.ActiveContext != _boundContext)
                Rebuild();
        }

        private void Rebuild()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("nexui-designer-root");
            DesignerStyleSheet.Apply(root);

            _boundContext = DesignerSessions.ActiveContext;
            if (_boundContext == null)
            {
                var empty = new Label(DesignerLocalization.T("pane.window.noSession"));
                empty.AddToClassList("nexui-empty-note");
                root.Add(empty);
                return;
            }

            var descriptor = DesignerPaneLayout.Get(_kind);
            if (descriptor?.Create == null) return;

            var content = descriptor.Create(_boundContext);
            content.style.flexGrow = 1;
            root.Add(content);
        }
    }
}

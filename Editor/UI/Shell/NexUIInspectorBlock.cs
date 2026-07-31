using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.UI.Shell
{
    /// <summary>
    /// One block in the Inspector stack, drawn like a Unity component: icon, bold title, a fold
    /// arrow that covers the whole header, and a ⋮ menu on the right.
    /// </summary>
    /// <remarks>
    /// Content is built on first expansion and never rebuilt on collapse, so a stack of twenty
    /// blocks costs one built section rather than twenty - which is what made the previous
    /// Inspector slow to open on a busy element.
    ///
    /// Expansion state is per block id in EditorPrefs, matching Unity's own behaviour of
    /// remembering which components you left open.
    /// </remarks>
    public sealed class NexUIInspectorBlock : VisualElement
    {
        private const string PrefPrefix = "NexUI.Designer.Inspector.Block.";

        private readonly Foldout _foldout;
        private readonly Func<VisualElement> _build;
        private bool _built;

        public string Id { get; }

        public NexUIInspectorBlock(string id, string title, string icon, Func<VisualElement> build,
            bool defaultExpanded = true, Action<GenericMenu> menu = null, string tooltipText = null)
        {
            Id = id;
            _build = build;
            AddToClassList("nexui-inspector-block");

            _foldout = new Foldout
            {
                text = string.IsNullOrEmpty(icon) ? title : icon + "  " + title,
                value = EditorPrefs.GetBool(PrefPrefix + id, defaultExpanded),
                tooltip = tooltipText
            };
            _foldout.AddToClassList("nexui-inspector-block__foldout");
            _foldout.RegisterValueChangedCallback(evt =>
            {
                // Foldouts nested inside the content bubble their own change events; only the
                // block's own header should be remembered.
                if (evt.target != _foldout) return;
                EditorPrefs.SetBool(PrefPrefix + id, evt.newValue);
                if (evt.newValue) EnsureContent();
            });
            Add(_foldout);

            if (menu != null)
            {
                var button = new Button(() =>
                {
                    var generic = new GenericMenu();
                    menu(generic);
                    generic.ShowAsContext();
                }) { text = "⋮" };
                button.AddToClassList("nexui-inspector-block__menu");
                Add(button);

                _foldout.RegisterCallback<ContextClickEvent>(evt =>
                {
                    var generic = new GenericMenu();
                    menu(generic);
                    generic.ShowAsContext();
                    evt.StopPropagation();
                });
            }

            if (_foldout.value) EnsureContent();
        }

        /// <summary>Expands the block, building its content if this is the first time.</summary>
        public void Expand()
        {
            if (_foldout.value) return;
            _foldout.value = true;
        }

        public bool IsExpanded => _foldout.value;

        private void EnsureContent()
        {
            if (_built) return;
            _built = true;

            var content = _build();
            if (content == null) return;

            // Sections were authored as standalone panels with their own title and card chrome.
            // Inside a block the header is the block's own, so that chrome is stripped rather than
            // drawn twice.
            content.Q<Label>("SectionTitle")?.RemoveFromHierarchy();
            content.Q<Label>("PanelTitle")?.RemoveFromHierarchy();
            content.RemoveFromClassList("nexui-inspector-section");
            content.RemoveFromClassList("nexui-panel");
            content.RemoveFromClassList("nexui-bottom-card");
            content.style.flexGrow = 0;
            content.AddToClassList("nexui-inspector-block__content");
            _foldout.Add(content);
        }

        public static void SetExpanded(string id, bool expanded)
            => EditorPrefs.SetBool(PrefPrefix + id, expanded);
    }
}

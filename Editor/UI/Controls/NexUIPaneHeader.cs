using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.UI.Controls
{
    /// <summary>
    /// The one-line caption at the top of every major Designer pane.
    ///
    /// The window is five regions that all look alike at a glance, which made it hard to tell what
    /// you were looking at. Rather than repeating what the tab bar already says, the caption states
    /// the pane's <b>name</b> and a short line on <b>what it is for</b> - so a pane answers "where am
    /// I and what does this do" without a tooltip hunt.
    ///
    /// Deliberately one 18px row: the window is already dense, so a caption that cost a full header
    /// block would trade one problem for another.
    /// </summary>
    public sealed class NexUIPaneHeader : VisualElement
    {
        private readonly Label _title = new Label();
        private readonly Label _detail = new Label();
        private readonly VisualElement _trailing = new VisualElement();

        public NexUIPaneHeader(string title, string detail = null)
        {
            AddToClassList("nexui-pane-header");

            _title.AddToClassList("nexui-pane-header-title");
            Add(_title);

            _detail.AddToClassList("nexui-pane-header-detail");
            Add(_detail);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            Add(spacer);

            _trailing.AddToClassList("nexui-pane-header-trailing");
            Add(_trailing);

            Set(title, detail);
        }

        /// <summary>Slot for small pane-level controls (counts, toggles) kept on the caption row.</summary>
        public VisualElement Trailing => _trailing;

        /// <summary>
        /// Adds the "open in its own window" button. Clicking it hands the pane to Unity's window
        /// system, where it can be docked anywhere; closing that window brings the pane back.
        /// </summary>
        public NexUIPaneHeader WithDetachButton(System.Action detach, string tooltip)
        {
            var button = new Button(detach) { text = "⧉", tooltip = tooltip };
            button.AddToClassList("nexui-pane-header-button");
            _trailing.Add(button);
            return this;
        }

        public void Set(string title, string detail = null)
        {
            _title.text = title ?? string.Empty;
            _detail.text = detail ?? string.Empty;
            _detail.style.display = string.IsNullOrEmpty(detail) ? DisplayStyle.None : DisplayStyle.Flex;
            tooltip = string.IsNullOrEmpty(detail) ? title : title + " — " + detail;
        }
    }
}

using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Commands;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Panels
{
    /// <summary>
    /// Multi-selection Align/Distribute/Layer actions as a narrow vertical strip docked beside
    /// the component Palette, instead of a third toolbar row (which used to spill past the
    /// toolbar's box and overlap the panels below it). Every button here has a matching default
    /// keyboard shortcut (see <see cref="Commands.UIDesignerShortcutRegistry"/>) shown in its
    /// tooltip, so once learned these never need a click at all.
    /// </summary>
    public sealed class NexUIDesignerAlignPanel : VisualElement
    {
        private readonly List<Button> _alignButtons = new();
        private readonly List<Button> _distributeButtons = new();
        private readonly List<Button> _layerButtons = new();

        public NexUIDesignerAlignPanel(NexUIDesignerContext context)
        {
            AddToClassList("nexui-panel");
            AddToClassList("nexui-align-panel");
            Add(new Label(DesignerLocalization.T("panel.align")) { name = "PanelTitle" });

            AddButton(_alignButtons, "⇤", () => context.AlignSelection("left"), DesignerLocalization.T("tooltip.toolbar.alignLeft") + " (Alt+L)");
            AddButton(_alignButtons, "⇔x", () => context.AlignSelection("centerX"), DesignerLocalization.T("tooltip.toolbar.alignCenterX") + " (Alt+C)");
            AddButton(_alignButtons, "⇥", () => context.AlignSelection("right"), DesignerLocalization.T("tooltip.toolbar.alignRight") + " (Alt+R)");
            AddButton(_alignButtons, "⇡", () => context.AlignSelection("top"), DesignerLocalization.T("tooltip.toolbar.alignTop") + " (Alt+T)");
            AddButton(_alignButtons, "⇕y", () => context.AlignSelection("centerY"), DesignerLocalization.T("tooltip.toolbar.alignCenterY") + " (Alt+M)");
            AddButton(_alignButtons, "⇣", () => context.AlignSelection("bottom"), DesignerLocalization.T("tooltip.toolbar.alignBottom") + " (Alt+B)");

            Add(MakeDivider());
            Add(MakeGroupLabel(DesignerLocalization.T("toolbar.group.distribute")));
            AddButton(_distributeButtons, "⋯H", context.DistributeSelectionHorizontal, DesignerLocalization.T("tooltip.toolbar.distributeH") + " (Alt+H)");
            AddButton(_distributeButtons, "⋮V", context.DistributeSelectionVertical, DesignerLocalization.T("tooltip.toolbar.distributeV") + " (Alt+V)");

            Add(MakeDivider());
            Add(MakeGroupLabel(DesignerLocalization.T("toolbar.group.layer")));
            AddButton(_layerButtons, "Fwd", context.BringSelectionForward, DesignerLocalization.T("tooltip.toolbar.bringForward") +
                " (" + UIDesignerShortcut.Format(KeyCode.RightBracket, primary: true) + ")");
            AddButton(_layerButtons, "Back", context.SendSelectionBackward, DesignerLocalization.T("tooltip.toolbar.sendBackward") +
                " (" + UIDesignerShortcut.Format(KeyCode.LeftBracket, primary: true) + ")");
            AddButton(_layerButtons, "Front", context.BringSelectionToFront, DesignerLocalization.T("tooltip.toolbar.bringToFront") +
                " (" + UIDesignerShortcut.Format(KeyCode.RightBracket, primary: true, shift: true) + ")");
            AddButton(_layerButtons, "Bottom", context.SendSelectionToBack, DesignerLocalization.T("tooltip.toolbar.sendToBack") +
                " (" + UIDesignerShortcut.Format(KeyCode.LeftBracket, primary: true, shift: true) + ")");

            void RefreshButtons(IReadOnlyList<DesignerElementMetadata> selection)
            {
                var hasSelection = selection != null && selection.Count > 0;
                var canDistribute = selection != null && selection.Count >= 3;
                foreach (var button in _alignButtons) button.SetEnabled(hasSelection);
                foreach (var button in _layerButtons) button.SetEnabled(hasSelection);
                foreach (var button in _distributeButtons) button.SetEnabled(canDistribute);
            }

            var subscriptions = new ContextBoundSubscriptions(this);
            subscriptions.Add<System.Collections.Generic.IReadOnlyList<DesignerElementMetadata>>(h => context.MultiSelectionChanged += h, h => context.MultiSelectionChanged -= h, RefreshButtons);
            RefreshButtons(context.SelectedElements);
        }

        private void AddButton(List<Button> group, string text, System.Action action, string tooltip)
        {
            var button = new Button(action) { text = text, tooltip = tooltip };
            button.AddToClassList("nexui-align-panel-button");
            group.Add(button);
            Add(button);
        }

        private static VisualElement MakeDivider()
        {
            var divider = new VisualElement();
            divider.AddToClassList("nexui-align-panel-divider");
            return divider;
        }

        private static Label MakeGroupLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("nexui-toolbar-group-label");
            return label;
        }
    }
}

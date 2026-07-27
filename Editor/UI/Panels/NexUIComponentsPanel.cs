using System;
using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.UI.Panels
{
    public sealed class NexUIComponentsPanel : VisualElement
    {
        /// <summary>
        /// Library contents, grouped the way UI Builder's Library groups its own entries. Both the
        /// group titles and the element names are localization keys so the panel reads in the
        /// editor language instead of mixing English labels into a Korean UI.
        /// </summary>
        private static readonly (string categoryKey, (DesignerElementType type, string labelKey)[] items)[] Categories =
        {
            ("shell.library.category.containers", new[] { (DesignerElementType.Panel, "component.panel"), (DesignerElementType.Card, "component.card"), (DesignerElementType.Container, "component.container"), (DesignerElementType.Modal, "component.modal") }),
            ("shell.library.category.textMedia", new[] { (DesignerElementType.Label, "component.label"), (DesignerElementType.Image, "component.image") }),
            ("shell.library.category.controls", new[] { (DesignerElementType.Button, "component.button"), (DesignerElementType.IconButton, "component.iconButton"), (DesignerElementType.ChoiceList, "component.choiceList") }),
            ("shell.library.category.feedback", new[] { (DesignerElementType.Toast, "component.toast"), (DesignerElementType.Tooltip, "component.tooltip"), (DesignerElementType.ProgressBar, "component.progressBar"), (DesignerElementType.Spinner, "component.spinner") }),
            ("shell.library.category.data", new[] { (DesignerElementType.List, "component.list"), (DesignerElementType.Grid, "component.grid"), (DesignerElementType.Slot, "component.slot"), (DesignerElementType.Skeleton, "component.skeleton") }),
        };

        private readonly NexUIDesignerContext _context;
        private readonly VisualElement _content;
        private readonly List<Button> _cards = new();
        private string _filter = "";

        public NexUIComponentsPanel(NexUIDesignerContext context)
        {
            _context = context;
            AddToClassList("nexui-components-panel");

            var search = new ToolbarSearchField { tooltip = DesignerLocalization.T("tooltip.palette.search") };
            search.RegisterValueChangedCallback(evt =>
            {
                _filter = evt.newValue ?? "";
                RefreshFilter();
            });
            Add(search);

            _content = new ScrollView();
            _content.AddToClassList("nexui-sidebar-scroll");
            Add(_content);

            BuildRecent();
            foreach (var category in Categories)
                BuildCategory(category.categoryKey, category.items);
        }

        private void BuildRecent()
        {
            var foldout = new Foldout { text = DesignerLocalization.T("shell.library.recent"), value = true };
            foldout.AddToClassList("nexui-sidebar-foldout");
            var grid = new VisualElement();
            grid.AddToClassList("nexui-component-grid");
            foldout.Add(grid);

            grid.Add(CreateCard(DesignerElementType.Panel, "component.panel"));
            grid.Add(CreateCard(DesignerElementType.Button, "component.button"));
            grid.Add(CreateCard(DesignerElementType.Label, "component.label"));
            grid.Add(CreateCard(DesignerElementType.Image, "component.image"));

            _content.Add(foldout);
        }

        private void BuildCategory(string titleKey, IReadOnlyList<(DesignerElementType type, string labelKey)> items)
        {
            // The pref key stays on the stable localization key, not the translated title, so the
            // expanded/collapsed state survives a language switch.
            var prefKey = "NexUI.Designer.Components." + titleKey;
            var foldout = new Foldout { text = DesignerLocalization.T(titleKey), value = EditorPrefs.GetBool(prefKey, true) };
            foldout.AddToClassList("nexui-sidebar-foldout");
            foldout.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(prefKey, evt.newValue));

            var grid = new VisualElement();
            grid.AddToClassList("nexui-component-grid");
            foldout.Add(grid);

            foreach (var item in items)
                grid.Add(CreateCard(item.type, item.labelKey));

            _content.Add(foldout);
        }

        private Button CreateCard(DesignerElementType type, string labelKey)
        {
            var label = DesignerLocalization.T(labelKey);
            var button = new Button(() => _context.CreateMetadataElement(type))
            {
                text = IconFor(type) + " " + label,
                tooltip = string.Format(DesignerLocalization.T("tooltip.palette.addComponent"), label)
            };
            button.AddToClassList("nexui-component-card");
            button.userData = label;
            _cards.Add(button);
            return button;
        }

        private void RefreshFilter()
        {
            foreach (var card in _cards)
            {
                var label = card.userData as string ?? "";
                card.style.display = string.IsNullOrEmpty(_filter) || label.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
        }

        private static string IconFor(DesignerElementType type)
        {
            return type switch
            {
                DesignerElementType.Button => "[B]",
                DesignerElementType.IconButton => "[I]",
                DesignerElementType.Label => "[T]",
                DesignerElementType.Image => "[M]",
                DesignerElementType.List => "[L]",
                DesignerElementType.Grid => "[G]",
                DesignerElementType.Modal => "[O]",
                _ => "[ ]"
            };
        }
    }
}

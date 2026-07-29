using System;
using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Panels
{
    public sealed class NexUIDesignerPalette : VisualElement
    {
        private readonly VisualElement _grid;
        private readonly Dictionary<Button, string> _buttonLabels = new();
        private readonly List<Foldout> _categoryFoldouts = new();

        public NexUIDesignerPalette(NexUIDesignerContext context)
        {
            AddToClassList("nexui-panel");
            AddToClassList("nexui-palette");
            Add(new Label("Components") { name = "PanelTitle" });

            var search = new ToolbarSearchField { tooltip = DesignerLocalization.T("tooltip.palette.search") };
            search.RegisterValueChangedCallback(evt => ApplyFilter(evt.newValue));
            Add(search);

            _grid = new VisualElement();
            _grid.AddToClassList("nexui-palette-grid");
            Add(_grid);

            // Folders and entries come from the component registry (see DesignerComponentPalette),
            // so NexUI components and Unity's own uGUI / UI Toolkit controls all appear here without
            // this panel keeping its own list.
            foreach (var group in DesignerComponentPalette.BuildGroups())
            {
                var prefsKey = "NexUI.Designer.Palette.Category." + group.GroupId;
                var defaultOpen = group.Family == DesignerComponentFamily.NexUI;
                var foldout = new Foldout { text = group.Title, value = EditorPrefs.GetBool(prefsKey, defaultOpen) };
                foldout.AddToClassList("nexui-palette-category");
                foldout.RegisterValueChangedCallback(evt =>
                {
                    if (evt.target == foldout) EditorPrefs.SetBool(prefsKey, evt.newValue);
                });
                _grid.Add(foldout);
                _categoryFoldouts.Add(foldout);

                foreach (var descriptor in group.Items)
                {
                    var label = DesignerComponentPalette.DisplayName(descriptor);
                    var button = AddButton(foldout, context, descriptor, label);
                    _buttonLabels[button] = label + " " + descriptor.TypeId;
                }
            }

            Add(new Label("Selection actions") { name = "PanelSubtitle" });
            var align = new VisualElement();
            align.AddToClassList("nexui-align-grid");
            Add(align);
            AddActionRow(align,
                "Left", () => context.AlignSelected("left"), DesignerLocalization.T("tooltip.palette.alignLeft"),
                "Center", () => context.AlignSelected("centerX"), DesignerLocalization.T("tooltip.palette.alignCenterX"));
            AddActionRow(align,
                "Right", () => context.AlignSelected("right"), DesignerLocalization.T("tooltip.palette.alignRight"),
                "Top", () => context.AlignSelected("top"), DesignerLocalization.T("tooltip.palette.alignTop"));
            AddActionRow(align,
                "Middle", () => context.AlignSelected("centerY"), DesignerLocalization.T("tooltip.palette.alignCenterY"),
                "Bottom", () => context.AlignSelected("bottom"), DesignerLocalization.T("tooltip.palette.alignBottom"));
            AddActionRow(align,
                "Fill", () => context.AlignSelected("fill"), DesignerLocalization.T("tooltip.palette.fill"),
                "Copy", () => context.DuplicateSelectedMetadata(), DesignerLocalization.T("tooltip.palette.copy"));
            AddActionRow(align,
                "Delete", () => context.DeleteSelectedMetadata(), DesignerLocalization.T("tooltip.palette.delete"),
                null, null, null);
        }

        private void ApplyFilter(string filter)
        {
            var hasFilter = !string.IsNullOrEmpty(filter);
            foreach (var foldout in _categoryFoldouts)
            {
                var anyVisible = false;
                // Entries live in the Foldout's content container, not directly under it, so match
                // against the tracked buttons rather than the Foldout's immediate children.
                foreach (var pair in _buttonLabels)
                {
                    if (!foldout.Contains(pair.Key)) continue;
                    var visible = !hasFilter || pair.Value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                    pair.Key.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                    if (visible) anyVisible = true;
                }

                foldout.style.display = anyVisible ? DisplayStyle.Flex : DisplayStyle.None;
                if (hasFilter && anyVisible) foldout.value = true; // auto-expand so matches are visible
            }
        }

        private static Button AddButton(VisualElement parent, NexUIDesignerContext context,
            DesignerComponentDescriptor descriptor, string label)
        {
            var button = new Button(() => context.CreateMetadataElement(descriptor.TypeId))
            {
                text = label,
                tooltip = string.Format(DesignerLocalization.T("tooltip.palette.addComponent"), label)
                          + (string.IsNullOrEmpty(descriptor.Description) ? "" : "\n" + descriptor.Description)
            };
            button.AddToClassList("nexui-palette-button");
            parent.Add(button);
            return button;
        }

        private static void AddActionRow(VisualElement parent,
            string leftLabel, System.Action leftAction, string leftTooltip,
            string rightLabel, System.Action rightAction, string rightTooltip)
        {
            var row = new VisualElement();
            row.AddToClassList("nexui-palette-row");
            parent.Add(row);
            AddAction(row, leftLabel, leftAction, leftTooltip);
            if (!string.IsNullOrEmpty(rightLabel))
                AddAction(row, rightLabel, rightAction, rightTooltip);
        }

        private static void AddAction(VisualElement parent, string label, System.Action action, string tooltip)
        {
            var button = new Button(action) { text = label, tooltip = tooltip };
            button.AddToClassList("nexui-align-button");
            parent.Add(button);
        }
    }
}

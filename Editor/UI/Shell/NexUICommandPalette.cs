using System;
using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Commands;
using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.Utilities;
using emiteat.NexUI.Designer.Editor.Viewport;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.UI.Shell
{
    public sealed class NexUICommandPalette : VisualElement
    {
        private readonly NexUIDesignerContext _context;
        private readonly TextField _search;
        private readonly ScrollView _results;
        private readonly List<Entry> _entries = new();

        public NexUICommandPalette(NexUIDesignerContext context)
        {
            _context = context;
            AddToClassList("nexui-command-palette-overlay");
            style.display = DisplayStyle.None;

            var panel = new VisualElement();
            panel.AddToClassList("nexui-command-palette");
            Add(panel);

            _search = new TextField { tooltip = "Search commands." };
            _search.AddToClassList("nexui-command-search");
            _search.RegisterValueChangedCallback(_ => Refresh());
            panel.Add(_search);

            _results = new ScrollView();
            _results.AddToClassList("nexui-command-results");
            panel.Add(_results);

            RegisterCallback<KeyDownEvent>(OnKeyDown);
            BuildEntries();
        }

        public void Toggle()
        {
            if (resolvedStyle.display == DisplayStyle.None) Open();
            else Close();
        }

        public void Open()
        {
            style.display = DisplayStyle.Flex;
            _search.value = string.Empty;
            Refresh();
            _search.Focus();
        }

        public void Close()
        {
            style.display = DisplayStyle.None;
        }

        private void BuildEntries()
        {
            _entries.Clear();
            foreach (var command in UIDesignerCommandDispatcher.Commands.Values)
            {
                var captured = command;
                _entries.Add(new Entry(captured.DisplayName, captured.Id, () => captured.Execute(_context), () => captured.CanExecute(_context)));
            }

            foreach (DesignerElementType type in Enum.GetValues(typeof(DesignerElementType)))
            {
                if (type == DesignerElementType.Custom) continue;
                var captured = type;
                _entries.Add(new Entry("Add " + captured, "component " + captured, () => _context.CreateMetadataElement(captured), () => _context.Metadata != null));
            }

            _entries.Add(new Entry("Validate", "screen validation", _context.Validate, () => true));
            _entries.Add(new Entry("Save", "screen metadata", () => _context.Save(), () => _context.CurrentScreen != null));
            _entries.Add(new Entry("Toggle Grid Snap", "canvas snap", () => _context.SetSnap(!_context.SnapEnabled), () => true));
            _entries.Add(new Entry("Open Layers", "sidebar", () => _context.SetSidebarTab(DesignerSidebarTab.Layers), () => true));
            _entries.Add(new Entry("Open Components", "sidebar", () => _context.SetSidebarTab(DesignerSidebarTab.Components), () => true));
            _entries.Add(new Entry("Show Project Assets", "assets sprites fonts project", EditorUtility.FocusProjectWindow, () => true));
            _entries.Add(new Entry("Open Validation Drawer", "drawer", () => _context.SetBottomTab(DesignerBottomTab.Validation), () => true));
            _entries.Add(new Entry("Open History Drawer", "drawer", () => _context.SetBottomTab(DesignerBottomTab.History), () => true));
            _entries.Add(new Entry("Open Graph Drawer", "drawer", () => _context.SetBottomTab(DesignerBottomTab.Graph), () => true));
            _entries.Add(new Entry(DesignerLocalization.T("utilities.command.open"),
                DesignerLocalization.T("utilities.command.keywords"), NexUIUtilitiesWindow.Open, () => true));

            foreach (var preset in DesignerResolutionPreset.Defaults)
            {
                var captured = preset;
                _entries.Add(new Entry("Resolution " + captured.Name, "frame canvas", () => _context.SetResolution(captured.Resolution), () => true));
            }
        }

        private void Refresh()
        {
            _results.Clear();
            var filter = _search.value ?? string.Empty;
            var shown = 0;
            foreach (var entry in _entries)
            {
                if (!entry.Matches(filter)) continue;
                var enabled = entry.CanExecute();
                var row = new Button(() =>
                {
                    if (!entry.CanExecute()) return;
                    entry.Execute();
                    Close();
                })
                {
                    text = entry.Title,
                    tooltip = entry.Keywords
                };
                row.SetEnabled(enabled);
                row.AddToClassList("nexui-command-row");
                _results.Add(row);
                shown++;
                if (shown >= 24) break;
            }
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                Close();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                foreach (var entry in _entries)
                {
                    if (!entry.Matches(_search.value ?? string.Empty) || !entry.CanExecute()) continue;
                    entry.Execute();
                    Close();
                    evt.StopPropagation();
                    return;
                }
            }
        }

        private readonly struct Entry
        {
            public readonly string Title;
            public readonly string Keywords;
            private readonly Action _execute;
            private readonly Func<bool> _canExecute;

            public Entry(string title, string keywords, Action execute, Func<bool> canExecute)
            {
                Title = title;
                Keywords = keywords;
                _execute = execute;
                _canExecute = canExecute;
            }

            public bool CanExecute() => _canExecute == null || _canExecute();
            public void Execute() => _execute?.Invoke();
            public bool Matches(string filter)
            {
                if (string.IsNullOrEmpty(filter)) return true;
                return Title.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || Keywords.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }
}

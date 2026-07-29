using System;
using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Commands;
using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.Utilities;
using emiteat.NexUI.Designer.Editor.Viewport;
using emiteat.NexUI.Designer.Editor.AI;
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

            _search = new TextField { tooltip = DesignerLocalization.T("shell.palette.search") };
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

            foreach (var descriptor in DesignerComponentRegistry.All)
            {
                if (descriptor == null || descriptor.IsGeneric || string.IsNullOrEmpty(descriptor.PaletteGroup)) continue;
                var captured = descriptor;
                var name = DesignerComponentPalette.DisplayName(captured);
                _entries.Add(new Entry(DesignerLocalization.T("shell.palette.add", name),
                    "component " + name + " " + captured.TypeId + " " + captured.Family,
                    () => _context.CreateMetadataElement(captured.TypeId), () => _context.Metadata != null));
            }

            _entries.Add(new Entry(DesignerLocalization.T("toolbar.validate"), "screen validation 검사", _context.Validate, () => true));
            _entries.Add(new Entry(DesignerLocalization.T("toolbar.save"), "screen metadata 저장", () => _context.Save(), () => _context.CurrentScreen != null));
            _entries.Add(new Entry(DesignerLocalization.T("shell.palette.toggleSnap"), "canvas snap grid 스냅 그리드", () => _context.SetSnap(!_context.SnapEnabled), () => true));
            AddPanelEntry("shell.tab.hierarchy", "sidebar layers 계층", () => _context.SetSidebarTab(DesignerSidebarTab.Layers));
            AddPanelEntry("shell.tab.library", "sidebar components palette 라이브러리 컴포넌트", () => _context.SetSidebarTab(DesignerSidebarTab.Components));
            AddPanelEntry("shell.tab.project", "assets sprites fonts materials browse sidebar 에셋 프로젝트", () => _context.SetSidebarTab(DesignerSidebarTab.Assets));
            _entries.Add(new Entry(DesignerLocalization.T("shell.palette.focusProject"), "assets project unity window 프로젝트 창", EditorUtility.FocusProjectWindow, () => true));
            AddPanelEntry("shell.tab.console", "drawer validation errors warnings 콘솔 검사", () => _context.SetBottomTab(DesignerBottomTab.Validation));
            AddPanelEntry("shell.tab.undoHistory", "drawer history undo 실행 취소 기록", () => _context.SetBottomTab(DesignerBottomTab.History));
            AddPanelEntry("shell.tab.screenGraph", "drawer graph binding 그래프", () => _context.SetBottomTab(DesignerBottomTab.Graph));
            AddPanelEntry("shell.tab.eventLog", "drawer preview command log 이벤트 로그", () => _context.SetBottomTab(DesignerBottomTab.Preview));
            _entries.Add(new Entry(DesignerLocalization.T("ai.command.open"),
                DesignerLocalization.T("ai.command.keywords"), NexUIAIWindow.Open, () => true));
            _entries.Add(new Entry(DesignerLocalization.T("utilities.command.open"),
                DesignerLocalization.T("utilities.command.keywords"), NexUIUtilitiesWindow.Open, () => true));

            foreach (var preset in DesignerResolutionPreset.Defaults)
            {
                var captured = preset;
                _entries.Add(new Entry(DesignerLocalization.T("shell.canvas.resolution") + " " + captured.Name,
                    "resolution frame canvas 해상도", () => _context.SetResolution(captured.Resolution), () => true));
            }
        }

        /// <summary>Adds an "Open &lt;panel&gt;" entry named after the panel's own tab label.</summary>
        private void AddPanelEntry(string titleKey, string keywords, Action open)
        {
            _entries.Add(new Entry(DesignerLocalization.T("shell.palette.open", DesignerLocalization.T(titleKey)),
                keywords, open, () => true));
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

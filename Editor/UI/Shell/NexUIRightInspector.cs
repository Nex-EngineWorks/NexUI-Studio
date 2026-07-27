using System;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Inspectors;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.UI.Shell
{
    /// <summary>
    /// The single Inspector host used by NexUI Designer. Sections are supplied by
    /// <see cref="DesignerInspectorRegistry"/> so selection, search and Beginner/Pro exposure
    /// rules cannot drift between multiple hard-coded Inspector implementations.
    /// </summary>
    public class NexUIRightInspector : VisualElement
    {
        private const string FoldoutPrefPrefix = "NexUI.Designer.Inspector.Section.";

        private readonly NexUIDesignerContext _context;
        private readonly Label _title;
        private readonly Label _subtitle;
        private readonly Button _mode;
        private readonly ToolbarSearchField _search;
        private readonly PopupField<DesignerInspectorWorkflow> _workflow;
        private readonly ScrollView _host;
        private string _lastTargetKey;

        public NexUIRightInspector(NexUIDesignerContext context)
        {
            _context = context;
            AddToClassList("nexui-right-inspector");
            AddToClassList("nexui-inspector");
            AddToClassList("nexui-unified-inspector");

            var header = new VisualElement();
            header.AddToClassList("nexui-inspector-header");
            var heading = new VisualElement();
            heading.AddToClassList("nexui-inspector-heading");
            _title = new Label();
            _title.AddToClassList("nexui-inspector-selection-title");
            _subtitle = new Label();
            _subtitle.AddToClassList("nexui-inspector-selection-subtitle");
            heading.Add(_title);
            heading.Add(_subtitle);
            header.Add(heading);

            _mode = new Button(ToggleMode);
            _mode.AddToClassList("nexui-inspector-mode");
            header.Add(_mode);
            Add(header);

            var tools = new VisualElement();
            tools.AddToClassList("nexui-inspector-tools");
            _search = new ToolbarSearchField { tooltip = DesignerLocalization.T("inspector.unified.searchTooltip") };
            _search.AddToClassList("nexui-inspector-search");
            _search.RegisterValueChangedCallback(_ => RebuildSections());
            tools.Add(_search);

            _workflow = new PopupField<DesignerInspectorWorkflow>(
                new System.Collections.Generic.List<DesignerInspectorWorkflow>
                {
                    DesignerInspectorWorkflow.All,
                    DesignerInspectorWorkflow.Build,
                    DesignerInspectorWorkflow.Connect,
                    DesignerInspectorWorkflow.Animate,
                    DesignerInspectorWorkflow.Verify,
                    DesignerInspectorWorkflow.Advanced
                }, 0)
            {
                tooltip = DesignerLocalization.T("inspector.unified.workflowTooltip")
            };
            _workflow.AddToClassList("nexui-inspector-workflow");
            _workflow.RegisterValueChangedCallback(_ => RebuildSections());
            tools.Add(_workflow);
            Add(tools);

            _host = new ScrollView();
            _host.AddToClassList("nexui-inspector-host");
            Add(_host);

            var subscriptions = new ContextBoundSubscriptions(this);
            subscriptions.Add<DesignerElementMetadata>(h => context.MetadataSelectionChanged += h,
                h => context.MetadataSelectionChanged -= h, _ => RebuildForTarget());
            subscriptions.Add<System.Collections.Generic.IReadOnlyList<DesignerElementMetadata>>(h => context.MultiSelectionChanged += h,
                h => context.MultiSelectionChanged -= h, _ => RebuildForTarget());
            subscriptions.Add<emiteat.NexUI.Core.UIScreenDefinition>(h => context.ScreenChanged += h,
                h => context.ScreenChanged -= h, _ => RebuildForTarget());
            subscriptions.Add<DesignerMode>(h => DesignerEditMode.Changed += h,
                h => DesignerEditMode.Changed -= h, _ => RebuildSections());

            RebuildForTarget();
        }

        private void ToggleMode()
        {
            DesignerEditMode.Current = DesignerEditMode.IsAdvanced ? DesignerMode.Simple : DesignerMode.Advanced;
        }

        private void RebuildForTarget()
        {
            var targetKey = CurrentTargetKey();
            if (!string.Equals(_lastTargetKey, targetKey, StringComparison.Ordinal))
            {
                _lastTargetKey = targetKey;
                _search.SetValueWithoutNotify(string.Empty);
                _workflow.SetValueWithoutNotify(DesignerInspectorWorkflow.All);
            }
            RebuildSections();
        }

        private void RebuildSections()
        {
            RefreshHeader();
            var scroll = _host.scrollOffset;
            _host.Clear();

            var query = (_search.value ?? string.Empty).Trim();
            var shown = 0;
            var hiddenByMode = 0;
            foreach (var descriptor in DesignerInspectorRegistry.All)
            {
                if (!descriptor.AppliesTo(_context)) continue;
                if (_workflow.value != DesignerInspectorWorkflow.All && descriptor.Workflow != _workflow.value) continue;
                if (!descriptor.Matches(query)) continue;

                if (!DesignerEditMode.IsAdvanced && descriptor.Exposure > DesignerInspectorExposure.Common)
                {
                    hiddenByMode++;
                    continue;
                }

                _host.Add(BuildSection(descriptor));
                shown++;
            }

            if (hiddenByMode > 0)
            {
                var reveal = new Button(() => DesignerEditMode.Current = DesignerMode.Advanced)
                {
                    text = DesignerLocalization.T("inspector.unified.proHidden", hiddenByMode)
                };
                reveal.AddToClassList("nexui-inspector-reveal-pro");
                _host.Add(reveal);
            }

            if (shown == 0)
            {
                var empty = new Label(string.IsNullOrEmpty(query)
                    ? DesignerLocalization.T("inspector.unified.emptySelection")
                    : DesignerLocalization.T("inspector.unified.emptySearch", query));
                empty.AddToClassList("nexui-inspector-empty");
                _host.Add(empty);
            }

            _host.schedule.Execute(() => _host.scrollOffset = scroll);
        }

        private VisualElement BuildSection(DesignerInspectorSectionDescriptor descriptor)
        {
            var foldout = new Foldout
            {
                text = descriptor.Title,
                value = EditorPrefs.GetBool(FoldoutPrefPrefix + descriptor.Id, DefaultExpanded(descriptor))
            };
            foldout.AddToClassList("nexui-unified-inspector-section");
            foldout.AddToClassList("workflow-" + descriptor.Workflow.ToString().ToLowerInvariant());
            foldout.tooltip = descriptor.Keywords;
            foldout.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(FoldoutPrefPrefix + descriptor.Id, evt.newValue));
            foldout.RegisterCallback<ContextClickEvent>(evt =>
            {
                ShowSectionMenu();
                evt.StopPropagation();
            });

            var content = descriptor.Create(_context);
            var duplicateTitle = content.Q<Label>("SectionTitle");
            duplicateTitle?.RemoveFromHierarchy();
            var duplicatePanelTitle = content.Q<Label>("PanelTitle");
            duplicatePanelTitle?.RemoveFromHierarchy();
            content.RemoveFromClassList("nexui-inspector-section");
            content.RemoveFromClassList("nexui-panel");
            content.RemoveFromClassList("nexui-bottom-card");
            content.style.flexGrow = 0;
            content.AddToClassList("nexui-unified-inspector-content");
            foldout.Add(content);
            return foldout;
        }

        private static bool DefaultExpanded(DesignerInspectorSectionDescriptor descriptor)
            => descriptor.Exposure == DesignerInspectorExposure.Essential;

        /// <summary>
        /// Right-click menu on a section header. Unity's component headers offer the same kind of
        /// bulk foldout control; here it also clears the search/workflow filter, which is the
        /// usual reason a section the user expected is not on screen.
        /// </summary>
        private void ShowSectionMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.inspector.expandAll")), false, () => SetAllSections(true));
            menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.inspector.collapseAll")), false, () => SetAllSections(false));
            menu.AddSeparator("");

            var hasFilter = !string.IsNullOrEmpty(_search.value) || _workflow.value != DesignerInspectorWorkflow.All;
            if (hasFilter)
                menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.inspector.resetFilters")), false, () =>
                {
                    _search.SetValueWithoutNotify(string.Empty);
                    _workflow.SetValueWithoutNotify(DesignerInspectorWorkflow.All);
                    RebuildSections();
                });
            else
                menu.AddDisabledItem(new GUIContent(DesignerLocalization.T("ctx.inspector.resetFilters")));

            menu.AddSeparator("");
            var advanced = DesignerEditMode.IsAdvanced;
            menu.AddItem(new GUIContent(DesignerLocalization.T("shell.mode.normal")), !advanced,
                () => DesignerEditMode.Current = DesignerMode.Simple);
            menu.AddItem(new GUIContent(DesignerLocalization.T("shell.mode.advanced")), advanced,
                () => DesignerEditMode.Current = DesignerMode.Advanced);

            menu.ShowAsContext();
        }

        private void SetAllSections(bool expanded)
        {
            foreach (var descriptor in DesignerInspectorRegistry.All)
                EditorPrefs.SetBool(FoldoutPrefPrefix + descriptor.Id, expanded);
            RebuildSections();
        }

        private void RefreshHeader()
        {
            _mode.text = DesignerEditMode.IsAdvanced
                ? DesignerLocalization.T("inspector.unified.pro")
                : DesignerLocalization.T("inspector.unified.beginner");
            _mode.tooltip = DesignerEditMode.IsAdvanced
                ? DesignerLocalization.T("inspector.unified.proTooltip")
                : DesignerLocalization.T("inspector.unified.beginnerTooltip");

            var count = _context.SelectedElements.Count;
            if (count == 0)
            {
                _title.text = _context.CurrentScreen == null
                    ? DesignerLocalization.T("inspector.unified.noScreen")
                    : _context.CurrentScreen.ScreenId;
                _subtitle.text = _context.CurrentScreen == null
                    ? DesignerLocalization.T("inspector.unified.selectScreen")
                    : DesignerLocalization.T("inspector.unified.screenSubtitle", _context.Backend);
                return;
            }

            if (count > 1)
            {
                _title.text = DesignerLocalization.T("inspector.unified.elementCount", count);
                _subtitle.text = DesignerLocalization.T("inspector.unified.multiSubtitle", _context.Backend);
                return;
            }

            var element = _context.SelectedElements[0];
            var descriptor = DesignerComponentRegistry.Get(element.elementType);
            _title.text = string.IsNullOrWhiteSpace(element.displayName) ? element.elementId : element.displayName;
            var support = _context.Backend == emiteat.NexUI.Abstractions.UIRenderBackend.UGUI
                ? descriptor.UGUISupport
                : descriptor.UIToolkitSupport;
            _subtitle.text = DesignerLocalization.T("inspector.unified.elementSubtitle", element.elementType, _context.Backend, support);
        }

        private string CurrentTargetKey()
        {
            var count = _context.SelectedElements.Count;
            if (count == 0) return "screen:" + (_context.CurrentScreen == null ? "none" : _context.CurrentScreen.ScreenId);
            if (count > 1) return "multi:" + count;
            return "element:" + _context.SelectedElements[0].elementId;
        }
    }
}

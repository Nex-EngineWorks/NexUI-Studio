using System;
using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Inspectors;
using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.UI.Controls;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.UI.Shell
{
    /// <summary>
    /// The single Inspector host used by NexUI Designer, laid out the way Unity lays out a
    /// GameObject: an identity header, the transform, the element's component stack, and then only
    /// the features the element actually uses - with Add Component underneath.
    /// </summary>
    /// <remarks>
    /// The previous host showed every registered section behind a workflow tab strip, so the same
    /// content was foldered twice (tab, then foldout) and an untouched element still listed twenty
    /// sections. Sections now declare a <see cref="DesignerInspectorSlot"/> and, for features, when
    /// they are in use, so the Inspector is as long as the element is complex.
    ///
    /// A feature that is not in use is not gone: Add Component ▸ NexUI Features adds it for the
    /// session, exactly like adding a component to reveal its fields.
    /// </remarks>
    public class NexUIRightInspector : VisualElement
    {
        private const string AddedFeaturesPrefix = "NexUI.Designer.Inspector.Features.";

        private readonly NexUIDesignerContext _context;
        private readonly VisualElement _header;
        private readonly Toggle _active;
        private readonly TextField _name;
        private readonly Label _title;
        private readonly Label _subtitle;
        private readonly ToolbarSearchField _search;
        private readonly ScrollView _host;
        private ElementComponentsInspector _components;
        private string _lastTargetKey;

        public NexUIRightInspector(NexUIDesignerContext context)
        {
            _context = context;
            AddToClassList("nexui-right-inspector");
            AddToClassList("nexui-inspector");
            AddToClassList("nexui-unified-inspector");

            Add(new NexUIPaneHeader(DesignerLocalization.T("pane.inspector"),
                    DesignerLocalization.T("pane.inspector.detail"))
                .WithDetachButton(() => NexUIPaneWindow.Open(DesignerPaneKind.Inspector),
                    DesignerLocalization.T("pane.detach.tooltip")));

            // ---- Identity header, mirroring Unity's GameObject header --------------------------
            _header = new VisualElement();
            _header.AddToClassList("nexui-inspector-identity");

            var identityRow = new VisualElement();
            identityRow.AddToClassList("nexui-inspector-identity-row");

            _active = new Toggle { tooltip = DesignerLocalization.T("inspector.identity.activeTooltip") };
            _active.AddToClassList("nexui-inspector-identity-active");
            _active.RegisterValueChangedCallback(evt =>
            {
                var element = SingleElement();
                if (element == null) return;
                _context.UpdateElement(element, e => e.runtimeVisible = evt.newValue, "Toggle NexUI Element Active");
            });
            identityRow.Add(_active);

            _name = new TextField { isDelayed = true, tooltip = DesignerLocalization.T("inspector.identity.nameTooltip") };
            _name.AddToClassList("nexui-inspector-identity-name");
            _name.RegisterValueChangedCallback(evt =>
            {
                var element = SingleElement();
                if (element == null || string.IsNullOrWhiteSpace(evt.newValue)) return;
                _context.UpdateElement(element, e => e.displayName = evt.newValue.Trim(), "Rename NexUI Element");
            });
            identityRow.Add(_name);

            _title = new Label();
            _title.AddToClassList("nexui-inspector-selection-title");
            identityRow.Add(_title);

            var overflow = new Button(ShowOverflowMenu) { text = "⋮", tooltip = DesignerLocalization.T("inspector.overflowTooltip") };
            overflow.AddToClassList("nexui-inspector-overflow");
            identityRow.Add(overflow);
            _header.Add(identityRow);

            _subtitle = new Label();
            _subtitle.AddToClassList("nexui-inspector-selection-subtitle");
            _header.Add(_subtitle);
            Add(_header);

            _search = new ToolbarSearchField { tooltip = DesignerLocalization.T("inspector.unified.searchTooltip") };
            _search.AddToClassList("nexui-inspector-search");
            _search.RegisterValueChangedCallback(_ => RebuildSections());
            Add(_search);

            _host = new ScrollView();
            _host.AddToClassList("nexui-inspector-host");
            Add(_host);

            var subscriptions = new ContextBoundSubscriptions(this);
            subscriptions.Add<DesignerElementMetadata>(h => context.MetadataSelectionChanged += h,
                h => context.MetadataSelectionChanged -= h, _ => RebuildForTarget());
            subscriptions.Add<IReadOnlyList<DesignerElementMetadata>>(h => context.MultiSelectionChanged += h,
                h => context.MultiSelectionChanged -= h, _ => RebuildForTarget());
            subscriptions.Add<emiteat.NexUI.Core.UIScreenDefinition>(h => context.ScreenChanged += h,
                h => context.ScreenChanged -= h, _ => RebuildForTarget());
            subscriptions.Add<DesignerMode>(h => DesignerEditMode.Changed += h,
                h => DesignerEditMode.Changed -= h, _ => RebuildSections());

            RebuildForTarget();
        }

        private DesignerElementMetadata SingleElement()
            => _context.SelectedElements.Count == 1 ? _context.SelectedElements[0] : null;

        private void RebuildForTarget()
        {
            var targetKey = CurrentTargetKey();
            if (!string.Equals(_lastTargetKey, targetKey, StringComparison.Ordinal))
            {
                _lastTargetKey = targetKey;
                _search.SetValueWithoutNotify(string.Empty);
            }
            RebuildSections();
        }

        private void RebuildSections()
        {
            RefreshHeader();
            var scroll = _host.scrollOffset;
            _host.Clear();
            _components = null;

            var query = (_search.value ?? string.Empty).Trim();
            var searching = !string.IsNullOrEmpty(query);
            var added = AddedFeatures();
            var shown = 0;
            var hiddenByMode = 0;
            var dormantFeatures = 0;

            foreach (var descriptor in Ordered())
            {
                if (!descriptor.AppliesTo(_context)) continue;
                if (!descriptor.Matches(query)) continue;

                if (!DesignerEditMode.IsAdvanced && descriptor.Exposure > DesignerInspectorExposure.Common)
                {
                    hiddenByMode++;
                    continue;
                }

                // A dormant feature stays off the stack unless the user added it - or is searching
                // for it, in which case hiding the one thing they asked for would be perverse.
                var inUse = descriptor.IsInUseBy(_context) || added.Contains(descriptor.Id);
                if (!inUse && !searching)
                {
                    dormantFeatures++;
                    continue;
                }

                _host.Add(BuildBlock(descriptor, searching, inUse));
                shown++;
            }

            if (_context.SelectedElements.Count == 1)
                _host.Add(AddComponentButton(dormantFeatures));

            if (hiddenByMode > 0)
            {
                var reveal = new Button(() => DesignerEditMode.Current = DesignerMode.Advanced)
                {
                    text = DesignerLocalization.T("inspector.unified.proHidden", hiddenByMode)
                };
                reveal.AddToClassList("nexui-inspector-reveal-pro");
                _host.Add(reveal);
            }

            if (shown == 0 && hiddenByMode == 0) _host.Add(BuildEmptyState(query));

            _host.schedule.Execute(() => _host.scrollOffset = scroll);
        }

        /// <summary>Screen blocks, then transform, core, the component stack, then features.</summary>
        private IEnumerable<DesignerInspectorSectionDescriptor> Ordered()
        {
            foreach (var slot in new[]
                     {
                         DesignerInspectorSlot.Screen, DesignerInspectorSlot.Transform, DesignerInspectorSlot.Core,
                         DesignerInspectorSlot.Components, DesignerInspectorSlot.Feature
                     })
                foreach (var descriptor in DesignerInspectorRegistry.All)
                    if (descriptor.Slot == slot)
                        yield return descriptor;
        }

        private VisualElement BuildBlock(DesignerInspectorSectionDescriptor descriptor, bool expandForSearch, bool inUse)
        {
            // The component stack is not a section with a header: its cards *are* the stack, the
            // same way Unity draws one card per component rather than a "Components" foldout.
            if (descriptor.Slot == DesignerInspectorSlot.Components)
            {
                _components = new ElementComponentsInspector(_context, includeAddButton: false);
                _components.Q<Label>("SectionTitle")?.RemoveFromHierarchy();
                _components.RemoveFromClassList("nexui-inspector-section");
                _components.AddToClassList("nexui-inspector-component-stack");
                return _components;
            }

            if (expandForSearch) NexUIInspectorBlock.SetExpanded(descriptor.Id, true);

            var block = new NexUIInspectorBlock(
                descriptor.Id,
                descriptor.Title,
                Icon(descriptor.Slot),
                () => descriptor.Create(_context),
                // Unity opens every component it draws; a feature only reaches the stack when the
                // element uses it, so open is the right default for everything but diagnostics.
                defaultExpanded: descriptor.Exposure != DesignerInspectorExposure.Diagnostic,
                menu: generic => BuildBlockMenu(generic, descriptor, inUse),
                tooltipText: DesignerLocalization.T("inspector.unified.sectionTooltip",
                    descriptor.Title, ExposureTitle(descriptor.Exposure), descriptor.Keywords));
            block.AddToClassList("slot-" + descriptor.Slot.ToString().ToLowerInvariant());
            block.AddToClassList("exposure-" + descriptor.Exposure.ToString().ToLowerInvariant());
            return block;
        }

        private void BuildBlockMenu(GenericMenu menu, DesignerInspectorSectionDescriptor descriptor, bool inUse)
        {
            var manual = descriptor.Slot == DesignerInspectorSlot.Feature
                         && AddedFeatures().Contains(descriptor.Id)
                         && !descriptor.IsInUseBy(_context);
            if (manual)
                menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.inspector.removeSection")), false,
                    () => RemoveFeature(descriptor.Id));
            else if (descriptor.Slot == DesignerInspectorSlot.Feature && inUse)
                // Removing a section the element genuinely uses would hide data, not remove it.
                menu.AddDisabledItem(new GUIContent(DesignerLocalization.T("ctx.inspector.removeSectionInUse")));

            menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.inspector.expandAll")), false, () => SetAllBlocks(true));
            menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.inspector.collapseAll")), false, () => SetAllBlocks(false));
        }

        private VisualElement AddComponentButton(int dormantFeatures)
        {
            var button = new Button(ShowAddMenu)
            {
                text = DesignerLocalization.T("inspector.components.add"),
                tooltip = dormantFeatures > 0
                    ? DesignerLocalization.T("inspector.add.tooltipWithFeatures", dormantFeatures)
                    : DesignerLocalization.T("tooltip.inspector.addComponent")
            };
            button.AddToClassList("nexui-inspector-add-component");
            return button;
        }

        /// <summary>
        /// One menu for both kinds of addition: real components from the component registry, and
        /// the NexUI feature sections this element is not using yet.
        /// </summary>
        private void ShowAddMenu()
        {
            var element = SingleElement();
            if (element == null) return;

            var menu = new GenericMenu();
            _components?.PopulateAddComponentMenu(menu, element);

            var added = AddedFeatures();
            var features = new List<DesignerInspectorSectionDescriptor>();
            foreach (var descriptor in DesignerInspectorRegistry.All)
            {
                if (descriptor.Slot != DesignerInspectorSlot.Feature) continue;
                if (!descriptor.AppliesTo(_context)) continue;
                if (!DesignerEditMode.IsAdvanced && descriptor.Exposure > DesignerInspectorExposure.Common) continue;
                if (descriptor.IsInUseBy(_context) || added.Contains(descriptor.Id)) continue;
                features.Add(descriptor);
            }

            if (features.Count > 0)
            {
                menu.AddSeparator("");
                var group = DesignerLocalization.T("inspector.add.featureGroup");
                foreach (var descriptor in features)
                {
                    var id = descriptor.Id;
                    menu.AddItem(new GUIContent(group + "/" + descriptor.Title), false, () => AddFeature(id));
                }
            }

            menu.ShowAsContext();
        }

        // ---- Added-feature set --------------------------------------------------------------
        // Session-scoped rather than written into the metadata: adding an empty Motion section is a
        // view decision, and persisting it would dirty the asset for something the user may never
        // fill in. Once a value is entered the section is in use and shows on its own.

        private HashSet<string> AddedFeatures()
        {
            var stored = SessionState.GetString(AddedFeaturesPrefix + CurrentTargetKey(), string.Empty);
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in stored.Split(','))
                if (!string.IsNullOrEmpty(id)) set.Add(id);
            return set;
        }

        private void AddFeature(string id)
        {
            var set = AddedFeatures();
            if (!set.Add(id)) return;
            NexUIInspectorBlock.SetExpanded(id, true);
            SaveAddedFeatures(set);
            RebuildSections();
        }

        private void RemoveFeature(string id)
        {
            var set = AddedFeatures();
            if (!set.Remove(id)) return;
            SaveAddedFeatures(set);
            RebuildSections();
        }

        private void SaveAddedFeatures(HashSet<string> set)
            => SessionState.SetString(AddedFeaturesPrefix + CurrentTargetKey(), string.Join(",", set));

        private VisualElement BuildEmptyState(string query)
        {
            var empty = new VisualElement();
            empty.AddToClassList("nexui-inspector-empty");
            var icon = new Label(string.IsNullOrEmpty(query) ? "◇" : "⌕") { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("nexui-inspector-empty-icon");
            empty.Add(icon);
            var title = new Label(string.IsNullOrEmpty(query)
                ? DesignerLocalization.T("inspector.unified.emptySelection")
                : DesignerLocalization.T("inspector.unified.emptySearch", query));
            title.AddToClassList("nexui-inspector-empty-title");
            empty.Add(title);
            if (!string.IsNullOrEmpty(query))
            {
                var clear = new Button(() =>
                {
                    _search.SetValueWithoutNotify(string.Empty);
                    RebuildSections();
                })
                {
                    text = DesignerLocalization.T("inspector.unified.clearSearch"),
                    tooltip = DesignerLocalization.T("inspector.unified.clearSearchTooltip")
                };
                clear.AddToClassList("nexui-inspector-empty-action");
                empty.Add(clear);
            }
            return empty;
        }

        private static string ExposureTitle(DesignerInspectorExposure exposure)
            => DesignerLocalization.T("inspector.exposure." + exposure.ToString().ToLowerInvariant());

        private static string Icon(DesignerInspectorSlot slot)
        {
            switch (slot)
            {
                case DesignerInspectorSlot.Screen: return "▤";
                case DesignerInspectorSlot.Transform: return "⤢";
                case DesignerInspectorSlot.Core: return "◧";
                default: return "◆";
            }
        }

        /// <summary>
        /// The ⋮ menu next to the element name: bulk foldout control plus the Normal/Advanced
        /// switch, which used to sit in the header as a button of its own.
        /// </summary>
        private void ShowOverflowMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.inspector.expandAll")), false, () => SetAllBlocks(true));
            menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.inspector.collapseAll")), false, () => SetAllBlocks(false));

            menu.AddSeparator("");
            var advanced = DesignerEditMode.IsAdvanced;
            menu.AddItem(new GUIContent(DesignerLocalization.T("shell.mode.normal")), !advanced,
                () => DesignerEditMode.Current = DesignerMode.Simple);
            menu.AddItem(new GUIContent(DesignerLocalization.T("shell.mode.advanced")), advanced,
                () => DesignerEditMode.Current = DesignerMode.Advanced);

            menu.AddSeparator("");
            var hasSearch = !string.IsNullOrEmpty(_search.value);
            if (hasSearch)
                menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.inspector.resetFilters")), false, () =>
                {
                    _search.SetValueWithoutNotify(string.Empty);
                    RebuildSections();
                });
            else
                menu.AddDisabledItem(new GUIContent(DesignerLocalization.T("ctx.inspector.resetFilters")));

            var added = AddedFeatures();
            if (added.Count > 0)
                menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.inspector.clearAddedSections")), false, () =>
                {
                    SaveAddedFeatures(new HashSet<string>());
                    RebuildSections();
                });
            else
                menu.AddDisabledItem(new GUIContent(DesignerLocalization.T("ctx.inspector.clearAddedSections")));

            menu.ShowAsContext();
        }

        private void SetAllBlocks(bool expanded)
        {
            foreach (var descriptor in DesignerInspectorRegistry.All)
                NexUIInspectorBlock.SetExpanded(descriptor.Id, expanded);
            RebuildSections();
        }

        private void RefreshHeader()
        {
            var count = _context.SelectedElements.Count;
            var single = count == 1;
            _active.style.display = single ? DisplayStyle.Flex : DisplayStyle.None;
            _name.style.display = single ? DisplayStyle.Flex : DisplayStyle.None;
            _title.style.display = single ? DisplayStyle.None : DisplayStyle.Flex;

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
            _active.SetValueWithoutNotify(element.runtimeVisible);
            _name.SetValueWithoutNotify(string.IsNullOrWhiteSpace(element.displayName) ? element.elementId : element.displayName);
            var support = _context.Backend == emiteat.NexUI.Abstractions.UIRenderBackend.UGUI
                ? descriptor.UGUISupport
                : descriptor.UIToolkitSupport;
            _subtitle.text = DesignerLocalization.T("inspector.identity.subtitle",
                element.elementId, element.elementType, _context.Backend, support);
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

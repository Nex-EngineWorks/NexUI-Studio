using System;
using System.Collections.Generic;
using System.Text;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Components.Preview;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.UI.Panels
{
    public sealed class NexUIComponentsPanel : VisualElement
    {
        private const string FamilyFilterPrefKey = "NexUI.Designer.Components.FamilyFilter";

        private readonly NexUIDesignerContext _context;
        private readonly VisualElement _content;
        private readonly VisualElement _details;
        private readonly List<Button> _cards = new();
        private readonly List<Foldout> _foldouts = new();
        private string _filter = "";
        private FamilyFilter _familyFilter;

        /// <summary>
        /// Which component libraries the palette lists. The default shows everything; a project that
        /// only ships one backend can narrow the list without losing the other family's entries from
        /// screens that already use them.
        /// </summary>
        private enum FamilyFilter
        {
            All,
            NexUI,
            UGUI,
            UIToolkit
        }

        public NexUIComponentsPanel(NexUIDesignerContext context)
        {
            _context = context;
            AddToClassList("nexui-components-panel");

            _familyFilter = (FamilyFilter)EditorPrefs.GetInt(FamilyFilterPrefKey, (int)FamilyFilter.All);

            var search = new ToolbarSearchField { tooltip = DesignerLocalization.T("tooltip.palette.search") };
            search.RegisterValueChangedCallback(evt =>
            {
                _filter = evt.newValue ?? "";
                RefreshFilter();
            });
            Add(search);

            Add(BuildFamilyFilter());

            _details = new VisualElement();
            _details.AddToClassList("nexui-component-details");
            Add(_details);

            _content = new ScrollView();
            _content.AddToClassList("nexui-sidebar-scroll");
            Add(_content);

            Rebuild();
            ShowDetails(DesignerComponentRegistry.Get("Button"));
        }

        private VisualElement BuildFamilyFilter()
        {
            var choices = new List<FamilyFilter> { FamilyFilter.All, FamilyFilter.NexUI, FamilyFilter.UGUI, FamilyFilter.UIToolkit };
            var field = new PopupField<FamilyFilter>(DesignerLocalization.T("palette.family"), choices, _familyFilter, FamilyLabel, FamilyLabel)
            {
                tooltip = DesignerLocalization.T("tooltip.palette.family")
            };
            field.AddToClassList("nexui-palette-family");
            field.RegisterValueChangedCallback(evt =>
            {
                _familyFilter = evt.newValue;
                EditorPrefs.SetInt(FamilyFilterPrefKey, (int)_familyFilter);
                Rebuild();
            });
            return field;
        }

        private static string FamilyLabel(FamilyFilter value) => value switch
        {
            FamilyFilter.NexUI => DesignerLocalization.T("palette.family.nexui"),
            FamilyFilter.UGUI => DesignerLocalization.T("palette.family.ugui"),
            FamilyFilter.UIToolkit => DesignerLocalization.T("palette.family.uitk"),
            _ => DesignerLocalization.T("palette.family.all")
        };

        private void Rebuild()
        {
            _content.Clear();
            _cards.Clear();
            _foldouts.Clear();

            // The historical "Recent" shortcuts are NexUI entries. Do not leak them into a
            // backend-specific uGUI / UI Toolkit view, where the family filter is expected to be
            // strict.
            if (_familyFilter == FamilyFilter.All || _familyFilter == FamilyFilter.NexUI)
                BuildRecent();
            foreach (var group in DesignerComponentPalette.BuildGroups())
            {
                if (!ShowsFamily(group.Family)) continue;
                BuildGroup(group);
            }

            RefreshFilter();
        }

        private bool ShowsFamily(DesignerComponentFamily family) => _familyFilter switch
        {
            FamilyFilter.NexUI => family == DesignerComponentFamily.NexUI,
            FamilyFilter.UGUI => family == DesignerComponentFamily.UGUI,
            FamilyFilter.UIToolkit => family == DesignerComponentFamily.UIToolkit,
            _ => true
        };

        private void BuildRecent()
        {
            var foldout = new Foldout { text = DesignerLocalization.T("shell.library.recent"), value = true };
            foldout.AddToClassList("nexui-sidebar-foldout");
            var grid = new VisualElement();
            grid.AddToClassList("nexui-component-grid");
            foldout.Add(grid);

            foreach (var typeId in new[] { "Panel", "Button", "Label", "Image" })
                grid.Add(CreateCard(DesignerComponentRegistry.Get(typeId)));

            _content.Add(foldout);
            _foldouts.Add(foldout);
        }

        private void BuildGroup(DesignerPaletteGroupView group)
        {
            // The pref key stays on the stable group id, not the translated title, so the
            // expanded/collapsed state survives a language switch.
            var prefKey = "NexUI.Designer.Components." + group.GroupId;
            // Unity's own control libraries start collapsed: they are long, and a project usually
            // works in one of them at a time.
            var defaultOpen = group.Family == DesignerComponentFamily.NexUI;
            var foldout = new Foldout { text = group.Title, value = EditorPrefs.GetBool(prefKey, defaultOpen) };
            foldout.AddToClassList("nexui-sidebar-foldout");
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == foldout) EditorPrefs.SetBool(prefKey, evt.newValue);
            });

            var grid = new VisualElement();
            grid.AddToClassList("nexui-component-grid");
            foldout.Add(grid);

            foreach (var descriptor in group.Items)
                grid.Add(CreateCard(descriptor));

            _content.Add(foldout);
            _foldouts.Add(foldout);
        }

        private Button CreateCard(DesignerComponentDescriptor descriptor)
        {
            var label = DesignerComponentPalette.DisplayName(descriptor);
            var button = new Button(() => _context.CreateMetadataElement(descriptor.TypeId))
            {
                text = string.Empty,
                tooltip = BuildTooltip(descriptor, label)
            };
            button.AddToClassList("nexui-component-card");

            button.Add(CreatePreview(descriptor, compact: true));

            var caption = new VisualElement { pickingMode = PickingMode.Ignore };
            caption.AddToClassList("nexui-component-card-caption");

            var title = new Label(label) { pickingMode = PickingMode.Ignore };
            title.AddToClassList("nexui-component-card-title");
            caption.Add(title);

            var badge = new Label(FamilyBadge(descriptor.Family)) { pickingMode = PickingMode.Ignore };
            badge.AddToClassList("nexui-component-family-badge");
            badge.AddToClassList(FamilyClass(descriptor.Family));
            caption.Add(badge);
            button.Add(caption);

            // Both the localized label and the type id are searchable, so "UGUI.Toggle" and "토글"
            // both find the same entry.
            button.userData = label + " " + descriptor.TypeId + " " + descriptor.DisplayName;
            button.RegisterCallback<PointerEnterEvent>(_ => ShowDetails(descriptor));
            button.RegisterCallback<FocusInEvent>(_ => ShowDetails(descriptor));
            button.RegisterCallback<ContextClickEvent>(evt =>
            {
                ShowCardMenu(descriptor);
                evt.StopPropagation();
            });
            _cards.Add(button);
            return button;
        }

        /// <summary>
        /// Keeps a larger, persistent preview above the scrolling library. Native Unity tooltips
        /// still carry the full text summary, while this panel makes the component understandable
        /// without waiting for the tooltip delay.
        /// </summary>
        private void ShowDetails(DesignerComponentDescriptor descriptor)
        {
            if (descriptor == null || _details == null) return;
            _details.Clear();

            var body = new VisualElement { pickingMode = PickingMode.Ignore };
            body.AddToClassList("nexui-component-details-body");
            body.Add(CreatePreview(descriptor, compact: false));

            var copy = new VisualElement { pickingMode = PickingMode.Ignore };
            copy.AddToClassList("nexui-component-details-copy");
            var label = DesignerComponentPalette.DisplayName(descriptor);
            var title = new Label(label) { pickingMode = PickingMode.Ignore };
            title.AddToClassList("nexui-component-details-title");
            copy.Add(title);
            var type = new Label(descriptor.TypeId) { pickingMode = PickingMode.Ignore };
            type.AddToClassList("nexui-component-details-type");
            copy.Add(type);
            if (!string.IsNullOrEmpty(descriptor.Description))
            {
                var description = new Label(descriptor.Description) { pickingMode = PickingMode.Ignore };
                description.AddToClassList("nexui-component-details-description");
                copy.Add(description);
            }
            body.Add(copy);
            _details.Add(body);

            var support = new VisualElement { pickingMode = PickingMode.Ignore };
            support.AddToClassList("nexui-component-support-row");
            support.Add(SupportBadge("uGUI", descriptor.UGUISupport));
            support.Add(SupportBadge("UI Toolkit", descriptor.UIToolkitSupport));
            _details.Add(support);

            var capabilities = new Label(CapabilitySummary(descriptor)) { pickingMode = PickingMode.Ignore };
            capabilities.AddToClassList("nexui-component-details-capabilities");
            _details.Add(capabilities);
        }

        private static VisualElement CreatePreview(DesignerComponentDescriptor descriptor, bool compact)
        {
            var frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.AddToClassList(compact ? "nexui-component-card-preview" : "nexui-component-detail-preview");
            frame.AddToClassList(FamilyClass(descriptor.Family));

            var surface = new VisualElement { pickingMode = PickingMode.Ignore };
            surface.AddToClassList("nexui-component-preview-surface");
            surface.style.backgroundColor = new StyleColor(descriptor.DefaultColor);
            var radius = descriptor.DefaultShape == DesignerElementShape.Rectangle ? 2f : 6f;
            surface.style.borderTopLeftRadius = radius;
            surface.style.borderTopRightRadius = radius;
            surface.style.borderBottomLeftRadius = radius;
            surface.style.borderBottomRightRadius = radius;
            frame.Add(surface);

            var element = new DesignerElementMetadata
            {
                elementId = "palettePreview",
                displayName = descriptor.DisplayName,
                elementType = descriptor.TypeId,
                rect = new Rect(0, 0, descriptor.DefaultSize.x, descriptor.DefaultSize.y),
                text = string.IsNullOrEmpty(descriptor.DefaultText) ? descriptor.DisplayName : descriptor.DefaultText,
                tint = descriptor.DefaultColor,
                textColor = Color.white,
                fontSize = compact ? 10 : 12,
                previewValue = 60f,
                previewItemCount = compact ? 3 : 4
            };
            element.previewOptions.Add("One");
            element.previewOptions.Add("Two");
            element.previewOptions.Add("Three");

            var ctx = new DesignerPreviewContext(element, DesignerComponentState.Normal, compact ? 0.75f : 1f, false);
            DesignerComponentPreviewRegistry.Get(descriptor.TypeId).BuildPreview(surface, ctx);

            // Generic/container/text types intentionally have no virtual-parts renderer. Give those
            // a restrained label instead of leaving an indistinguishable empty color swatch.
            if (surface.childCount == 0)
            {
                var fallback = new Label(PreviewCaption(descriptor)) { pickingMode = PickingMode.Ignore };
                fallback.AddToClassList("nexui-component-preview-fallback");
                surface.Add(fallback);
            }
            return frame;
        }

        private static Label SupportBadge(string backend, DesignerBackendSupport support)
        {
            var badge = new Label(backend + " · " + SupportLabel(support)) { pickingMode = PickingMode.Ignore };
            badge.AddToClassList("nexui-component-support-badge");
            badge.AddToClassList("support-" + support.ToString().ToLowerInvariant());
            return badge;
        }

        private static string BuildTooltip(DesignerComponentDescriptor descriptor, string label)
        {
            var sb = new StringBuilder();
            sb.Append(label).Append("  [").Append(descriptor.TypeId).AppendLine("]");
            if (!string.IsNullOrEmpty(descriptor.Description)) sb.AppendLine(descriptor.Description);
            sb.AppendLine();
            sb.Append(DesignerLocalization.T("palette.tooltip.family")).Append(": ").Append(FamilyLabel(descriptor.Family)).AppendLine();
            sb.Append(DesignerLocalization.T("palette.tooltip.backends")).Append(": uGUI ").Append(SupportLabel(descriptor.UGUISupport))
              .Append(" · UI Toolkit ").Append(SupportLabel(descriptor.UIToolkitSupport)).AppendLine();
            sb.Append(DesignerLocalization.T("palette.tooltip.size")).Append(": ")
              .Append(Mathf.RoundToInt(descriptor.DefaultSize.x)).Append(" × ").Append(Mathf.RoundToInt(descriptor.DefaultSize.y)).AppendLine();
            sb.Append(DesignerLocalization.T("palette.tooltip.states")).Append(": ").Append(FlagList(descriptor.SupportedStates)).AppendLine();
            sb.Append(DesignerLocalization.T("palette.tooltip.bindings")).Append(": ").Append(FlagList(descriptor.SupportedBindings));
            if (descriptor.SupportedEvents.Count > 0)
                sb.AppendLine().Append(DesignerLocalization.T("palette.tooltip.events")).Append(": ")
                  .Append(string.Join(" · ", descriptor.SupportedEvents));
            sb.AppendLine().AppendLine().Append(DesignerLocalization.T("palette.tooltip.hint"));
            return sb.ToString();
        }

        private static string CapabilitySummary(DesignerComponentDescriptor descriptor)
        {
            var items = new List<string>();
            if (descriptor.IsContainer || descriptor.CanHaveChildren) items.Add(DesignerLocalization.T("palette.capability.children"));
            if (descriptor.IsInteractive) items.Add(DesignerLocalization.T("palette.capability.interactive"));
            if (descriptor.IsValueComponent) items.Add(DesignerLocalization.T("palette.capability.value"));
            if (descriptor.IsCollectionComponent) items.Add(DesignerLocalization.T("palette.capability.collection"));
            if (descriptor.IsOverlayComponent) items.Add(DesignerLocalization.T("palette.capability.overlay"));
            if (items.Count == 0) items.Add(DesignerLocalization.T("palette.capability.display"));
            return string.Join("  ·  ", items);
        }

        private static string FlagList(Enum value)
        {
            var text = value?.ToString();
            return string.IsNullOrEmpty(text) || text == "None"
                ? DesignerLocalization.T("palette.value.none")
                : text.Replace(", ", " · ");
        }

        private static string PreviewCaption(DesignerComponentDescriptor descriptor)
        {
            if (!string.IsNullOrEmpty(descriptor.DefaultText)) return descriptor.DefaultText;
            var name = DesignerComponentPalette.DisplayName(descriptor);
            return name.Length > 16 ? name.Substring(0, 15) + "…" : name;
        }

        private static string FamilyLabel(DesignerComponentFamily family) => family switch
        {
            DesignerComponentFamily.UGUI => DesignerLocalization.T("palette.family.ugui"),
            DesignerComponentFamily.UIToolkit => DesignerLocalization.T("palette.family.uitk"),
            _ => DesignerLocalization.T("palette.family.nexui")
        };

        private static string FamilyBadge(DesignerComponentFamily family) => family switch
        {
            DesignerComponentFamily.UGUI => "uGUI",
            DesignerComponentFamily.UIToolkit => "UITK",
            _ => "NexUI"
        };

        private static string FamilyClass(DesignerComponentFamily family) => family switch
        {
            DesignerComponentFamily.UGUI => "family-ugui",
            DesignerComponentFamily.UIToolkit => "family-uitk",
            _ => "family-nexui"
        };

        private static string SupportLabel(DesignerBackendSupport support) => support switch
        {
            DesignerBackendSupport.Full => DesignerLocalization.T("palette.support.full"),
            DesignerBackendSupport.Partial => DesignerLocalization.T("palette.support.partial"),
            DesignerBackendSupport.PreviewOnly => DesignerLocalization.T("palette.support.previewOnly"),
            _ => DesignerLocalization.T("palette.support.unsupported")
        };

        /// <summary>
        /// Right-clicking a Library entry offers where to put it, mirroring how Unity's own
        /// create menus distinguish "at the root" from "under the current selection".
        /// </summary>
        private void ShowCardMenu(DesignerComponentDescriptor descriptor)
        {
            var menu = new GenericMenu();
            var canAdd = _context.Metadata != null;
            var parent = _context.SelectedMetadata;

            if (canAdd)
                menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.library.add")), false,
                    () => _context.CreateMetadataElement(descriptor.TypeId));
            else
                menu.AddDisabledItem(new GUIContent(DesignerLocalization.T("ctx.library.add")));

            if (canAdd && parent != null)
                menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.library.addAsChild")), false, () =>
                    NexUIDesignerUndo.Group("Add NexUI Element As Child", () =>
                    {
                        var created = _context.CreateMetadataElement(descriptor.TypeId);
                        if (created != null) _context.ReparentElement(created, parent);
                    }));
            else
                menu.AddDisabledItem(new GUIContent(DesignerLocalization.T("ctx.library.addAsChild")));

            menu.ShowAsContext();
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

            // A search that matches a collapsed library would otherwise look like "no results".
            var searching = !string.IsNullOrEmpty(_filter);
            foreach (var foldout in _foldouts)
            {
                var anyVisible = false;
                foreach (var card in _cards)
                    if (foldout.Contains(card) && card.style.display != DisplayStyle.None) { anyVisible = true; break; }
                foldout.style.display = anyVisible || !searching ? DisplayStyle.Flex : DisplayStyle.None;
                if (searching && anyVisible) foldout.value = true;
            }
        }

    }
}

using System;
using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.Styles;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Components
{
    /// <summary>
    /// Unity's Add Component picker for project and engine MonoBehaviours, over
    /// <see cref="StudioComponentTypeIndex"/>.
    /// </summary>
    /// <remarks>
    /// This window is the only place a script is chosen. What it returns goes into the element's one
    /// component stack, exactly like a uGUI Image does - which is why there is no second "attached
    /// scripts" panel any more.
    /// </remarks>
    public sealed class StudioAddComponentPicker : EditorWindow
    {
        private Action<Type> _onSelect;

        /// <summary>Types already on the element, so the picker can explain why one is unavailable.</summary>
        private Func<Type, string> _blockedReason;

        private ToolbarSearchField _search;
        private ScrollView _results;
        private VisualElement _details;
        private Label _summary;

        public static void Open(Action<Type> onSelect, Func<Type, string> blockedReason = null)
        {
            var window = CreateInstance<StudioAddComponentPicker>();
            window._onSelect = onSelect;
            window._blockedReason = blockedReason;
            window.titleContent = new GUIContent(DesignerLocalization.T("attachedComponents.pickerTitle"));
            window.minSize = new Vector2(420, 360);
            window.maxSize = new Vector2(640, 720);
            window.ShowAuxWindow();
            window.Focus();
        }

        public void CreateGUI()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(DesignerStyleSheet.Path);
            if (styleSheet != null) rootVisualElement.styleSheets.Add(styleSheet);
            rootVisualElement.AddToClassList("nexui-designer-root");
            rootVisualElement.AddToClassList("nexui-component-picker");

            var header = new VisualElement();
            header.AddToClassList("nexui-component-picker-header");
            var title = new Label(DesignerLocalization.T("attachedComponents.pickerTitle"));
            title.AddToClassList("nexui-component-picker-title");
            header.Add(title);
            var subtitle = new Label(DesignerLocalization.T("attachedComponents.pickerDescription"));
            subtitle.AddToClassList("nexui-component-picker-subtitle");
            header.Add(subtitle);
            rootVisualElement.Add(header);

            _search = new ToolbarSearchField { tooltip = DesignerLocalization.T("attachedComponents.searchTooltip") };
            _search.AddToClassList("nexui-component-picker-search");
            _search.RegisterValueChangedCallback(_ => Rebuild());
            rootVisualElement.Add(_search);

            _details = new VisualElement();
            _details.AddToClassList("nexui-component-picker-details");
            rootVisualElement.Add(_details);

            _summary = new Label();
            _summary.AddToClassList("nexui-component-picker-summary");
            rootVisualElement.Add(_summary);

            _results = new ScrollView { style = { flexGrow = 1 } };
            _results.AddToClassList("nexui-component-picker-results");
            rootVisualElement.Add(_results);

            Rebuild();
            _search.Focus();
        }

        private void Rebuild()
        {
            if (_results == null) return;
            _results.Clear();

            var query = _search?.value?.Trim() ?? string.Empty;
            var groups = new SortedDictionary<string, List<StudioComponentTypeEntry>>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in StudioComponentTypeIndex.Search(query))
            {
                if (!groups.TryGetValue(entry.Category, out var items))
                    groups[entry.Category] = items = new List<StudioComponentTypeEntry>();
                items.Add(entry);
            }

            var count = 0;
            StudioComponentTypeEntry first = null;
            foreach (var pair in groups)
            {
                var foldout = new Foldout { text = pair.Key, value = query.Length > 0 || groups.Count <= 6 };
                foldout.AddToClassList("nexui-component-picker-group");
                foreach (var entry in pair.Value)
                {
                    first ??= entry;
                    count++;
                    foldout.Add(Result(entry));
                }
                _results.Add(foldout);
            }

            _summary.text = string.Format(DesignerLocalization.T("attachedComponents.resultCount"), count);
            if (first != null) ShowDetails(first);
            else
            {
                _details.Clear();
                var empty = new Label(DesignerLocalization.T("attachedComponents.noResults"));
                empty.AddToClassList("nexui-component-picker-empty");
                _details.Add(empty);
            }
        }

        private Button Result(StudioComponentTypeEntry entry)
        {
            // Unavailable types stay visible and explain themselves; hiding them would leave the user
            // searching for something that is already on the element.
            var blocked = _blockedReason?.Invoke(entry.Type);

            var button = new Button(() =>
            {
                if (blocked != null) return;
                _onSelect?.Invoke(entry.Type);
                Close();
            })
            {
                text = string.Empty,
                tooltip = blocked ?? Tooltip(entry)
            };
            button.AddToClassList("nexui-component-picker-card");
            if (blocked != null) button.SetEnabled(false);

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("nexui-component-picker-icon");
            var texture = StudioComponentTypeIndex.Icon(entry.Type);
            if (texture != null) icon.style.backgroundImage = new StyleBackground(texture);
            else icon.Add(new Label("C#") { pickingMode = PickingMode.Ignore });
            button.Add(icon);

            var copy = new VisualElement { pickingMode = PickingMode.Ignore };
            copy.AddToClassList("nexui-component-picker-card-copy");
            var title = new Label(entry.DisplayName);
            title.AddToClassList("nexui-component-picker-card-title");
            copy.Add(title);
            var description = new Label(blocked ?? Description(entry));
            description.AddToClassList("nexui-component-picker-card-description");
            copy.Add(description);
            button.Add(copy);

            var source = new Label(SourceLabel(entry.Origin)) { pickingMode = PickingMode.Ignore };
            source.AddToClassList("nexui-component-picker-source");
            if (entry.Origin == StudioComponentOrigin.Project) source.AddToClassList("is-project");
            button.Add(source);

            button.RegisterCallback<PointerEnterEvent>(_ => ShowDetails(entry));
            button.RegisterCallback<FocusInEvent>(_ => ShowDetails(entry));
            return button;
        }

        private void ShowDetails(StudioComponentTypeEntry entry)
        {
            if (_details == null || entry == null) return;
            _details.Clear();

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("nexui-component-picker-detail-icon");
            var texture = StudioComponentTypeIndex.Icon(entry.Type);
            if (texture != null) icon.style.backgroundImage = new StyleBackground(texture);
            else icon.Add(new Label("C#") { pickingMode = PickingMode.Ignore });
            _details.Add(icon);

            var copy = new VisualElement { pickingMode = PickingMode.Ignore };
            copy.AddToClassList("nexui-component-picker-detail-copy");
            var title = new Label(entry.DisplayName);
            title.AddToClassList("nexui-component-picker-detail-title");
            copy.Add(title);
            var identity = new Label(entry.QualifiedName);
            identity.AddToClassList("nexui-component-picker-detail-identity");
            copy.Add(identity);
            var description = new Label(Description(entry));
            description.AddToClassList("nexui-component-picker-detail-description");
            copy.Add(description);

            if (!string.IsNullOrEmpty(entry.Requirements))
            {
                var required = new Label(string.Format(
                    DesignerLocalization.T("attachedComponents.requires"), entry.Requirements));
                required.AddToClassList("nexui-component-picker-detail-requires");
                copy.Add(required);
            }
            _details.Add(copy);
        }

        private static string SourceLabel(StudioComponentOrigin origin) => origin switch
        {
            StudioComponentOrigin.Project => "PROJECT",
            StudioComponentOrigin.NexUI => "NEXUI",
            StudioComponentOrigin.UGUI => "UGUI",
            _ => "UNITY"
        };

        private static string Description(StudioComponentTypeEntry entry)
        {
            var authored = entry.Type
                .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
            if (authored.Length > 0)
            {
                var text = ((System.ComponentModel.DescriptionAttribute)authored[0]).Description;
                if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
            }
            return string.Format(
                entry.Origin == StudioComponentOrigin.Project
                    ? DesignerLocalization.T("attachedComponents.descriptionProject")
                    : DesignerLocalization.T("attachedComponents.descriptionUnity"),
                entry.DisplayName);
        }

        private static string Tooltip(StudioComponentTypeEntry entry)
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine(entry.DisplayName);
            builder.AppendLine(Description(entry));
            builder.AppendLine();
            builder.Append(DesignerLocalization.T("attachedComponents.tooltipCategory")).Append(": ")
                .AppendLine(entry.Category);
            builder.Append(DesignerLocalization.T("attachedComponents.tooltipAssembly")).Append(": ")
                .AppendLine(entry.AssemblyName);
            builder.Append(DesignerLocalization.T("attachedComponents.tooltipBackend")).Append(": ")
                .Append(DesignerLocalization.T("attachedComponents.uguiOnly"));
            if (!string.IsNullOrEmpty(entry.Requirements))
                builder.AppendLine().AppendFormat(
                    DesignerLocalization.T("attachedComponents.requires"), entry.Requirements);
            return builder.ToString();
        }
    }
}

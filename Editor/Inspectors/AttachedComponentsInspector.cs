using System;
using System.Collections.Generic;
using System.Reflection;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Inspectors
{
    /// <summary>
    /// Unity-style Add Component authoring for Designer elements. The metadata stores type names;
    /// UGUIAssetSerializer materializes them on the generated GameObject without taking ownership
    /// of components the user added directly to the prefab.
    /// </summary>
    public sealed class AttachedComponentsInspector : DesignerInspectorBase
    {
        private readonly VisualElement _host;

        public AttachedComponentsInspector(NexUIDesignerContext context)
            : base(context, "inspector.attachedComponents")
        {
            _host = new VisualElement();
            Add(_host);
            Subscriptions.Add<IReadOnlyList<DesignerElementMetadata>>(
                h => context.MultiSelectionChanged += h, h => context.MultiSelectionChanged -= h, _ => Rebuild());
            Subscriptions.Add<DesignerElementMetadata>(
                h => context.ElementChanged += h, h => context.ElementChanged -= h, _ => Rebuild());
            Rebuild();
        }

        private void Rebuild()
        {
            _host.Clear();
            var element = Context.SelectedMetadata;
            if (element == null)
            {
                style.display = DisplayStyle.None;
                return;
            }
            style.display = DisplayStyle.Flex;
            element.attachedComponents ??= new List<DesignerAttachedComponentMetadata>();

            _host.Add(BuildOverview(element));
            var help = new Label(DesignerLocalization.T("attachedComponents.help"))
            {
                tooltip = DesignerLocalization.T("attachedComponents.helpTooltip")
            };
            help.AddToClassList("nexui-attached-help");
            _host.Add(help);

            var list = new VisualElement();
            list.AddToClassList("nexui-attached-list");
            for (var i = 0; i < element.attachedComponents.Count; i++)
            {
                var item = element.attachedComponents[i];
                list.Add(BuildComponentCard(DesignerMonoBehaviourTypes.Resolve(item?.typeName), item?.typeName, i));
            }
            if (element.attachedComponents.Count == 0)
            {
                var empty = new Label(DesignerLocalization.T("attachedComponents.empty"));
                empty.AddToClassList("nexui-attached-empty");
                list.Add(empty);
            }
            _host.Add(list);

            var add = new Button(() => DesignerMonoBehaviourPickerWindow.Open(Add))
            {
                text = "+  " + DesignerLocalization.T("attachedComponents.add"),
                tooltip = DesignerLocalization.T("attachedComponents.addTooltip")
            };
            add.AddToClassList("nexui-attached-add");
            _host.Add(add);
        }

        private static VisualElement BuildOverview(DesignerElementMetadata element)
        {
            var overview = new VisualElement();
            overview.AddToClassList("nexui-attached-overview");

            var icon = new Label("C#") { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("nexui-attached-overview-icon");
            overview.Add(icon);

            var copy = new VisualElement { pickingMode = PickingMode.Ignore };
            copy.AddToClassList("nexui-attached-overview-copy");
            var title = new Label(string.Format(DesignerLocalization.T("attachedComponents.summary"),
                element.attachedComponents?.Count ?? 0));
            title.AddToClassList("nexui-attached-overview-title");
            copy.Add(title);
            var target = new Label(string.Format(DesignerLocalization.T("attachedComponents.target"),
                string.IsNullOrWhiteSpace(element.displayName) ? element.elementId : element.displayName));
            target.AddToClassList("nexui-attached-overview-target");
            copy.Add(target);
            overview.Add(copy);

            var backend = new Label("uGUI") { tooltip = DesignerLocalization.T("attachedComponents.uguiTooltip") };
            backend.AddToClassList("nexui-attached-backend-badge");
            overview.Add(backend);
            return overview;
        }

        private VisualElement BuildComponentCard(Type type, string storedTypeName, int index)
        {
            var missing = type == null;
            var card = new VisualElement
            {
                tooltip = missing
                    ? DesignerLocalization.T("attachedComponents.missing") + "\n" + (storedTypeName ?? string.Empty)
                    : DesignerMonoBehaviourTypes.Tooltip(type)
            };
            card.AddToClassList("nexui-attached-card");
            if (missing) card.AddToClassList("is-missing");

            var iconFrame = new VisualElement { pickingMode = PickingMode.Ignore };
            iconFrame.AddToClassList("nexui-attached-icon-frame");
            var texture = DesignerMonoBehaviourTypes.Icon(type);
            if (texture != null)
                iconFrame.style.backgroundImage = new StyleBackground(texture);
            else
            {
                var fallback = new Label(missing ? "!" : "C#") { pickingMode = PickingMode.Ignore };
                fallback.AddToClassList("nexui-attached-icon-fallback");
                iconFrame.Add(fallback);
            }
            card.Add(iconFrame);

            var copy = new VisualElement();
            copy.AddToClassList("nexui-attached-card-copy");
            var title = new Label(missing
                ? DesignerLocalization.T("attachedComponents.missingTitle")
                : DesignerMonoBehaviourTypes.ShortName(type));
            title.AddToClassList("nexui-attached-card-title");
            copy.Add(title);
            var category = new Label(missing ? storedTypeName ?? string.Empty : DesignerMonoBehaviourTypes.Category(type));
            category.AddToClassList("nexui-attached-card-category");
            copy.Add(category);
            var description = new Label(missing
                ? DesignerLocalization.T("attachedComponents.missing")
                : DesignerMonoBehaviourTypes.Description(type));
            description.AddToClassList("nexui-attached-card-description");
            copy.Add(description);

            if (!missing)
            {
                var meta = new VisualElement { pickingMode = PickingMode.Ignore };
                meta.AddToClassList("nexui-attached-card-meta");
                meta.Add(Badge(DesignerMonoBehaviourTypes.IsUnityType(type)
                    ? DesignerLocalization.T("attachedComponents.sourceUnity")
                    : DesignerLocalization.T("attachedComponents.sourceProject"), "source"));
                if (type.GetCustomAttribute<DisallowMultipleComponent>() != null)
                    meta.Add(Badge(DesignerLocalization.T("attachedComponents.single"), "single"));
                if (type.GetCustomAttribute<ExecuteAlways>() != null || type.GetCustomAttribute<ExecuteInEditMode>() != null)
                    meta.Add(Badge(DesignerLocalization.T("attachedComponents.editMode"), "edit"));
                copy.Add(meta);

                var requirements = DesignerMonoBehaviourTypes.Requirements(type);
                if (!string.IsNullOrEmpty(requirements))
                {
                    var required = new Label(string.Format(DesignerLocalization.T("attachedComponents.requires"), requirements));
                    required.AddToClassList("nexui-attached-card-requires");
                    copy.Add(required);
                }
            }
            card.Add(copy);

            var remove = new Button(() => Remove(index))
            {
                text = "×",
                tooltip = DesignerLocalization.T("attachedComponents.remove")
            };
            remove.AddToClassList("nexui-attached-remove");
            card.Add(remove);

            if (!missing)
            {
                card.RegisterCallback<ContextClickEvent>(evt =>
                {
                    var menu = new GenericMenu();
                    var script = DesignerMonoBehaviourTypes.FindScript(type);
                    if (script != null)
                        menu.AddItem(new GUIContent(DesignerLocalization.T("attachedComponents.showScript")), false,
                            () => EditorGUIUtility.PingObject(script));
                    else
                        menu.AddDisabledItem(new GUIContent(DesignerLocalization.T("attachedComponents.showScript")));
                    var helpUrl = type.GetCustomAttribute<HelpURLAttribute>()?.URL;
                    if (!string.IsNullOrWhiteSpace(helpUrl))
                        menu.AddItem(new GUIContent(DesignerLocalization.T("attachedComponents.openDocs")), false,
                            () => Application.OpenURL(helpUrl));
                    menu.ShowAsContext();
                    evt.StopPropagation();
                });
            }
            return card;
        }

        private static Label Badge(string text, string modifier)
        {
            var badge = new Label(text) { pickingMode = PickingMode.Ignore };
            badge.AddToClassList("nexui-attached-badge");
            badge.AddToClassList("is-" + modifier);
            return badge;
        }

        private void Add(Type type)
        {
            var element = Context.SelectedMetadata;
            if (element == null || type == null) return;
            Context.UpdateElement(element, e =>
            {
                e.attachedComponents ??= new List<DesignerAttachedComponentMetadata>();
                if (type.GetCustomAttribute<DisallowMultipleComponent>() != null)
                {
                    foreach (var existing in e.attachedComponents)
                        if (DesignerMonoBehaviourTypes.Resolve(existing?.typeName) == type) return;
                }
                e.attachedComponents.Add(new DesignerAttachedComponentMetadata
                {
                    typeName = DesignerMonoBehaviourTypes.Identity(type)
                });
            }, "Add Component");
            Rebuild();
        }

        private void Remove(int index)
        {
            var element = Context.SelectedMetadata;
            if (element?.attachedComponents == null || index < 0 || index >= element.attachedComponents.Count) return;
            Context.UpdateElement(element, e =>
            {
                if (e.attachedComponents != null && index < e.attachedComponents.Count)
                    e.attachedComponents.RemoveAt(index);
            }, "Remove Component");
            Rebuild();
        }
    }

    internal static class DesignerMonoBehaviourTypes
    {
        private static List<Type> _all;

        public static IReadOnlyList<Type> All
        {
            get
            {
                if (_all != null) return _all;
                _all = new List<Type>();
                foreach (var type in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
                {
                    if (type == null || type.IsAbstract || type.ContainsGenericParameters) continue;
                    if (!type.IsPublic && !type.IsNestedPublic) continue;
                    if (type == typeof(DesignerAttachedComponentTracker)) continue;
                    if (type.GetCustomAttribute<ObsoleteAttribute>() != null) continue;
                    var assemblyName = type.Assembly.GetName().Name;
                    if (assemblyName.EndsWith(".Editor", StringComparison.OrdinalIgnoreCase) ||
                        assemblyName.EndsWith("-Editor", StringComparison.OrdinalIgnoreCase) ||
                        (type.Namespace?.StartsWith("UnityEditor", StringComparison.Ordinal) ?? false)) continue;
                    _all.Add(type);
                }
                _all.Sort((a, b) => string.Compare(DisplayName(a), DisplayName(b), StringComparison.OrdinalIgnoreCase));
                return _all;
            }
        }

        public static string Identity(Type type)
            => type == null ? string.Empty : type.FullName + ", " + type.Assembly.GetName().Name;

        public static Type Resolve(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;
            var type = Type.GetType(typeName, false);
            if (type != null) return type;
            var comma = typeName.IndexOf(',');
            var fullName = comma >= 0 ? typeName.Substring(0, comma).Trim() : typeName.Trim();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                if ((type = assembly.GetType(fullName, false)) != null) return type;
            return null;
        }

        public static string DisplayName(Type type)
        {
            var menu = type.GetCustomAttribute<AddComponentMenu>();
            return menu != null && !string.IsNullOrEmpty(menu.componentMenu)
                ? menu.componentMenu
                : (string.IsNullOrEmpty(type.Namespace) ? type.Name : type.Namespace + "/" + type.Name);
        }

        public static string ShortName(Type type)
        {
            if (type == null) return string.Empty;
            var display = DisplayName(type);
            var slash = display.LastIndexOf('/');
            var name = slash >= 0 ? display.Substring(slash + 1) : type.Name;
            return ObjectNames.NicifyVariableName(name);
        }

        public static string Category(Type type)
        {
            if (type == null) return DesignerLocalization.T("attachedComponents.categoryUnknown");
            var display = DisplayName(type);
            var slash = display.LastIndexOf('/');
            if (slash > 0) return display.Substring(0, slash);
            if (!string.IsNullOrWhiteSpace(type.Namespace)) return type.Namespace;
            return type.Assembly.GetName().Name;
        }

        public static string Description(Type type)
        {
            if (type == null) return DesignerLocalization.T("attachedComponents.missing");
            var authored = type.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description;
            if (!string.IsNullOrWhiteSpace(authored)) return authored.Trim();
            return string.Format(IsUnityType(type)
                    ? DesignerLocalization.T("attachedComponents.descriptionUnity")
                    : DesignerLocalization.T("attachedComponents.descriptionProject"),
                ShortName(type));
        }

        public static bool IsUnityType(Type type)
        {
            if (type == null) return false;
            var assembly = type.Assembly.GetName().Name;
            return assembly.StartsWith("Unity", StringComparison.OrdinalIgnoreCase) ||
                   (type.Namespace?.StartsWith("UnityEngine", StringComparison.Ordinal) ?? false);
        }

        public static Texture2D Icon(Type type)
            => type == null ? null : EditorGUIUtility.ObjectContent(null, type)?.image as Texture2D;

        public static string Requirements(Type type)
        {
            if (type == null) return string.Empty;
            var names = new List<string>();
            foreach (var attribute in type.GetCustomAttributes<RequireComponent>())
            {
                AddRequirement(attribute.m_Type0, names);
                AddRequirement(attribute.m_Type1, names);
                AddRequirement(attribute.m_Type2, names);
            }
            return string.Join(", ", names);
        }

        private static void AddRequirement(Type requirement, List<string> names)
        {
            if (requirement == null) return;
            var name = ObjectNames.NicifyVariableName(requirement.Name);
            if (!names.Contains(name)) names.Add(name);
        }

        public static string Tooltip(Type type)
        {
            if (type == null) return DesignerLocalization.T("attachedComponents.missing");
            var builder = new System.Text.StringBuilder();
            builder.AppendLine(ShortName(type));
            builder.AppendLine(Description(type));
            builder.AppendLine();
            builder.Append(DesignerLocalization.T("attachedComponents.tooltipCategory")).Append(": ").AppendLine(Category(type));
            builder.Append(DesignerLocalization.T("attachedComponents.tooltipAssembly")).Append(": ")
                .AppendLine(type.Assembly.GetName().Name);
            builder.Append(DesignerLocalization.T("attachedComponents.tooltipBackend")).Append(": ")
                .Append(DesignerLocalization.T("attachedComponents.uguiOnly"));
            var requirements = Requirements(type);
            if (!string.IsNullOrEmpty(requirements))
                builder.AppendLine().AppendFormat(DesignerLocalization.T("attachedComponents.requires"), requirements);
            return builder.ToString();
        }

        public static MonoScript FindScript(Type type)
        {
            if (type == null) return null;
            foreach (var guid in AssetDatabase.FindAssets(type.Name + " t:MonoScript"))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script != null && script.GetClass() == type) return script;
            }
            return null;
        }
    }

    internal sealed class DesignerMonoBehaviourPickerWindow : EditorWindow
    {
        private Action<Type> _onSelect;
        private ToolbarSearchField _search;
        private ScrollView _results;
        private VisualElement _details;
        private Label _summary;

        public static void Open(Action<Type> onSelect)
        {
            var window = CreateInstance<DesignerMonoBehaviourPickerWindow>();
            window._onSelect = onSelect;
            window.titleContent = new GUIContent(DesignerLocalization.T("attachedComponents.pickerTitle"));
            window.minSize = new Vector2(420, 360);
            window.maxSize = new Vector2(640, 720);
            window.ShowAuxWindow();
            window.Focus();
        }

        public void CreateGUI()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.emiteat.nexui.designer/Editor/Styles/NexUIDesigner.uss");
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

            _search = new ToolbarSearchField
            {
                tooltip = DesignerLocalization.T("attachedComponents.searchTooltip")
            };
            _search.AddToClassList("nexui-component-picker-search");
            _search.RegisterValueChangedCallback(_ => Rebuild());
            rootVisualElement.Add(_search);

            _details = new VisualElement();
            _details.AddToClassList("nexui-component-picker-details");
            rootVisualElement.Add(_details);

            _summary = new Label();
            _summary.AddToClassList("nexui-component-picker-summary");
            rootVisualElement.Add(_summary);

            _results = new ScrollView();
            _results.style.flexGrow = 1;
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
            var groups = new SortedDictionary<string, List<Type>>(StringComparer.OrdinalIgnoreCase);
            foreach (var type in DesignerMonoBehaviourTypes.All)
            {
                var display = DesignerMonoBehaviourTypes.DisplayName(type);
                var identity = DesignerMonoBehaviourTypes.Identity(type);
                if (query.Length > 0 && display.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                    identity.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                    DesignerMonoBehaviourTypes.Description(type).IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                    DesignerMonoBehaviourTypes.Category(type).IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var category = DesignerMonoBehaviourTypes.Category(type);
                if (!groups.TryGetValue(category, out var items))
                {
                    items = new List<Type>();
                    groups.Add(category, items);
                }
                items.Add(type);
            }

            var count = 0;
            Type first = null;
            foreach (var pair in groups)
            {
                var foldout = new Foldout { text = pair.Key, value = query.Length > 0 || groups.Count <= 6 };
                foldout.AddToClassList("nexui-component-picker-group");
                foreach (var type in pair.Value)
                {
                    first ??= type;
                    count++;
                    foldout.Add(BuildResult(type));
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

        private Button BuildResult(Type type)
        {
            var captured = type;
            var button = new Button(() =>
            {
                _onSelect?.Invoke(captured);
                Close();
            })
            {
                text = string.Empty,
                tooltip = DesignerMonoBehaviourTypes.Tooltip(type)
            };
            button.AddToClassList("nexui-component-picker-card");

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("nexui-component-picker-icon");
            var texture = DesignerMonoBehaviourTypes.Icon(type);
            if (texture != null) icon.style.backgroundImage = new StyleBackground(texture);
            else icon.Add(new Label("C#") { pickingMode = PickingMode.Ignore });
            button.Add(icon);

            var copy = new VisualElement { pickingMode = PickingMode.Ignore };
            copy.AddToClassList("nexui-component-picker-card-copy");
            var title = new Label(DesignerMonoBehaviourTypes.ShortName(type));
            title.AddToClassList("nexui-component-picker-card-title");
            copy.Add(title);
            var description = new Label(DesignerMonoBehaviourTypes.Description(type));
            description.AddToClassList("nexui-component-picker-card-description");
            copy.Add(description);
            button.Add(copy);

            var source = new Label(DesignerMonoBehaviourTypes.IsUnityType(type) ? "UNITY" : "PROJECT")
            {
                pickingMode = PickingMode.Ignore
            };
            source.AddToClassList("nexui-component-picker-source");
            if (!DesignerMonoBehaviourTypes.IsUnityType(type)) source.AddToClassList("is-project");
            button.Add(source);

            button.RegisterCallback<PointerEnterEvent>(_ => ShowDetails(captured));
            button.RegisterCallback<FocusInEvent>(_ => ShowDetails(captured));
            return button;
        }

        private void ShowDetails(Type type)
        {
            if (_details == null || type == null) return;
            _details.Clear();

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("nexui-component-picker-detail-icon");
            var texture = DesignerMonoBehaviourTypes.Icon(type);
            if (texture != null) icon.style.backgroundImage = new StyleBackground(texture);
            else icon.Add(new Label("C#") { pickingMode = PickingMode.Ignore });
            _details.Add(icon);

            var copy = new VisualElement { pickingMode = PickingMode.Ignore };
            copy.AddToClassList("nexui-component-picker-detail-copy");
            var title = new Label(DesignerMonoBehaviourTypes.ShortName(type));
            title.AddToClassList("nexui-component-picker-detail-title");
            copy.Add(title);
            var identity = new Label(DesignerMonoBehaviourTypes.Identity(type));
            identity.AddToClassList("nexui-component-picker-detail-identity");
            copy.Add(identity);
            var description = new Label(DesignerMonoBehaviourTypes.Description(type));
            description.AddToClassList("nexui-component-picker-detail-description");
            copy.Add(description);

            var requirements = DesignerMonoBehaviourTypes.Requirements(type);
            if (!string.IsNullOrEmpty(requirements))
            {
                var required = new Label(string.Format(DesignerLocalization.T("attachedComponents.requires"), requirements));
                required.AddToClassList("nexui-component-picker-detail-requires");
                copy.Add(required);
            }
            _details.Add(copy);
        }
    }
}

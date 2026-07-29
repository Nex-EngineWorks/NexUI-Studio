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

            var help = new HelpBox(DesignerLocalization.T("attachedComponents.help"), HelpBoxMessageType.Info);
            _host.Add(help);

            element.attachedComponents ??= new List<DesignerAttachedComponentMetadata>();
            for (var i = 0; i < element.attachedComponents.Count; i++)
            {
                var index = i;
                var item = element.attachedComponents[i];
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                var type = DesignerMonoBehaviourTypes.Resolve(item?.typeName);
                var label = new Label(type != null ? type.FullName : item?.typeName ?? "Missing type")
                {
                    tooltip = type != null ? DesignerMonoBehaviourTypes.Identity(type) : DesignerLocalization.T("attachedComponents.missing")
                };
                label.style.flexGrow = 1;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                row.Add(label);
                var remove = new Button(() => Remove(index)) { text = "−", tooltip = DesignerLocalization.T("attachedComponents.remove") };
                remove.style.width = 24;
                row.Add(remove);
                _host.Add(row);
            }

            var add = new Button(() => DesignerMonoBehaviourPickerWindow.Open(Add))
            {
                text = DesignerLocalization.T("attachedComponents.add"),
                tooltip = DesignerLocalization.T("attachedComponents.addTooltip")
            };
            _host.Add(add);
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
    }

    internal sealed class DesignerMonoBehaviourPickerWindow : EditorWindow
    {
        private Action<Type> _onSelect;
        private ToolbarSearchField _search;
        private ScrollView _results;

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
            _search = new ToolbarSearchField();
            _search.RegisterValueChangedCallback(_ => Rebuild());
            rootVisualElement.Add(_search);
            _results = new ScrollView();
            _results.style.flexGrow = 1;
            rootVisualElement.Add(_results);
            Rebuild();
            _search.Focus();
        }

        private void Rebuild()
        {
            if (_results == null) return;
            _results.Clear();
            var query = _search?.value?.Trim() ?? string.Empty;
            foreach (var type in DesignerMonoBehaviourTypes.All)
            {
                var display = DesignerMonoBehaviourTypes.DisplayName(type);
                var identity = DesignerMonoBehaviourTypes.Identity(type);
                if (query.Length > 0 && display.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                    identity.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var captured = type;
                var button = new Button(() =>
                {
                    _onSelect?.Invoke(captured);
                    Close();
                }) { text = display, tooltip = identity };
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                _results.Add(button);
            }
        }
    }
}

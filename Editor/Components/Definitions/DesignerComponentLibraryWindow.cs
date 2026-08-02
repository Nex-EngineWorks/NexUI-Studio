using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Common;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Components.Definitions
{
    /// <summary>
    /// Browse and apply reusable component definitions: search, filter by category, mark favourites,
    /// see where each one is used, create one from the current Designer selection and place instances.
    ///
    /// Everything here routes through <see cref="DesignerComponentService"/>, so the window holds no
    /// authoring logic of its own and destructive operations (Swap, Detach) go through the same
    /// confirmation path regardless of where they are triggered from.
    /// </summary>
    public sealed class DesignerComponentLibraryWindow : NexUIToolWindow
    {
        [SerializeField] private string _search = string.Empty;
        [SerializeField] private string _category = string.Empty;
        [SerializeField] private bool _favouritesOnly;
        [SerializeField] private string _newComponentFolder = "Assets/UI/Components";

        private Vector2 _listScroll;
        private DesignerComponentDefinitionAsset _selected;
        private List<DesignerComponentLibrary.DesignerComponentUsage> _usages;
        private DesignerComponentDefinitionAsset _usagesFor;
        private string _status;
        private MessageType _statusKind = MessageType.Info;

        protected override string TitleKey => "panel.componentLibrary";
        protected override string TooltipKey => "tooltip.componentLibrary";

        [MenuItem("Tools/Nex/NexUI Studio/Component Library", priority = NexUIDesignerMenu.PriorityWindows + 4)]
        public static void Open() => GetWindow<DesignerComponentLibraryWindow>();

        protected override void OnEnable()
        {
            base.OnEnable();
            DesignerComponentLibrary.Changed += OnLibraryChanged;
        }

        protected override void OnDisable()
        {
            DesignerComponentLibrary.Changed -= OnLibraryChanged;
            base.OnDisable();
        }

        private void OnLibraryChanged()
        {
            _usages = null;
            _usagesFor = null;
            Repaint();
        }

        protected override void DrawBody()
        {
            var context = FindOpenContext();

            DrawFilters();
            DrawList();
            EditorGUILayout.Space(6);
            DrawSelected(context);
            EditorGUILayout.Space(6);
            DrawCreateFromSelection(context);

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(_status, _statusKind);
            }
        }

        /// <summary>The Designer window's context when one is open. Never opens the Designer just to browse.</summary>
        private static NexUIDesignerContext FindOpenContext()
        {
            var windows = Resources.FindObjectsOfTypeAll<NexUIDesignerWindow>();
            return windows.Length > 0 ? windows[0].Context : null;
        }

        private void DrawFilters()
        {
            Section("panel.componentLibrary");
            _search = EditorGUILayout.TextField("Search", _search);

            var categories = new List<string> { "(all)" };
            categories.AddRange(DesignerComponentLibrary.Categories());
            var currentIndex = string.IsNullOrEmpty(_category) ? 0 : Mathf.Max(0, categories.IndexOf(_category));
            var newIndex = EditorGUILayout.Popup("Category", currentIndex, categories.ToArray());
            _category = newIndex <= 0 ? string.Empty : categories[newIndex];

            _favouritesOnly = EditorGUILayout.Toggle("Favourites only", _favouritesOnly);
        }

        private void DrawList()
        {
            var results = DesignerComponentLibrary.Search(_search, _category, _favouritesOnly);
            if (results.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    DesignerComponentLibrary.All.Count == 0
                        ? "No component definitions exist yet. Select an element in the Designer and use 'Create From Selection' below."
                        : "No component matches this filter.",
                    MessageType.Info);
                return;
            }

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(220f));
            foreach (var definition in results)
            {
                EditorGUILayout.BeginHorizontal();

                var favourite = DesignerComponentLibrary.IsFavourite(definition);
                var newFavourite = GUILayout.Toggle(favourite, favourite ? "★" : "☆", EditorStyles.label, GUILayout.Width(18f));
                if (newFavourite != favourite) DesignerComponentLibrary.SetFavourite(definition, newFavourite);

                var isSelected = _selected == definition;
                if (GUILayout.Toggle(isSelected, $"{definition.EffectiveDisplayName}  v{definition.version}",
                        EditorStyles.miniButton) && !isSelected)
                {
                    _selected = definition;
                    _usages = null;
                }

                GUILayout.Label(string.IsNullOrEmpty(definition.category) ? "Custom" : definition.category,
                    EditorStyles.miniLabel, GUILayout.Width(90f));

                if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(40f)))
                    EditorGUIUtility.PingObject(definition);

                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawSelected(NexUIDesignerContext context)
        {
            if (_selected == null)
            {
                EditorGUILayout.HelpBox("Select a component to see its contract and place an instance.", MessageType.None);
                return;
            }

            Section("panel.inspector");
            EditorGUILayout.LabelField("Component", _selected.EffectiveDisplayName);
            if (!string.IsNullOrEmpty(_selected.description))
                EditorGUILayout.LabelField(_selected.description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Elements", _selected.elements.Count.ToString());
            EditorGUILayout.LabelField("Slots", DescribeSlots(_selected));
            EditorGUILayout.LabelField("Exposed", _selected.exposedProperties.Count.ToString());
            EditorGUILayout.LabelField("Variants", DescribeVariants(_selected));

            if (_selected.Root == null)
                EditorGUILayout.HelpBox("This definition has no root element; instances of it cannot expand.", MessageType.Error);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(context?.Metadata == null))
            {
                if (GUILayout.Button("Place Instance"))
                    PlaceInstance(context);
            }
            if (GUILayout.Button("Find Usages"))
            {
                _usages = DesignerComponentLibrary.FindUsages(_selected);
                _usagesFor = _selected;
            }
            if (GUILayout.Button("Edit Definition"))
                DesignerComponentDefinitionEditorWindow.Open(_selected);
            EditorGUILayout.EndHorizontal();

            if (context?.Metadata == null)
                EditorGUILayout.HelpBox("Open a screen in the Designer to place instances.", MessageType.Info);

            if (_usages != null && _usagesFor == _selected)
            {
                EditorGUILayout.LabelField($"Used by {_usages.Count} element(s)", EditorStyles.miniBoldLabel);
                foreach (var usage in _usages)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label($"{usage.Screen.name} → {usage.ElementId}{(usage.Detached ? " (detached)" : "")}",
                        EditorStyles.miniLabel);
                    if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(40f)))
                        EditorGUIUtility.PingObject(usage.Screen);
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private void PlaceInstance(NexUIDesignerContext context)
        {
            var result = DesignerComponentService.Instantiate(context.Metadata, _selected, new Vector2(64f, 64f));
            SetStatus(result);
            if (result.Success)
            {
                context.InvalidateComponentExpansion();
                context.Validate();
                context.Select(result.Element);
            }
        }

        private void DrawCreateFromSelection(NexUIDesignerContext context)
        {
            Section("panel.hierarchy");
            _newComponentFolder = EditorGUILayout.TextField("Folder", _newComponentFolder);

            var selected = context?.SelectedMetadata;
            var canCreate = context?.Metadata != null && selected != null &&
                            (selected.componentInstance == null || !selected.componentInstance.IsInstance);

            using (new EditorGUI.DisabledScope(!canCreate))
            {
                if (GUILayout.Button(selected != null
                        ? $"Create Component From '{selected.elementId}'"
                        : "Create Component From Selection", GUILayout.Height(24f)))
                    CreateFromSelection(context, selected);
            }

            if (context?.Metadata == null)
                EditorGUILayout.HelpBox("Open a screen in the Designer first.", MessageType.Info);
            else if (selected == null)
                EditorGUILayout.HelpBox("Select the element that should become the component root.", MessageType.Info);
            else if (!canCreate)
                EditorGUILayout.HelpBox("The selection is already a component instance. Detach it first to fork it.", MessageType.Warning);
        }

        private void CreateFromSelection(NexUIDesignerContext context, DesignerElementMetadata selected)
        {
            var folder = string.IsNullOrWhiteSpace(_newComponentFolder) ? "Assets" : _newComponentFolder.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(folder))
            {
                SetStatus($"Folder '{folder}' does not exist. Create it first so nothing is written to an unexpected place.", MessageType.Error);
                return;
            }

            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{selected.elementId}.asset");
            var result = DesignerComponentService.CreateDefinitionFromSubtree(context.Metadata, selected.elementId, path);
            SetStatus(result);
            if (!result.Success) return;

            _selected = result.Definition;
            _usages = null;
            context.InvalidateComponentExpansion();
            context.Validate();
            EditorGUIUtility.PingObject(result.Definition);
        }

        private static string DescribeSlots(DesignerComponentDefinitionAsset definition)
        {
            if (definition.slots.Count == 0) return "(none)";
            var names = new List<string>(definition.slots.Count);
            foreach (var slot in definition.slots)
                if (slot != null) names.Add(slot.required ? slot.slotId + "*" : slot.slotId);
            return string.Join(", ", names);
        }

        private static string DescribeVariants(DesignerComponentDefinitionAsset definition)
        {
            if (definition.variantProperties.Count == 0) return "(none)";
            var names = new List<string>(definition.variantProperties.Count);
            foreach (var property in definition.variantProperties)
                if (property != null) names.Add($"{property.propertyName}={property.EffectiveDefault}");
            return string.Join(", ", names);
        }

        private void SetStatus(DesignerComponentOperationResult result)
        {
            _status = result.Message;
            foreach (var warning in result.Warnings) _status += "\n• " + warning;
            _statusKind = !result.Success ? MessageType.Error
                : result.Warnings.Count > 0 ? MessageType.Warning
                : MessageType.Info;
        }

        private void SetStatus(string message, MessageType kind)
        {
            _status = message;
            _statusKind = kind;
        }
    }

    /// <summary>Focused authoring surface for a reusable component's element tree and contract.</summary>
    public sealed class DesignerComponentDefinitionEditorWindow : EditorWindow
    {
        [SerializeField] private DesignerComponentDefinitionAsset _definition;
        [SerializeField] private Vector2 _scroll;
        private SerializedObject _serialized;

        public static void Open(DesignerComponentDefinitionAsset definition)
        {
            var window = GetWindow<DesignerComponentDefinitionEditorWindow>();
            window.titleContent = new GUIContent("Component Definition");
            window.minSize = new Vector2(430f, 420f);
            window.SetDefinition(definition);
            window.Show();
        }

        [MenuItem("CONTEXT/DesignerComponentDefinitionAsset/Edit in NexUI")]
        private static void OpenFromContext(MenuCommand command)
            => Open(command.context as DesignerComponentDefinitionAsset);

        private void OnEnable()
        {
            titleContent = new GUIContent("Component Definition");
            if (_definition != null) _serialized = new SerializedObject(_definition);
        }

        private void SetDefinition(DesignerComponentDefinitionAsset definition)
        {
            _definition = definition;
            _serialized = definition == null ? null : new SerializedObject(definition);
            Repaint();
        }

        private void OnGUI()
        {
            var picked = (DesignerComponentDefinitionAsset)EditorGUILayout.ObjectField(
                "Definition", _definition, typeof(DesignerComponentDefinitionAsset), false);
            if (picked != _definition) SetDefinition(picked);
            if (_definition == null || _serialized == null)
            {
                EditorGUILayout.HelpBox("Choose a Component Definition asset.", MessageType.Info);
                return;
            }

            _serialized.Update();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            Section("Identity", "displayName", "category", "description", "tags", "thumbnail", "defaultSize", "version");
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.TextField("Component Id", _definition.componentId);
            Section("Element Tree", "rootElementId", "elements");
            if (_definition.Root == null)
                EditorGUILayout.HelpBox("A definition needs at least one root element.", MessageType.Error);
            Section("Instance Contract", "exposedProperties", "slots", "variantProperties", "variantRules");
            EditorGUILayout.EndScrollView();

            if (_serialized.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_definition);
                DesignerComponentLibrary.Invalidate();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Bump Version"))
            {
                Undo.RecordObject(_definition, "Bump NexUI Component Version");
                _definition.version = Mathf.Max(1, _definition.version + 1);
                EditorUtility.SetDirty(_definition);
            }
            if (GUILayout.Button("Ping Asset")) EditorGUIUtility.PingObject(_definition);
            if (GUILayout.Button("Open Library")) DesignerComponentLibraryWindow.Open();
            EditorGUILayout.EndHorizontal();
        }

        private void Section(string title, params string[] propertyNames)
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach (var propertyName in propertyNames)
            {
                var property = _serialized.FindProperty(propertyName);
                if (property != null) EditorGUILayout.PropertyField(property, true);
            }
        }
    }
}

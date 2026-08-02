using System;
using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Components.Definitions;
using emiteat.NexUI.Designer.Editor.Responsive;
using emiteat.NexUI.Designer.Editor.Properties;
using emiteat.NexUI.Designer.Editor.Serialization;
using emiteat.NexUI.Designer.Editor.Validation;
using emiteat.NexUI.Designer.Editor.Variants;
using emiteat.NexUI.Motion;
using emiteat.NexUI.State;
using emiteat.NexUI.Theme;
using UnityEditor;
using UnityEngine;
using Unity.Profiling;

namespace emiteat.NexUI.Designer.Editor
{
    public sealed partial class NexUIDesignerContext : IDisposable
    {
        private readonly List<string> _validationMessages = new List<string>();
        private readonly List<DesignerValidationIssue> _validationIssues = new List<DesignerValidationIssue>();
        private readonly List<DesignerElementMetadata> _selection = new List<DesignerElementMetadata>();
        private readonly List<DesignerElementMetadata> _clipboard = new List<DesignerElementMetadata>();
        private readonly List<string> _recentActions = new List<string>();
        private const string PrefPrefix = "NexUI.Designer.UI.";
        private const string LastScreenGuidKey = PrefPrefix + "LastScreenGuid";
        private const string LastMetadataGuidKey = PrefPrefix + "LastMetadataGuid";
        private int _elementCounter = 1;
        private int _groupCounter = 1;
        private const int MaxRecentActions = 50;
        private static readonly ProfilerMarker CanvasRebuildMarker = new ProfilerMarker("NexUI.Designer.Canvas.Rebuild");
        private static readonly ProfilerMarker ValidationMarker = new ProfilerMarker("NexUI.Designer.Validation");
        private static readonly ProfilerMarker PublishMarker = new ProfilerMarker("NexUI.Designer.Publish");
        private bool _disposed;
        private bool _hasUnsavedChanges;
        private string _screenBaselineJson;
        private string _metadataBaselineJson;
        private UIScreenDefinition _screenBaselineTarget;
        private DesignerMetadataAsset _metadataBaselineTarget;
        private int _screenExpectedDirtyCount;
        private int _metadataExpectedDirtyCount;
        private bool _screenWasDirtyAtBaseline;
        private bool _metadataWasDirtyAtBaseline;
        private bool? _lastReportedDirtyState;

        public bool IsDisposed => _disposed;
        public bool HasUnsavedChanges => _hasUnsavedChanges || HasExternalAssetChanges();
        public event Action<bool> DirtyStateChanged;

        /// <summary>
        /// C2: read-only log of the last <see cref="MaxRecentActions"/> undo-recorded edit
        /// names, newest first - session-only (cleared on domain reload), not a jump-to-any-
        /// point history. Unity's public Undo API does not expose an enumerable/random-access
        /// undo stack, so this is visibility into what changed rather than a true steppable
        /// history browser.
        /// </summary>
        public IReadOnlyList<string> RecentActions => _recentActions;

        public event Action RecentActionsChanged;

        private void LogAction(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            _recentActions.Insert(0, name);
            if (_recentActions.Count > MaxRecentActions)
                _recentActions.RemoveAt(_recentActions.Count - 1);
            RecentActionsChanged?.Invoke();
        }

        public UIScreenDefinition CurrentScreen { get; private set; }
        public DesignerMetadataAsset Metadata { get; private set; }
        public IUISurface PreviewSurface { get; private set; }
        public IUIElementHandle SelectedElement { get; private set; }

        /// <summary>All currently selected elements. Empty when nothing is selected.</summary>
        public IReadOnlyList<DesignerElementMetadata> SelectedElements => _selection;
        public DesignerElementMetadata KeyObject { get; private set; }

        /// <summary>
        /// The "primary" selected element - the last one added to the selection. Kept for every
        /// pre-existing single-select caller (inspectors, rect/anchor edits); resolves to the
        /// same element a single click would have selected before multi-select existed.
        /// </summary>
        public DesignerElementMetadata SelectedMetadata => _selection.Count > 0 ? _selection[_selection.Count - 1] : null;

        public bool HasClipboard => _clipboard.Count > 0;
        public UIRenderBackend Backend { get; private set; }
        public UIStateStore PreviewStateStore { get; private set; }
        public IUIMotionPlayer PreviewMotionPlayer { get; private set; }
        public ThemeRegistry PreviewThemeRegistry { get; private set; }
        public INexUIDesignerBackend CurrentBackend { get; private set; }
        public Vector2Int Resolution { get; private set; }
        public float Zoom { get; private set; }
        public bool SnapEnabled { get; private set; }
        public float GridSize { get; private set; }
        public string PreviewState { get; private set; }
        public string InputMode { get; private set; }
        public IReadOnlyList<string> ValidationMessages => _validationMessages;
        public IReadOnlyList<DesignerValidationIssue> ValidationIssues => _validationIssues;
        public DesignerSaveReport LastSaveReport { get; private set; }
        public DesignerSaveReport LastSavePreviewReport { get; private set; }
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public DesignerTool CurrentTool { get; private set; }
        public DesignerSidebarTab SidebarTab { get; private set; }
        public DesignerInspectorTab InspectorTab { get; private set; }
        public DesignerBottomTab BottomTab { get; private set; }
        public bool BottomDrawerOpen { get; private set; }
        public float BottomDrawerHeight { get; private set; }

        public event Action<UIScreenDefinition> ScreenChanged;
        public event Action<DesignerSaveReport> SaveCompleted;
        public event Action<DesignerSaveReport> SavePreviewCompleted;
        public event Action<DesignerMetadataAsset> MetadataChanged;
        public event Action<IUIElementHandle> SelectionChanged;
        public event Action<DesignerElementMetadata> MetadataSelectionChanged;
        public event Action<IReadOnlyList<DesignerElementMetadata>> MultiSelectionChanged;
        public event Action PreviewRebuilt;
        public event Action ValidationChanged;
        public event Action CanvasChanged;
        public event Action UIStateChanged;

        /// <summary>
        /// C1 (visible style-apply feedback): raised whenever a single element's fields change
        /// through <see cref="UpdateElement"/>/<see cref="UpdateSelectedElement"/> (style, theme,
        /// binding, accessibility, policy, motion - every per-element Inspector routes through
        /// here). The Viewport uses this to briefly flash the affected element so "did that
        /// apply?" is never in doubt.
        /// </summary>
        public event Action<DesignerElementMetadata> ElementChanged;

        public NexUIDesignerContext()
        {
            PreviewStateStore = new UIStateStore();
            PreviewMotionPlayer = new BuiltInMotionPlayer();
            PreviewThemeRegistry = new ThemeRegistry();
            Resolution = new Vector2Int(1920, 1080);
            Zoom = EditorPrefs.GetFloat(PrefPrefix + "Zoom", 0.5f);
            SnapEnabled = EditorPrefs.GetBool(PrefPrefix + "Snap", true);
            GridSize = EditorPrefs.GetFloat(PrefPrefix + "GridSize", 8f);
            PreviewState = "Normal";
            InputMode = "Keyboard";
            CurrentTool = (DesignerTool)EditorPrefs.GetInt(PrefPrefix + "Tool", (int)DesignerTool.Select);
            SidebarTab = (DesignerSidebarTab)EditorPrefs.GetInt(PrefPrefix + "SidebarTab", (int)DesignerSidebarTab.Layers);
            InspectorTab = (DesignerInspectorTab)EditorPrefs.GetInt(PrefPrefix + "InspectorTab", (int)DesignerInspectorTab.Design);
            BottomTab = (DesignerBottomTab)EditorPrefs.GetInt(PrefPrefix + "BottomTab", (int)DesignerBottomTab.Validation);
            BottomDrawerOpen = EditorPrefs.GetBool(PrefPrefix + "BottomOpen", false);
            BottomDrawerHeight = EditorPrefs.GetFloat(PrefPrefix + "BottomHeight", 220f);
            DesignerBackendRegistry.RegisterDefaults();
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            EditorApplication.projectChanged += OnProjectAssetsChanged;
        }

        public void Open(UIScreenDefinition definition) => TryOpen(definition);

        public bool TryOpen(UIScreenDefinition definition)
        {
            if (definition != CurrentScreen && HasUnsavedChanges && !Application.isBatchMode)
            {
                var choice = EditorUtility.DisplayDialogComplex("NexUI Studio",
                    "There are unsaved Designer changes. Save before switching screens?", "Save", "Cancel", "Discard");
                if (choice == 1) return false;
                if (choice == 0 && Save().HasErrors) return false;
                if (choice == 2 && !DiscardUnsavedChanges()) return false;
            }
            if (definition == CurrentScreen) return true;

            CurrentScreen = definition;
            StoreAssetGuid(LastScreenGuidKey, definition);
            Backend = definition != null ? definition.backendAsset.backend : UIRenderBackend.UIToolkit;
            CaptureScreenBaseline();
            SetMetadataInternal(ResolveMetadataForScreen(definition));
            ScreenChanged?.Invoke(definition);
            RebuildPreview();
            return true;
        }

        public void SetMetadata(DesignerMetadataAsset metadata) => TrySetMetadata(metadata);

        public bool TrySetMetadata(DesignerMetadataAsset metadata)
        {
            if (metadata == Metadata) return true;
            if (HasUnsavedChanges && !Application.isBatchMode)
            {
                var choice = EditorUtility.DisplayDialogComplex("NexUI Studio",
                    "There are unsaved Designer changes. Save before switching metadata?", "Save", "Cancel", "Discard");
                if (choice == 1) return false;
                if (choice == 0 && Save().HasErrors) return false;
                if (choice == 2 && !DiscardUnsavedChanges()) return false;
            }

            SetMetadataInternal(metadata);
            return true;
        }

        private void SetMetadataInternal(DesignerMetadataAsset metadata)
        {
            Metadata = metadata;
            _expansionValid = false;
            StoreAssetGuid(LastMetadataGuidKey, metadata);
            CaptureMetadataBaseline();
            var metadataChangedOnOpen = false;
            if (Metadata != null && CurrentScreen != null && string.IsNullOrEmpty(Metadata.screenId))
            {
                Metadata.screenId = CurrentScreen.ScreenId;
                metadataChangedOnOpen = true;
            }
            // Bring pre-hierarchy assets up to the current schema (assigns sibling indices from the
            // existing draw order - visually invisible) and repair any dangling/cyclic parentIds.
            if (Metadata != null)
            {
                if (Metadata.screenMotion == null)
                    Metadata.screenMotion = new DesignerScreenMotionMetadata();
                metadataChangedOnOpen |= DesignerHierarchyMigration.Migrate(Metadata);
            }
            if (metadataChangedOnOpen) SetDirtyState(true);
            ClearSelection();
            MetadataChanged?.Invoke(metadata);
            CanvasChanged?.Invoke();
            Validate();
            RestoreSelection();
        }

        public void RestoreLastSession()
        {
            if (_disposed || CurrentScreen != null || Application.isBatchMode) return;
            var screen = LoadAssetGuid<UIScreenDefinition>(LastScreenGuidKey);
            var metadata = LoadAssetGuid<DesignerMetadataAsset>(LastMetadataGuidKey);
            if (screen != null) Open(screen);
            if (metadata != null && (screen == null || metadata.screenId == screen.ScreenId))
                SetMetadataInternal(metadata);
        }

        public DesignerMetadataAsset CreateMetadataAsset()
        {
            var asset = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            asset.screenId = CurrentScreen != null ? CurrentScreen.ScreenId : string.Empty;

            var folder = "Assets";
            if (CurrentScreen != null)
            {
                var screenPath = AssetDatabase.GetAssetPath(CurrentScreen);
                if (!string.IsNullOrEmpty(screenPath))
                    folder = System.IO.Path.GetDirectoryName(screenPath).Replace("\\", "/");
            }

            var baseName = !string.IsNullOrEmpty(asset.screenId) ? asset.screenId : "NexUIDesigner";
            var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + baseName + ".Metadata.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            SetMetadata(asset);
            EditorGUIUtility.PingObject(asset);
            return asset;
        }

        public void Select(IUIElementHandle handle)
        {
            SelectedElement = handle;
            SelectionChanged?.Invoke(handle);
        }

        /// <summary>Replaces the whole selection with a single element (or clears it if null).</summary>
        public void SelectMetadata(DesignerElementMetadata element)
        {
            _selection.Clear();
            if (element != null)
                _selection.Add(element);
            _selectedComponentPartId = null;
            ComponentPartSelectionChanged?.Invoke(null);
            KeyObject = null;
            RaiseSelectionChanged();
        }

        public void SelectMetadata(string elementId)
            => SelectMetadata(Metadata != null ? Metadata.Find(elementId) : null);

        /// <summary>Alias of <see cref="SelectMetadata(DesignerElementMetadata)"/> matching the requested selection-service shape.</summary>
        public void Select(DesignerElementMetadata element) => SelectMetadata(element);

        public void AddToSelection(DesignerElementMetadata element)
        {
            if (element == null || _selection.Contains(element)) return;
            _selection.Add(element);
            RaiseSelectionChanged();
        }

        public void RemoveFromSelection(DesignerElementMetadata element)
        {
            if (element == null || !_selection.Remove(element)) return;
            if (KeyObject == element) KeyObject = null;
            RaiseSelectionChanged();
        }

        public void ToggleSelection(DesignerElementMetadata element)
        {
            if (element == null) return;
            if (!_selection.Remove(element))
                _selection.Add(element);
            else if (KeyObject == element)
                KeyObject = null;
            RaiseSelectionChanged();
        }

        public void SelectMany(IEnumerable<DesignerElementMetadata> elements)
        {
            _selection.Clear();
            if (elements != null)
                foreach (var e in elements)
                    if (e != null && !_selection.Contains(e))
                        _selection.Add(e);
            KeyObject = null;
            RaiseSelectionChanged();
        }

        public void SelectAll()
        {
            if (Metadata == null) return;
            SelectMany(Metadata.elements);
        }

        public bool IsSelected(DesignerElementMetadata element) => element != null && _selection.Contains(element);

        public List<DesignerElementMetadata> GetChildren(DesignerElementMetadata element)
        {
            var result = new List<DesignerElementMetadata>();
            if (Metadata == null || element == null) return result;
            foreach (var e in Metadata.elements)
                if (e != null && e.parentId == element.elementId)
                    result.Add(e);
            return result;
        }

        public void SelectChildren(DesignerElementMetadata element)
        {
            var children = GetChildren(element);
            if (children.Count > 0)
                SelectMany(children);
        }

        public void SelectParent(DesignerElementMetadata element)
        {
            if (Metadata == null || element == null || string.IsNullOrEmpty(element.parentId)) return;
            var parent = Metadata.Find(element.parentId);
            if (parent != null)
                SelectMetadata(parent);
        }

        public void RenameElement(DesignerElementMetadata element, string displayName)
        {
            if (element == null) return;
            UpdateElement(element, e => e.displayName = displayName, "Rename NexUI Element");
        }

        /// <summary>
        /// Reparents a single element (keeping its canvas position). Superseded by the richer
        /// <see cref="ReparentElement"/> in the hierarchy partial; retained for existing callers.
        /// </summary>
        public void SetElementParent(DesignerElementMetadata element, DesignerElementMetadata parent)
            => ReparentElement(element, parent);

        public void ClearSelection()
        {
            if (_selection.Count == 0 && SelectedElement == null) return;
            _selection.Clear();
            KeyObject = null;
            RaiseSelectionChanged();
        }

        public void SetKeyObject(DesignerElementMetadata element)
        {
            if (element == null || !_selection.Contains(element) || _selection.Count < 2) return;
            KeyObject = element;
            MultiSelectionChanged?.Invoke(_selection);
            CanvasChanged?.Invoke();
        }

        private void RaiseSelectionChanged()
        {
            var primary = SelectedMetadata;
            if (!string.IsNullOrEmpty(_selectedComponentPartId) &&
                (primary == null || DesignerComponentRegistry.Get(primary.elementType).GetPart(_selectedComponentPartId) == null))
            {
                _selectedComponentPartId = null;
                ComponentPartSelectionChanged?.Invoke(null);
            }
            SelectedElement = primary != null && TryFindElement(primary.elementId, out var handle) ? handle : null;
            SelectionChanged?.Invoke(SelectedElement);
            MetadataSelectionChanged?.Invoke(primary);
            MultiSelectionChanged?.Invoke(_selection);
            if (CurrentScreen != null)
                EditorPrefs.SetString(PrefPrefix + "Selection." + CurrentScreen.ScreenId, primary?.elementId ?? string.Empty);
        }

        public void RebuildPreview()
        {
            using var markerScope = CanvasRebuildMarker.Auto();
            var selectedIds = new List<string>();
            foreach (var selected in _selection)
                if (selected != null && !string.IsNullOrEmpty(selected.elementId)) selectedIds.Add(selected.elementId);
            var keyObjectId = KeyObject?.elementId;
            if (PreviewSurface != null && CurrentBackend != null)
                CurrentBackend.DestroyPreviewSurface(PreviewSurface);

            PreviewSurface = null;
            CurrentBackend = null;

            if (CurrentScreen != null && DesignerBackendRegistry.TryGet(CurrentScreen.backendAsset.backend, out var backend))
            {
                CurrentBackend = backend;
                PreviewSurface = backend.CreatePreviewSurface(CurrentScreen);
            }

            RestoreSelection(selectedIds, keyObjectId);
            PreviewRebuilt?.Invoke();
            Validate();
        }

        /// <summary>
        /// Persists the screen through the backend-appropriate serializer. The returned
        /// report (also stored in <see cref="LastSaveReport"/> and logged) states exactly
        /// what was written to disk and what was preview-only/skipped.
        /// </summary>
        public DesignerSaveReport Save()
        {
            using var markerScope = PublishMarker.Auto();
            var report = new DesignerSaveReport();

            if (CurrentScreen == null)
            {
                report.Warn("No screen is open; nothing was saved.");
                LastSaveReport = report;
                SaveCompleted?.Invoke(report);
                return report;
            }

            if (Metadata != null && !string.IsNullOrEmpty(Metadata.screenId) &&
                !string.Equals(Metadata.screenId, CurrentScreen.ScreenId, StringComparison.Ordinal))
            {
                report.Error($"Metadata '{Metadata.name}' belongs to screen '{Metadata.screenId}', not '{CurrentScreen.ScreenId}'.");
                LastSaveReport = report;
                SaveCompleted?.Invoke(report);
                Debug.LogError("[NexUI Studio] " + report.Details());
                return report;
            }

            var preflightIssues = DesignerValidationService.Validate(CurrentScreen, Metadata);
            foreach (var issue in preflightIssues)
                if (issue.Severity == DesignerValidationSeverity.Error)
                    report.Error($"Validation {issue.Code}: {issue.Message}  →  {issue.Fix}");
            if (report.HasErrors)
            {
                LastSaveReport = report;
                SaveCompleted?.Invoke(report);
                Debug.LogError("[NexUI Studio] Save blocked by validation. " + report.Details());
                Validate();
                return report;
            }

            var screenBeforePublish = EditorJsonUtility.ToJson(CurrentScreen);
            var screenWasDirtyBeforePublish = EditorUtility.IsDirty(CurrentScreen);
            var serializer = DesignerSerializerRegistry.Get(CurrentScreen.backendAsset.backend);

            // Component instances are references, not copies: the backend must receive the flattened
            // tree. The expansion is a throw-away in-memory asset, so the authored metadata is never
            // reshaped by a save - which also means we must save the authored asset ourselves, since
            // the serializer only knows about the copy it was handed.
            var expansion = DesignerComponentExpander.Expand(Metadata, DesignerComponentLibrary.Resolver);
            try
            {
                foreach (var issue in expansion.Issues)
                {
                    var text = $"Component '{issue.InstanceElementId}': {issue.Message}";
                    if (issue.Kind == DesignerComponentExpansionIssueKind.MissingDefinition ||
                        issue.Kind == DesignerComponentExpansionIssueKind.CircularReference ||
                        issue.Kind == DesignerComponentExpansionIssueKind.BudgetExceeded)
                        report.Error(text + "  →  " + issue.Fix);
                    else
                        report.Warn(text);
                }
                if (!report.HasErrors)
                {
                    SynchronizeScreenMotionReferences();
                    if (Metadata != null)
                    {
                        CurrentScreen.variants = VariantService.Compile(Metadata);
                        CurrentScreen.responsiveRules = ResponsiveService.Compile(Metadata);
                        EditorUtility.SetDirty(CurrentScreen);
                    }
                    report.Merge(serializer.Save(CurrentScreen, expansion.Expanded));
                }
            }
            finally
            {
                if (expansion.ContainsInstances && Metadata != null)
                    AssetDatabase.SaveAssetIfDirty(Metadata);
                expansion.Dispose();
            }

            // B8: keep the git-friendly companion JSON in sync with every save so it's always
            // the reviewable diff for a PR, never stale relative to the .asset.
            if (Metadata != null && !report.HasErrors)
            {
                var jsonPath = DesignerMetadataJsonSerializer.Export(Metadata);
                if (!string.IsNullOrEmpty(jsonPath))
                    report.MarkChanged($"Companion JSON: {jsonPath}");
            }

            if (report.HasErrors)
            {
                EditorJsonUtility.FromJsonOverwrite(screenBeforePublish, CurrentScreen);
                if (screenWasDirtyBeforePublish) EditorUtility.SetDirty(CurrentScreen);
                else EditorUtility.ClearDirty(CurrentScreen);
            }

            if (report.HasErrors)
                Debug.LogError("[NexUI Studio] " + report.Details());
            else if (report.HasWarnings)
                Debug.LogWarning("[NexUI Studio] " + report.Details());
            else
                Debug.Log("[NexUI Studio] " + report.Details());

            LastSaveReport = report;
            SaveCompleted?.Invoke(report);
            if (!report.HasErrors)
            {
                CaptureBaselines();
                SetDirtyState(false);
            }
            Validate();
            return report;
        }

        /// <summary>Inspects the next backend save without dirtying or writing any asset.</summary>
        public DesignerSaveReport PreviewSave()
        {
            var report = DesignerSavePreviewService.Preview(CurrentScreen, Metadata);
            LastSavePreviewReport = report;
            SavePreviewCompleted?.Invoke(report);
            return report;
        }

        public void Validate()
        {
            using var markerScope = ValidationMarker.Auto();
            _validationIssues.Clear();
            _validationIssues.AddRange(DesignerValidationService.Validate(CurrentScreen, Metadata));

            _validationMessages.Clear();
            ErrorCount = 0;
            WarningCount = 0;
            foreach (var issue in _validationIssues)
            {
                _validationMessages.Add(issue.ToString());
                if (issue.Severity == DesignerValidationSeverity.Error) ErrorCount++;
                else if (issue.Severity == DesignerValidationSeverity.Warning) WarningCount++;
            }

            ValidationChanged?.Invoke();
        }

        public void SetResolution(Vector2Int resolution)
        {
            Resolution = resolution;
            CanvasChanged?.Invoke();
            PreviewRebuilt?.Invoke();
        }

        public void SetZoom(float zoom)
        {
            Zoom = Mathf.Clamp(zoom, 0.15f, 2.0f);
            EditorPrefs.SetFloat(PrefPrefix + "Zoom", Zoom);
            CanvasChanged?.Invoke();
        }

        public void ZoomBy(float delta) => SetZoom(Zoom + delta);

        public void SetSnap(bool enabled)
        {
            SnapEnabled = enabled;
            EditorPrefs.SetBool(PrefPrefix + "Snap", enabled);
            CanvasChanged?.Invoke();
        }

        public void SetGridSize(float size)
        {
            GridSize = Mathf.Clamp(size, 1f, 64f);
            EditorPrefs.SetFloat(PrefPrefix + "GridSize", GridSize);
            CanvasChanged?.Invoke();
        }

        public void SetTool(DesignerTool tool)
        {
            if (CurrentTool == tool) return;
            CurrentTool = tool;
            EditorPrefs.SetInt(PrefPrefix + "Tool", (int)tool);
            UIStateChanged?.Invoke();
        }

        public void SetSidebarTab(DesignerSidebarTab tab)
        {
            if (SidebarTab == tab) return;
            SidebarTab = tab;
            EditorPrefs.SetInt(PrefPrefix + "SidebarTab", (int)tab);
            UIStateChanged?.Invoke();
        }

        public void SetInspectorTab(DesignerInspectorTab tab)
        {
            if (InspectorTab == tab) return;
            InspectorTab = tab;
            EditorPrefs.SetInt(PrefPrefix + "InspectorTab", (int)tab);
            UIStateChanged?.Invoke();
        }

        public void SetBottomTab(DesignerBottomTab tab, bool open = true)
        {
            BottomTab = tab;
            BottomDrawerOpen = open;
            EditorPrefs.SetInt(PrefPrefix + "BottomTab", (int)tab);
            EditorPrefs.SetBool(PrefPrefix + "BottomOpen", BottomDrawerOpen);
            UIStateChanged?.Invoke();
        }

        public void SetBottomDrawerOpen(bool open)
        {
            if (BottomDrawerOpen == open) return;
            BottomDrawerOpen = open;
            EditorPrefs.SetBool(PrefPrefix + "BottomOpen", open);
            UIStateChanged?.Invoke();
        }

        public void SetBottomDrawerHeight(float height)
        {
            BottomDrawerHeight = Mathf.Clamp(height, 180f, 520f);
            EditorPrefs.SetFloat(PrefPrefix + "BottomHeight", BottomDrawerHeight);
            UIStateChanged?.Invoke();
        }

        public void SetPreviewState(string state)
        {
            PreviewState = state;
            CanvasChanged?.Invoke();
        }

        public void SetInputMode(string mode)
        {
            InputMode = mode;
            CanvasChanged?.Invoke();
        }

        public void SetTheme(string themeId)
        {
            if (!string.IsNullOrEmpty(themeId))
                NexUITheme.Use(themeId);
            PreviewRebuilt?.Invoke();
        }

        public bool TryFindElement(string elementId, out IUIElementHandle handle)
        {
            handle = PreviewSurface?.TryFind(elementId);
            return handle != null;
        }

        /// <summary>
        /// Adds metadata entries for named backend elements (UXML names / prefab GameObject
        /// names) that have no metadata yet. Returns the number of elements added.
        /// </summary>
        /// <summary>
        /// Report from the last Prefab Import, so a panel can show what was skipped or approximated
        /// instead of the user having to read the console.
        /// </summary>
        public DesignerSaveReport LastImportReport { get; private set; }

        public int SyncMetadataFromBackend()
        {
            if (Metadata == null || CurrentScreen == null) return 0;
            var asset = CurrentScreen.backendAsset.asset;
            var added = 0;

            if (CurrentScreen.backendAsset.backend == UIRenderBackend.UIToolkit && asset is UnityEngine.UIElements.VisualTreeAsset vta)
            {
                added = UIToolkitAssetSerializer.SyncMetadataFromUxml(Metadata, vta);
            }
            else if (CurrentScreen.backendAsset.backend == UIRenderBackend.UGUI && asset is GameObject prefab)
            {
                Undo.RecordObject(Metadata, "Import Prefab");

                // Full import, not the name-only stubs this used to create: geometry, the component
                // stack and every serialized value come across, so the screen is actually editable
                // rather than being a list of empty placeholders named after GameObjects.
                var result = StudioPrefabImporter.ImportInto(Metadata, prefab);
                added = result.Elements.Count;
                LastImportReport = result.Report;
                Debug.Log("[NexUI Studio] " + result.Report.Details());

                if (added > 0)
                {
                    EditorUtility.SetDirty(Metadata);
                    SetDirtyState(true);
                }
            }

            if (added > 0)
            {
                MetadataChanged?.Invoke(Metadata);
                MetadataSelectionChanged?.Invoke(SelectedMetadata);
            }
            CanvasChanged?.Invoke();
            Validate();
            return added;
        }

        /// <summary>
        /// B8: overwrites <see cref="Metadata"/> with the contents of its companion JSON file
        /// (Undo-tracked) - use after resolving a Git merge conflict in the JSON to push the
        /// merged result back into the <c>.asset</c>. Returns false if there's no JSON file yet
        /// (nothing has been saved through this screen) or it failed to parse.
        /// </summary>
        public bool SyncMetadataFromJson()
        {
            if (Metadata == null) return false;
            var applied = DesignerMetadataJsonSerializer.Import(Metadata);
            if (applied)
            {
                MetadataChanged?.Invoke(Metadata);
                MetadataSelectionChanged?.Invoke(SelectedMetadata);
                CanvasChanged?.Invoke();
                Validate();
            }
            return applied;
        }

        /// <summary>
        /// Applies Designer metadata to the live preview surface only (names, classes,
        /// position, size, visibility, binding). This is preview-only and is NOT written to
        /// disk until the user saves.
        /// </summary>
        public void ApplyMetadataToPreview()
        {
            if (Metadata == null || CurrentBackend == null || PreviewSurface == null) return;
            foreach (var element in Metadata.elements)
            {
                if (element == null || string.IsNullOrEmpty(element.elementId)) continue;
                if (!TryFindElement(element.elementId, out var handle))
                    handle = CurrentBackend.CreateElement(PreviewSurface, element.parentId,
                        new DesignerElementCreateInfo { elementId = element.elementId, displayName = element.displayName });
                if (handle == null) continue;

                CurrentBackend.SetPosition(handle, element.rect.position);
                CurrentBackend.SetSize(handle, element.rect.size);
                CurrentBackend.SetVisible(handle, !element.hiddenInDesigner);
                CurrentBackend.SetBinding(handle, element.binding);
                foreach (var cls in element.classes)
                    CurrentBackend.AddClass(handle, cls);
            }
            PreviewRebuilt?.Invoke();
        }

        public DesignerElementMetadata CreateMetadataElement(DesignerElementType type)
            => CreateMetadataElement(type.ToString());

        /// <summary>
        /// Creates an element of any registered component type, including Unity's stock uGUI /
        /// UI Toolkit controls, whose ids are namespaced strings ("UGUI.Button") with no
        /// <see cref="DesignerElementType"/> member. Every creation default comes from the type's
        /// registry descriptor, so a new component type never needs a change here.
        /// </summary>
        public DesignerElementMetadata CreateMetadataElement(string typeId)
        {
            if (Metadata == null) return null;
            if (string.IsNullOrEmpty(typeId)) typeId = DesignerElementType.Panel.ToString();
            var descriptor = DesignerComponentRegistry.Get(typeId);
            RecordMetadata("Create NexUI Element");
            var element = new DesignerElementMetadata
            {
                elementId = NextElementId(descriptor),
                displayName = descriptor.DisplayName,
                elementType = typeId,
                rect = new Rect(96, 96, descriptor.DefaultSize.x, descriptor.DefaultSize.y),
                text = descriptor.DefaultText ?? string.Empty,
                tint = descriptor.DefaultColor,
                shape = descriptor.DefaultShape,
                textColor = Color.white,
                fontSize = descriptor.Category == DesignerComponentCategory.Text ? 18 : 14,
                accessibilityRole = descriptor.DefaultAccessibilityRole
            };
            // A palette entry is a preset: it stamps components, and from here on the element is
            // whatever those components say it is.
            DesignerComponentPresetComposer.Stamp(element, typeId,
                CurrentBackend != null && CurrentBackend.Backend == emiteat.NexUI.Abstractions.UIRenderBackend.UIToolkit
                    ? DesignerUIComponentFamily.UIToolkit
                    : DesignerUIComponentFamily.UGUI);

            DesignerPropertyAdapter.SetBackgroundColor(element, element.tint);
            DesignerPropertyAdapter.SetTextColor(element, element.textColor);
            DesignerPropertyAdapter.SetFontSize(element, element.fontSize);
            Metadata.elements.Add(element);
            MarkMetadataDirty();
            SelectMetadata(element);
            return element;
        }

        /// <summary>
        /// The backend components can currently run on, derived from the open screen.
        /// </summary>
        public DesignerUIComponentFamily ComponentBackend =>
            CurrentBackend != null && CurrentBackend.Backend == emiteat.NexUI.Abstractions.UIRenderBackend.UIToolkit
                ? DesignerUIComponentFamily.UIToolkit
                : DesignerUIComponentFamily.UGUI;

        /// <summary>
        /// Creates an element with no preset behind it - the equivalent of Unity's
        /// <c>GameObject &gt; Create Empty</c>. Optionally attaches one component straight away, which
        /// is how the palette's component entries place something directly.
        /// </summary>
        public DesignerElementMetadata CreateEmptyMetadataElement(string componentTypeId = null)
        {
            if (Metadata == null) return null;

            var componentType = string.IsNullOrEmpty(componentTypeId)
                ? null
                : DesignerUIComponentRegistry.Get(componentTypeId);

            RecordMetadata("Create NexUI Element");
            var element = new DesignerElementMetadata
            {
                elementId = NextElementId(DesignerComponentRegistry.Get("Container")),
                displayName = componentType?.DisplayName ?? "Element",
                // Empty elements are not a palette preset, so nothing claims otherwise: the element is
                // exactly the components it carries.
                elementType = "Custom",
                rect = new Rect(96, 96, 160, 80),
                text = string.Empty,
                tint = new Color(0f, 0f, 0f, 0f),
                textColor = Color.white,
                fontSize = 14
            };

            DesignerElementComponentAccess.EnsureCore(element);
            if (componentType != null)
                DesignerElementComponentAccess.Attach(element, componentType.TypeId, ComponentBackend);

            DesignerPropertyAdapter.SetBackgroundColor(element, element.tint);
            DesignerPropertyAdapter.SetTextColor(element, element.textColor);
            DesignerPropertyAdapter.SetFontSize(element, element.fontSize);
            Metadata.elements.Add(element);
            MarkMetadataDirty();
            SelectMetadata(element);
            return element;
        }

        /// <summary>Attaches a component to every selected element in one Undo step.</summary>
        public int AttachComponentToSelection(string componentTypeId)
        {
            if (Metadata == null || _selection.Count == 0 || string.IsNullOrEmpty(componentTypeId)) return 0;

            var attached = 0;
            NexUIDesignerUndo.Group("Add Component", () =>
            {
                RecordMetadata("Add Component");
                foreach (var element in _selection)
                {
                    if (element == null) continue;
                    DesignerElementComponentAccess.EnsureCore(element);
                    if (DesignerElementComponentAccess.Attach(element, componentTypeId, ComponentBackend) != null)
                        attached++;
                }
                MarkMetadataDirty();
            });
            return attached;
        }

        /// <summary>
        /// Deletes every currently selected element (multi-select aware). By default each
        /// element's whole subtree is removed (requirement default = "Delete with Children"); pass
        /// <paramref name="withChildren"/> = false to instead lift the direct children up to the
        /// deleted element's parent (keeping their canvas positions). Single Undo group.
        /// </summary>
        public void DeleteSelectedMetadata(bool withChildren = true)
        {
            if (Metadata == null || _selection.Count == 0) return;
            RecordMetadata(withChildren ? "Delete NexUI Element (with children)" : "Delete NexUI Element (keep children)");

            // Snapshot selection; when deleting with children, skip nodes already covered by an
            // ancestor also being deleted so we don't double-process a subtree.
            var targets = new List<DesignerElementMetadata>(_selection);
            foreach (var element in targets)
            {
                if (element == null || !Metadata.elements.Contains(element)) continue;
                if (withChildren)
                {
                    foreach (var d in DesignerHierarchyUtility.GetDescendants(Metadata, element))
                        Metadata.elements.Remove(d);
                }
                else
                {
                    var children = DesignerHierarchyUtility.GetOrderedChildren(Metadata, element);
                    ReparentElementsInternal(children, element.parentId ?? string.Empty, true);
                }
                Metadata.elements.Remove(element);
            }
            _selection.Clear();
            DesignerHierarchyUtility.NormalizeSiblingIndices(Metadata);
            MarkMetadataDirty();
            RaiseSelectionChanged();
        }

        public void DeleteSelection() => DeleteSelectedMetadata();

        private List<DesignerElementMetadata> CollectCloneClosure(IEnumerable<DesignerElementMetadata> selection)
        {
            var selected = new HashSet<DesignerElementMetadata>();
            if (selection != null)
                foreach (var element in selection)
                    if (element != null && Metadata.elements.Contains(element)) selected.Add(element);

            var roots = new List<DesignerElementMetadata>();
            foreach (var element in selected)
            {
                var coveredBySelectedAncestor = false;
                foreach (var possibleAncestor in selected)
                    if (possibleAncestor != element && DesignerHierarchyUtility.IsSelfOrDescendant(Metadata, element, possibleAncestor))
                    {
                        coveredBySelectedAncestor = true;
                        break;
                    }
                if (!coveredBySelectedAncestor) roots.Add(element);
            }
            roots.Sort((a, b) => Metadata.elements.IndexOf(a).CompareTo(Metadata.elements.IndexOf(b)));

            var result = new List<DesignerElementMetadata>();
            var seen = new HashSet<DesignerElementMetadata>();
            foreach (var root in roots)
            {
                if (seen.Add(root)) result.Add(root);
                foreach (var child in DesignerHierarchyUtility.GetDescendants(Metadata, root))
                    if (seen.Add(child)) result.Add(child);
            }
            return result;
        }

        private List<DesignerElementMetadata> CloneElementsIntoMetadata(
            IEnumerable<DesignerElementMetadata> sources, Vector2 offset)
        {
            var sourceList = new List<DesignerElementMetadata>();
            if (sources != null)
                foreach (var source in sources)
                    if (source != null) sourceList.Add(source);
            var result = new List<DesignerElementMetadata>();
            if (sourceList.Count == 0) return result;

            var occupied = new HashSet<string>();
            foreach (var element in Metadata.elements)
                if (element != null && !string.IsNullOrEmpty(element.elementId)) occupied.Add(element.elementId);
            var idMap = new Dictionary<string, string>();

            // Component references are stored by stable id, so re-pointing a copy at its own children
            // needs the stable-id map as well as the public elementId one.
            var stableIdMap = new Dictionary<string, string>();
            var pairs = new List<(DesignerElementMetadata source, DesignerElementMetadata clone)>();

            foreach (var source in sourceList)
            {
                var baseId = (string.IsNullOrEmpty(source.elementId) ? "element" : source.elementId) + "Copy";
                var newId = baseId;
                for (var suffix = 1; occupied.Contains(newId); suffix++) newId = baseId + suffix;
                occupied.Add(newId);
                if (!string.IsNullOrEmpty(source.elementId) && !idMap.ContainsKey(source.elementId))
                    idMap.Add(source.elementId, newId);

                var clone = DesignerMetadataUtility.Clone(source);
                clone.stableId = Guid.NewGuid().ToString("N");
                if (!string.IsNullOrEmpty(source.stableId) && !stableIdMap.ContainsKey(source.stableId))
                    stableIdMap.Add(source.stableId, clone.stableId);
                clone.elementId = newId;
                clone.displayName = string.IsNullOrEmpty(source.displayName)
                    ? (source.elementId ?? "Element") + " Copy"
                    : source.displayName + " Copy";
                clone.rect = new Rect(source.rect.position + offset, source.rect.size);
                pairs.Add((source, clone));
                result.Add(clone);
            }

            foreach (var pair in pairs)
            {
                var clone = pair.clone;
                if (!string.IsNullOrEmpty(pair.source.parentId) && idMap.TryGetValue(pair.source.parentId, out var parentId))
                    clone.parentId = parentId;
                RemapFocus(clone.focus, idMap);
                DesignerMetadataUtility.RemapComponentReferences(clone, stableIdMap);
                Metadata.elements.Add(clone);
            }

            DuplicateMotionBindings(idMap);
            DesignerHierarchyUtility.NormalizeSiblingIndices(Metadata);
            return result;
        }

        private static void RemapFocus(DesignerFocusMetadata focus, IReadOnlyDictionary<string, string> idMap)
        {
            if (focus == null) return;
            if (!string.IsNullOrEmpty(focus.upElementId) && idMap.TryGetValue(focus.upElementId, out var up)) focus.upElementId = up;
            if (!string.IsNullOrEmpty(focus.downElementId) && idMap.TryGetValue(focus.downElementId, out var down)) focus.downElementId = down;
            if (!string.IsNullOrEmpty(focus.leftElementId) && idMap.TryGetValue(focus.leftElementId, out var left)) focus.leftElementId = left;
            if (!string.IsNullOrEmpty(focus.rightElementId) && idMap.TryGetValue(focus.rightElementId, out var right)) focus.rightElementId = right;
        }

        private void DuplicateMotionBindings(IReadOnlyDictionary<string, string> idMap)
        {
            if (Metadata?.screenMotion?.bindings == null || idMap.Count == 0) return;
            var originals = new List<DesignerMotionBinding>(Metadata.screenMotion.bindings);
            var occupied = new HashSet<string>();
            foreach (var binding in originals)
                if (binding != null && !string.IsNullOrEmpty(binding.bindingId)) occupied.Add(binding.bindingId);
            foreach (var binding in originals)
            {
                if (binding == null || string.IsNullOrEmpty(binding.targetElementId) ||
                    !idMap.TryGetValue(binding.targetElementId, out var newTarget)) continue;
                var clone = JsonUtility.FromJson<DesignerMotionBinding>(JsonUtility.ToJson(binding));
                var baseId = (string.IsNullOrEmpty(binding.bindingId) ? "motion" : binding.bindingId) + "Copy";
                var newId = baseId;
                for (var suffix = 1; occupied.Contains(newId); suffix++) newId = baseId + suffix;
                occupied.Add(newId);
                clone.bindingId = newId;
                clone.targetElementId = newTarget;
                Metadata.screenMotion.bindings.Add(clone);
            }
        }

        public DesignerElementMetadata DuplicateSelectedMetadata()
        {
            var copies = DuplicateSelection();
            return copies.Count > 0 ? copies[copies.Count - 1] : null;
        }

        /// <summary>Duplicates every selected element (offset by two grid cells) and selects the copies.</summary>
        public List<DesignerElementMetadata> DuplicateSelection()
        {
            var copies = new List<DesignerElementMetadata>();
            if (Metadata == null || _selection.Count == 0) return copies;
            RecordMetadata("Duplicate NexUI Element");
            var offset = new Vector2(GridSize * 2f, GridSize * 2f);
            copies.AddRange(CloneElementsIntoMetadata(CollectCloneClosure(_selection), offset));
            MarkMetadataDirty();
            SelectMany(copies);
            return copies;
        }

        public List<DesignerElementMetadata> DuplicateSelectionAtDragStart()
        {
            var copies = DuplicateSelection();
            if (copies.Count > 0)
                LogAction("Alt Drag Duplicate");
            return copies;
        }

        /// <summary>Copies the current selection into an in-memory clipboard (survives across Paste calls).</summary>
        public void CopySelection()
        {
            _clipboard.Clear();
            foreach (var e in CollectCloneClosure(_selection))
                _clipboard.Add(DesignerMetadataUtility.Clone(e));
        }

        /// <summary>Pastes the clipboard as new elements offset from their originals, and selects the pasted copies.</summary>
        public List<DesignerElementMetadata> PasteSelection()
        {
            var copies = new List<DesignerElementMetadata>();
            if (Metadata == null || _clipboard.Count == 0) return copies;
            RecordMetadata("Paste NexUI Element");
            var offset = new Vector2(GridSize * 2f, GridSize * 2f);
            copies.AddRange(CloneElementsIntoMetadata(_clipboard, offset));
            MarkMetadataDirty();
            SelectMany(copies);
            return copies;
        }

        public void UpdateSelectedRect(Rect rect) => UpdateElementRect(SelectedMetadata, rect);

        /// <summary>
        /// Rect update targeted at a specific element rather than the "primary" selection - used
        /// by the viewport's resize/move-drag commit, where the dragged element isn't always the
        /// primary selection (e.g. dragging a non-primary member of an existing multi-selection).
        /// </summary>
        public void UpdateElementRect(DesignerElementMetadata element, Rect rect)
        {
            if (element == null || element.locked) return;
            RecordMetadata("Edit NexUI Element Rect");
            element.rect = SnapRect(rect);
            MarkMetadataDirty();
            ElementChanged?.Invoke(element);
        }

        /// <summary>
        /// Persists an anchor preset on the selected element's metadata (undo-tracked) and,
        /// when a matching live element exists, applies it to the preview surface through the
        /// backend so the viewport reflects the choice immediately. The saved prefab picks up
        /// the same preset via <c>UGUIAssetSerializer.ApplyRect</c>.
        /// </summary>
        public void SetSelectedAnchor(DesignerAnchorPreset preset)
        {
            if (SelectedMetadata == null) return;
            RecordMetadata("Set NexUI Element Anchor");
            SelectedMetadata.anchorPreset = preset;
            MarkMetadataDirty();
            if (CurrentBackend != null && TryFindElement(SelectedMetadata.elementId, out var handle))
                CurrentBackend.SetAnchor(handle, preset);
        }

        public void MoveSelected(Vector2 delta)
        {
            if (SelectedMetadata == null || SelectedMetadata.locked) return;
            var r = SelectedMetadata.rect;
            r.position += delta;
            UpdateSelectedRect(r);
        }

        /// <summary>
        /// Moves every selected (and unlocked) element by the same delta as a single undo step.
        /// Because element rects are stored in absolute canvas space, a moved element's descendants
        /// are carried along by the same delta so children visually follow their parent (each node
        /// is moved exactly once even if both it and an ancestor are selected).
        /// </summary>
        public void MoveSelection(Vector2 delta)
        {
            if (_selection.Count == 0) return;
            var rects = new Dictionary<DesignerElementMetadata, Rect>();
            foreach (var element in MoveClosure(_selection))
            {
                if (element.locked) continue;
                var r = element.rect;
                r.position += delta;
                rects[element] = r;
            }
            SetElementsRects(rects, "Move NexUI Elements");
        }

        /// <summary>
        /// The set of elements affected by moving <paramref name="roots"/>: the roots plus all of
        /// their descendants, de-duplicated. Descendants follow their parent so the whole subtree
        /// translates together.
        /// </summary>
        internal HashSet<DesignerElementMetadata> MoveClosure(IEnumerable<DesignerElementMetadata> roots)
        {
            var closure = new HashSet<DesignerElementMetadata>();
            if (Metadata == null || roots == null) return closure;
            foreach (var root in roots)
            {
                if (root == null || !closure.Add(root)) continue;
                foreach (var d in DesignerHierarchyUtility.GetDescendants(Metadata, root))
                    closure.Add(d);
            }
            return closure;
        }

        /// <summary>
        /// Applies a batch of rect changes (from group move, align, or distribute) as a single
        /// undo step. All elements live on the same <see cref="Metadata"/> asset, so one
        /// <c>Undo.RecordObject</c> call before the loop is enough to collapse the whole batch.
        /// </summary>
        public void SetElementsRects(IReadOnlyDictionary<DesignerElementMetadata, Rect> rects, string undoName)
        {
            if (Metadata == null || rects == null || rects.Count == 0) return;
            RecordMetadata(undoName);
            foreach (var pair in rects)
            {
                if (pair.Key == null || pair.Key.locked) continue;
                pair.Key.rect = SnapRect(pair.Value);
            }
            MarkMetadataDirty();
        }

        public void AlignSelection(string mode)
        {
            if (_selection.Count == 0) return;
            if (_selection.Count == 1)
            {
                AlignSelected(mode);
                return;
            }

            var bounds = KeyObject != null && _selection.Contains(KeyObject)
                ? KeyObject.rect
                : UIAlignmentUtility.GetBounds(_selection);
            Dictionary<DesignerElementMetadata, Rect> rects = mode switch
            {
                "left" => UIAlignmentUtility.AlignLeft(_selection, bounds),
                "centerX" => UIAlignmentUtility.AlignCenterX(_selection, bounds),
                "right" => UIAlignmentUtility.AlignRight(_selection, bounds),
                "top" => UIAlignmentUtility.AlignTop(_selection, bounds),
                "centerY" => UIAlignmentUtility.AlignCenterY(_selection, bounds),
                "bottom" => UIAlignmentUtility.AlignBottom(_selection, bounds),
                _ => null
            };
            if (rects != null)
                SetElementsRects(rects, "Align NexUI Elements");
        }

        public void DistributeSelectionHorizontal()
            => SetElementsRects(UIAlignmentUtility.DistributeHorizontal(_selection), "Distribute NexUI Elements Horizontally");

        public void DistributeSelectionVertical()
            => SetElementsRects(UIAlignmentUtility.DistributeVertical(_selection), "Distribute NexUI Elements Vertically");

        public void BringSelectionForward()
        {
            if (_selection.Count == 0) return;
            RecordMetadata("Bring Forward");
            UIElementLayerOrder.BringForward(Metadata, _selection);
            MarkMetadataDirty();
        }

        public void SendSelectionBackward()
        {
            if (_selection.Count == 0) return;
            RecordMetadata("Send Backward");
            UIElementLayerOrder.SendBackward(Metadata, _selection);
            MarkMetadataDirty();
        }

        public void BringSelectionToFront()
        {
            if (_selection.Count == 0) return;
            RecordMetadata("Bring To Front");
            UIElementLayerOrder.BringToFront(Metadata, _selection);
            MarkMetadataDirty();
        }

        public void SendSelectionToBack()
        {
            if (_selection.Count == 0) return;
            RecordMetadata("Send To Back");
            UIElementLayerOrder.SendToBack(Metadata, _selection);
            MarkMetadataDirty();
        }

        public void MoveElementInLayerOrder(DesignerElementMetadata element, int delta)
        {
            if (Metadata == null || element == null || delta == 0) return;
            var index = Metadata.elements.IndexOf(element);
            if (index < 0) return;
            var target = Mathf.Clamp(index + delta, 0, Metadata.elements.Count - 1);
            if (target == index) return;

            RecordMetadata("Reorder NexUI Element");
            Metadata.elements.RemoveAt(index);
            Metadata.elements.Insert(target, element);
            MarkMetadataDirty();
            MetadataSelectionChanged?.Invoke(SelectedMetadata);
            MultiSelectionChanged?.Invoke(_selection);
        }

        public void MoveElementToLayerIndex(DesignerElementMetadata element, int targetIndex)
        {
            if (Metadata == null || element == null) return;
            var index = Metadata.elements.IndexOf(element);
            if (index < 0) return;
            targetIndex = Mathf.Clamp(targetIndex, 0, Metadata.elements.Count - 1);
            if (targetIndex == index) return;

            RecordMetadata("Reorder NexUI Element");
            Metadata.elements.RemoveAt(index);
            Metadata.elements.Insert(targetIndex, element);
            MarkMetadataDirty();
            MetadataSelectionChanged?.Invoke(SelectedMetadata);
            MultiSelectionChanged?.Invoke(_selection);
        }

        /// <summary>
        /// Wraps the current selection in a new Panel element sized to their bounding box and
        /// reassigns their <c>parentId</c> to it. Rects stay in the same absolute canvas space
        /// the viewport already renders in (element rects are not parent-relative), so no
        /// coordinate conversion is needed - grouping only changes the saved hierarchy.
        /// </summary>
        public DesignerElementMetadata GroupSelection()
        {
            if (Metadata == null || _selection.Count < 2) return null;
            RecordMetadata("Group NexUI Elements");

            var bounds = UIAlignmentUtility.GetBounds(_selection);
            var group = new DesignerElementMetadata
            {
                elementId = UniqueElementId("group" + _groupCounter++),
                displayName = "Group",
                elementType = "Panel",
                rect = bounds,
                tint = new Color(0f, 0f, 0f, 0f)
            };
            DesignerPropertyAdapter.SetBackgroundColor(group, group.tint);

            var members = new List<DesignerElementMetadata>(_selection);
            var insertIndex = int.MaxValue;
            foreach (var member in members)
                insertIndex = Mathf.Min(insertIndex, Metadata.elements.IndexOf(member));
            if (insertIndex == int.MaxValue || insertIndex > Metadata.elements.Count)
                insertIndex = Metadata.elements.Count;

            Metadata.elements.Insert(insertIndex, group);
            // Preserve the members' current sibling order under the new group parent.
            var orderedMembers = new List<DesignerElementMetadata>(members);
            orderedMembers.Sort((a, b) =>
            {
                if (a.siblingIndex != b.siblingIndex) return a.siblingIndex.CompareTo(b.siblingIndex);
                return Metadata.elements.IndexOf(a).CompareTo(Metadata.elements.IndexOf(b));
            });
            for (int i = 0; i < orderedMembers.Count; i++)
            {
                orderedMembers[i].parentId = group.elementId;
                orderedMembers[i].siblingIndex = i;
            }
            DesignerHierarchyUtility.NormalizeSiblingIndices(Metadata);

            MarkMetadataDirty();
            SelectMetadata(group);
            return group;
        }

        /// <summary>Removes the group's parentId from its direct children and deletes the group wrapper.</summary>
        public void UngroupSelection()
        {
            if (Metadata == null || SelectedMetadata == null) return;
            var group = SelectedMetadata;
            var children = Metadata.elements.FindAll(e => e != null && e.parentId == group.elementId);
            if (children.Count == 0) return;

            RecordMetadata("Ungroup NexUI Elements");
            // Re-parent children to the group's parent, keeping their relative order, then drop the group.
            var ordered = DesignerHierarchyUtility.GetOrderedChildren(Metadata, group);
            var destParent = group.parentId ?? string.Empty;
            var destSiblings = DesignerHierarchyUtility.GetOrderedChildren(Metadata, destParent);
            destSiblings.RemoveAll(ordered.Contains);
            destSiblings.Remove(group);
            var insertAt = group.siblingIndex <= destSiblings.Count ? group.siblingIndex : destSiblings.Count;
            insertAt = Mathf.Clamp(insertAt, 0, destSiblings.Count);
            destSiblings.InsertRange(insertAt, ordered);
            foreach (var child in ordered)
                child.parentId = destParent;
            for (int i = 0; i < destSiblings.Count; i++)
                destSiblings[i].siblingIndex = i;
            Metadata.elements.Remove(group);
            DesignerHierarchyUtility.NormalizeSiblingIndices(Metadata);
            MarkMetadataDirty();
            SelectMany(children);
        }

        public void ResizeSelected(Vector2 delta)
        {
            if (SelectedMetadata == null || SelectedMetadata.locked) return;
            var r = SelectedMetadata.rect;
            r.width = Mathf.Max(24f, r.width + delta.x);
            r.height = Mathf.Max(24f, r.height + delta.y);
            UpdateSelectedRect(r);
        }

        public void AlignSelected(string mode)
        {
            if (SelectedMetadata == null) return;
            var r = SelectedMetadata.rect;
            switch (mode)
            {
                case "left": r.x = 0f; break;
                case "centerX": r.x = (Resolution.x - r.width) * 0.5f; break;
                case "right": r.x = Resolution.x - r.width; break;
                case "top": r.y = 0f; break;
                case "centerY": r.y = (Resolution.y - r.height) * 0.5f; break;
                case "bottom": r.y = Resolution.y - r.height; break;
                case "fill": r = new Rect(0f, 0f, Resolution.x, Resolution.y); break;
            }
            UpdateSelectedRect(r);
        }

        public void UpdateSelectedElement(Action<DesignerElementMetadata> change, string undoName)
            => UpdateElement(SelectedMetadata, change, undoName);

        /// <summary>Element-targeted counterpart to <see cref="UpdateSelectedElement"/> (see <see cref="UpdateElementRect"/>).</summary>
        public void UpdateElement(DesignerElementMetadata element, Action<DesignerElementMetadata> change, string undoName)
        {
            if (element == null || change == null) return;
            RecordMetadata(undoName);
            change(element);
            MarkMetadataDirty();
            ElementChanged?.Invoke(element);
        }

        /// <summary>
        /// Screen-level counterpart to <see cref="UpdateSelectedElement"/>. Mutates the open
        /// <see cref="UIScreenDefinition"/> (e.g. its <c>policy</c> struct) under undo, marks it
        /// dirty and re-validates. Mirrors the element-level record/dirty idiom used above.
        /// </summary>
        public void UpdateScreen(Action<UIScreenDefinition> change, string undoName)
        {
            if (CurrentScreen == null || change == null) return;
            Undo.RecordObject(CurrentScreen, undoName);
            change(CurrentScreen);
            EditorUtility.SetDirty(CurrentScreen);
            SetDirtyState(true);
            LogAction(undoName);
            CanvasChanged?.Invoke();
            Validate();
        }

        public Rect SnapRect(Rect rect)
        {
            if (!SnapEnabled || GridSize <= 0f) return rect;
            rect.x = Mathf.Round(rect.x / GridSize) * GridSize;
            rect.y = Mathf.Round(rect.y / GridSize) * GridSize;
            rect.width = Mathf.Round(rect.width / GridSize) * GridSize;
            rect.height = Mathf.Round(rect.height / GridSize) * GridSize;
            return rect;
        }

        private void RecordMetadata(string name)
        {
            if (Metadata != null)
                Undo.RecordObject(Metadata, name);
            LogAction(name);
        }

        private void MarkMetadataDirty()
        {
            if (Metadata != null)
                EditorUtility.SetDirty(Metadata);
            _expansionValid = false;   // authored data changed; the flattened tree must be recomputed
            SetDirtyState(true);
            CanvasChanged?.Invoke();
            Validate();
        }

        private void SetDirtyState(bool dirty)
        {
            if (dirty)
            {
                _screenExpectedDirtyCount = DirtyCount(_screenBaselineTarget);
                _metadataExpectedDirtyCount = DirtyCount(_metadataBaselineTarget);
            }
            _hasUnsavedChanges = dirty;
            PublishDirtyState();
        }

        private void PublishDirtyState()
        {
            var dirty = HasUnsavedChanges;
            if (_lastReportedDirtyState.HasValue && _lastReportedDirtyState.Value == dirty) return;
            _lastReportedDirtyState = dirty;
            DirtyStateChanged?.Invoke(dirty);
        }

        private bool MatchesBaselines()
            => MatchesBaseline(_screenBaselineTarget, _screenBaselineJson) &&
               MatchesBaseline(_metadataBaselineTarget, _metadataBaselineJson);

        private static bool MatchesBaseline(UnityEngine.Object target, string baselineJson)
        {
            if (target == null) return string.IsNullOrEmpty(baselineJson);
            if (string.IsNullOrEmpty(baselineJson)) return false;
            return string.Equals(EditorJsonUtility.ToJson(target), baselineJson, StringComparison.Ordinal);
        }

        private void OnProjectAssetsChanged()
        {
            if (_disposed) return;
            PublishDirtyState();
        }

        private static void StoreAssetGuid(string key, UnityEngine.Object asset)
        {
            if (asset == null)
            {
                EditorPrefs.DeleteKey(key);
                return;
            }
            var path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path)) EditorPrefs.SetString(key, AssetDatabase.AssetPathToGUID(path));
        }

        public bool DiscardUnsavedChanges()
        {
            if (HasExternalAssetChanges() && !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("NexUI Studio - External Changes",
                    "The open screen or metadata was modified outside this Designer session. Discarding will also revert those external changes to the last Designer baseline.",
                    "Discard All Changes", "Cancel"))
                return false;

            RestoreBaseline(_screenBaselineTarget, _screenBaselineJson, _screenWasDirtyAtBaseline);
            RestoreBaseline(_metadataBaselineTarget, _metadataBaselineJson, _metadataWasDirtyAtBaseline);
            CaptureBaselines();
            SetDirtyState(false);
            ClearSelection();
            RebuildPreview();
            MetadataChanged?.Invoke(Metadata);
            CanvasChanged?.Invoke();
            Validate();
            return true;
        }

        private bool HasExternalAssetChanges()
            => DirtyCount(_screenBaselineTarget) != _screenExpectedDirtyCount ||
               DirtyCount(_metadataBaselineTarget) != _metadataExpectedDirtyCount;

        private static int DirtyCount(UnityEngine.Object target)
            => target != null ? EditorUtility.GetDirtyCount(target) : 0;

        private void CaptureBaselines()
        {
            CaptureScreenBaseline();
            CaptureMetadataBaseline();
        }

        private void CaptureScreenBaseline()
        {
            _screenBaselineTarget = CurrentScreen;
            _screenBaselineJson = CurrentScreen != null ? EditorJsonUtility.ToJson(CurrentScreen) : null;
            _screenExpectedDirtyCount = DirtyCount(CurrentScreen);
            _screenWasDirtyAtBaseline = CurrentScreen != null && EditorUtility.IsDirty(CurrentScreen);
        }

        private void CaptureMetadataBaseline()
        {
            _metadataBaselineTarget = Metadata;
            _metadataBaselineJson = Metadata != null ? EditorJsonUtility.ToJson(Metadata) : null;
            _metadataExpectedDirtyCount = DirtyCount(Metadata);
            _metadataWasDirtyAtBaseline = Metadata != null && EditorUtility.IsDirty(Metadata);
        }

        private static void RestoreBaseline(UnityEngine.Object target, string json, bool keepDirty)
        {
            if (target == null || string.IsNullOrEmpty(json)) return;
            EditorJsonUtility.FromJsonOverwrite(json, target);
            if (keepDirty) EditorUtility.SetDirty(target);
            else EditorUtility.ClearDirty(target);
        }

        private DesignerMetadataAsset ResolveMetadataForScreen(UIScreenDefinition screen)
        {
            if (screen == null || string.IsNullOrEmpty(screen.ScreenId)) return null;
            if (Metadata != null && Metadata.screenId == screen.ScreenId) return Metadata;
            DesignerMetadataAsset match = null;
            foreach (var guid in AssetDatabase.FindAssets("t:DesignerMetadataAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var candidate = AssetDatabase.LoadAssetAtPath<DesignerMetadataAsset>(path);
                if (candidate == null || candidate.screenId != screen.ScreenId) continue;
                if (match != null)
                {
                    Debug.LogWarning($"[NexUI Studio] Multiple metadata assets target screen '{screen.ScreenId}'. Select one explicitly.");
                    return null;
                }
                match = candidate;
            }
            return match;
        }

        private static T LoadAssetGuid<T>(string key) where T : UnityEngine.Object
        {
            var guid = EditorPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(guid)) return null;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private void RestoreSelection()
        {
            if (Metadata == null || CurrentScreen == null) return;
            var id = EditorPrefs.GetString(PrefPrefix + "Selection." + CurrentScreen.ScreenId, string.Empty);
            var element = string.IsNullOrEmpty(id) ? null : Metadata.Find(id);
            if (element != null) SelectMetadata(element);
        }

        private string NextElementId(DesignerElementType type) => NextElementId(DesignerComponentRegistry.Get(type));

        /// <summary>
        /// Element ids double as GameObject names and USS <c>#id</c> selectors, so they must stay
        /// dot-free. Namespaced stock-control types ("UGUI.Button") therefore generate ids from the
        /// descriptor's <see cref="DesignerComponentDescriptor.ElementIdPrefix"/> ("button0").
        /// </summary>
        private string NextElementId(DesignerComponentDescriptor descriptor)
        {
            var prefix = descriptor != null && !string.IsNullOrEmpty(descriptor.ElementIdPrefix)
                ? descriptor.ElementIdPrefix
                : SanitizeIdPrefix(descriptor?.TypeId);
            while (true)
            {
                var id = prefix + _elementCounter++;
                if (Metadata == null || Metadata.Find(id) == null)
                    return id;
            }
        }

        private static string SanitizeIdPrefix(string typeId)
        {
            if (string.IsNullOrEmpty(typeId)) return "element";
            var dot = typeId.LastIndexOf('.');
            if (dot >= 0 && dot < typeId.Length - 1) typeId = typeId.Substring(dot + 1);
            return char.ToLowerInvariant(typeId[0]) + typeId.Substring(1);
        }

        private string UniqueElementId(string baseId)
        {
            var id = string.IsNullOrEmpty(baseId) ? "element" : baseId;
            var candidate = id;
            var index = 1;
            while (Metadata != null && Metadata.Find(candidate) != null)
                candidate = id + index++;
            return candidate;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            EditorApplication.projectChanged -= OnProjectAssetsChanged;
            DisposeComponentExpansion();
            if (PreviewSurface != null && CurrentBackend != null)
                CurrentBackend.DestroyPreviewSurface(PreviewSurface);
        }

        private void OnUndoRedoPerformed()
        {
            if (_disposed) return;
            var selectedIds = new List<string>();
            foreach (var selected in _selection)
                if (selected != null && !string.IsNullOrEmpty(selected.elementId)) selectedIds.Add(selected.elementId);
            var keyObjectId = KeyObject?.elementId;
            _screenExpectedDirtyCount = DirtyCount(_screenBaselineTarget);
            _metadataExpectedDirtyCount = DirtyCount(_metadataBaselineTarget);
            _expansionValid = false;   // Undo can add/remove/retarget component instances
            EnsureScreenMotion();
            SetDirtyState(!MatchesBaselines());
            ApplyMetadataToPreview();
            RestoreSelection(selectedIds, keyObjectId);
            CanvasChanged?.Invoke();
            Validate();
        }

        private void RestoreSelection(IReadOnlyList<string> selectedIds, string keyObjectId)
        {
            _selection.Clear();
            if (Metadata != null && selectedIds != null)
                foreach (var id in selectedIds)
                {
                    var element = Metadata.Find(id);
                    if (element != null && !_selection.Contains(element)) _selection.Add(element);
                }
            KeyObject = !string.IsNullOrEmpty(keyObjectId) && Metadata != null ? Metadata.Find(keyObjectId) : null;
            if (KeyObject != null && !_selection.Contains(KeyObject)) KeyObject = null;
            RaiseSelectionChanged();
        }
    }
}

using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace emiteat.NexUI.Designer.Editor.UI.Panels
{
    /// <summary>
    /// A Project-window-style asset browser inside the Designer: folder navigation, search, kind
    /// filtering, thumbnails, and drag-and-drop onto the canvas or any Inspector object field.
    ///
    /// This replaces the placeholder tab that previously only had a "Show Project Assets" button.
    /// The common authoring verbs are available in-place, while destructive operations still go
    /// through an explicit confirmation and Unity's recoverable Move-to-Trash path.
    /// </summary>
    public sealed class NexUIAssetsPanel : VisualElement
    {
        private const string FolderPrefKey = "NexUI.Designer.Assets.Folder";
        private const string FilterPrefKey = "NexUI.Designer.Assets.Filter";
        private const string GridPrefKey = "NexUI.Designer.Assets.Grid";
        private const float DragThreshold = 6f;

        private readonly VisualElement _breadcrumbs = new VisualElement();
        private readonly ScrollView _list = new ScrollView();
        private readonly Label _status = new Label();
        private readonly List<VisualElement> _rows = new List<VisualElement>();
        private readonly HashSet<string> _selectedPaths = new HashSet<string>();

        private string _folder;
        private string _search = string.Empty;
        private DesignerAssetKind _filter;
        private Vector2 _pointerDownPosition;
        private Object _pointerDownAsset;
        private bool _previewsPending;
        private bool _grid;

        public NexUIAssetsPanel()
        {
            AddToClassList("nexui-assets-panel");

            _folder = EditorPrefs.GetString(FolderPrefKey, DesignerAssetBrowser.RootFolder);
            _filter = (DesignerAssetKind)EditorPrefs.GetInt(FilterPrefKey, (int)DesignerAssetKind.Other);
            _grid = EditorPrefs.GetBool(GridPrefKey, false);

            Add(BuildToolbar());

            _breadcrumbs.AddToClassList("nexui-assets-breadcrumbs");
            Add(_breadcrumbs);

            _list.AddToClassList("nexui-sidebar-scroll");
            Add(_list);

            _status.AddToClassList("nexui-assets-status");
            Add(_status);

            // Rebuild when the project changes so a newly imported sprite shows up without a manual
            // refresh. Unregistered automatically when the panel leaves the hierarchy.
            RegisterCallback<AttachToPanelEvent>(_ => AssetPostprocessorHook.Changed += Refresh);
            RegisterCallback<DetachFromPanelEvent>(_ => AssetPostprocessorHook.Changed -= Refresh);

            Refresh();
        }

        private VisualElement BuildToolbar()
        {
            var row = new VisualElement();
            row.AddToClassList("nexui-assets-toolbar");

            var up = new Button(() => Navigate(DesignerAssetBrowser.ParentFolder(_folder)))
            {
                text = "↑",
                tooltip = DesignerLocalization.T("tooltip.assets.up")
            };
            up.AddToClassList("nexui-square-button");
            row.Add(up);

            var search = new ToolbarSearchField { tooltip = DesignerLocalization.T("tooltip.assets.search") };
            search.AddToClassList("nexui-assets-search");
            search.RegisterValueChangedCallback(evt =>
            {
                _search = evt.newValue ?? string.Empty;
                Refresh();
            });
            row.Add(search);

            var choices = new List<string>();
            foreach (var kind in DesignerAssetBrowser.FilterKinds)
                choices.Add(DesignerAssetBrowser.FilterLabel(kind));

            var currentIndex = Mathf.Max(0, System.Array.IndexOf(DesignerAssetBrowser.FilterKinds, _filter));
            var filter = new PopupField<string>(choices, currentIndex)
            {
                tooltip = DesignerLocalization.T("tooltip.assets.filter")
            };
            filter.AddToClassList("nexui-assets-filter");
            filter.RegisterValueChangedCallback(evt =>
            {
                var index = choices.IndexOf(evt.newValue);
                _filter = index >= 0 ? DesignerAssetBrowser.FilterKinds[index] : DesignerAssetKind.Other;
                EditorPrefs.SetInt(FilterPrefKey, (int)_filter);
                Refresh();
            });
            row.Add(filter);

            var createFolder = new Button(CreateFolder) { text = "+", tooltip = "Create folder" };
            createFolder.AddToClassList("nexui-square-button");
            row.Add(createFolder);

            var grid = new Button(() =>
            {
                _grid = !_grid;
                EditorPrefs.SetBool(GridPrefKey, _grid);
                Refresh();
            }) { text = "▦", tooltip = "Toggle list / grid" };
            grid.AddToClassList("nexui-square-button");
            row.Add(grid);

            return row;
        }

        private void Navigate(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            _folder = folder;
            EditorPrefs.SetString(FolderPrefKey, _folder);
            Refresh();
        }

        private void Refresh()
        {
            if (panel == null) return;   // detached; nothing to draw

            BuildBreadcrumbs();

            _list.Clear();
            _rows.Clear();
            _list.EnableInClassList("nexui-assets-grid", _grid);

            var searching = !string.IsNullOrWhiteSpace(_search);
            var entries = searching
                ? DesignerAssetBrowser.Search(_folder, _search)
                : DesignerAssetBrowser.List(_folder);

            var shown = 0;
            foreach (var entry in entries)
            {
                if (!DesignerAssetBrowser.Matches(entry, searching ? null : _search, _filter)) continue;
                _list.Add(BuildRow(entry, searching));
                shown++;
            }

            if (shown == 0)
                _list.Add(new Label(searching
                    ? DesignerLocalization.T("assets.empty.search")
                    : DesignerLocalization.T("assets.empty.folder"))
                { name = "AssetsEmptyState" });

            _status.text = searching
                ? string.Format(DesignerLocalization.T("assets.status.search"), shown, _folder)
                : string.Format(DesignerLocalization.T("assets.status.folder"), shown, _folder);

            SchedulePreviewRefresh();
        }

        private void BuildBreadcrumbs()
        {
            _breadcrumbs.Clear();
            foreach (var crumb in DesignerAssetBrowser.Breadcrumbs(_folder))
            {
                var target = crumb;
                var button = new Button(() => Navigate(target)) { text = DesignerAssetBrowser.LeafName(crumb) };
                button.AddToClassList("nexui-assets-crumb");
                button.SetEnabled(target != _folder);
                _breadcrumbs.Add(button);
            }
        }

        private VisualElement BuildRow(DesignerAssetEntry entry, bool searching)
        {
            var row = new VisualElement();
            row.AddToClassList("nexui-assets-row");
            row.EnableInClassList("is-grid", _grid);
            row.userData = entry;

            var icon = new VisualElement();
            icon.AddToClassList("nexui-assets-icon");
            row.Add(icon);

            var text = new VisualElement();
            text.AddToClassList("nexui-assets-text");
            var name = new Label(entry.Name);
            name.AddToClassList("nexui-assets-name");
            text.Add(name);
            if (searching)
            {
                var path = new Label(DesignerAssetBrowser.ParentFolder(entry.Path));
                path.AddToClassList("nexui-assets-path");
                text.Add(path);
            }
            row.Add(text);

            if (entry.IsFolder)
            {
                icon.Add(new Label(DesignerAssetBrowser.GlyphFor(DesignerAssetKind.Folder)));
                row.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.clickCount >= 1) Navigate(entry.Path);
                });
                row.RegisterCallback<ContextClickEvent>(evt =>
                {
                    ShowEntryMenu(entry, null);
                    evt.StopPropagation();
                });
                row.tooltip = entry.Path;
                _rows.Add(row);
                return row;
            }

            var asset = AssetDatabase.LoadAssetAtPath<Object>(entry.Path);
            ApplyIcon(icon, asset, entry.Kind);
            row.tooltip = entry.Path;

            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (asset == null && !entry.IsFolder) return;
                if (evt.clickCount >= 2 && !entry.IsFolder) AssetDatabase.OpenAsset(asset);
                else
                {
                    Select(entry.Path, evt.ctrlKey || evt.commandKey);
                    SyncUnitySelection();
                    if (asset != null) EditorGUIUtility.PingObject(asset);
                }
            });

            // Drag out to the canvas or to any ObjectField in the Inspector.
            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                _pointerDownPosition = evt.position;
                _pointerDownAsset = asset;
            });
            row.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (_pointerDownAsset == null || (evt.pressedButtons & 1) == 0) return;
                if (Vector2.Distance(evt.position, _pointerDownPosition) < DragThreshold) return;

                DragAndDrop.PrepareStartDrag();
                var paths = _selectedPaths.Contains(entry.Path)
                    ? new List<string>(_selectedPaths)
                    : new List<string> { entry.Path };
                var objects = new List<Object>();
                foreach (var path in paths)
                {
                    var selectedAsset = AssetDatabase.LoadAssetAtPath<Object>(path);
                    if (selectedAsset != null) objects.Add(selectedAsset);
                }
                DragAndDrop.objectReferences = objects.ToArray();
                DragAndDrop.paths = paths.ToArray();
                DragAndDrop.StartDrag(objects.Count > 1 ? $"{objects.Count} assets" : _pointerDownAsset.name);
                _pointerDownAsset = null;
            });
            row.RegisterCallback<PointerUpEvent>(_ => _pointerDownAsset = null);

            row.RegisterCallback<ContextClickEvent>(evt =>
            {
                ShowEntryMenu(entry, asset);
                evt.StopPropagation();
            });

            _rows.Add(row);
            return row;
        }

        /// <summary>
        /// Project-window-style context menu. Mutating verbs use AssetDatabase so Undo/import state
        /// remains owned by Unity, and deletion is both confirmed and recoverable.
        /// </summary>
        private void ShowEntryMenu(DesignerAssetEntry entry, Object asset)
        {
            var menu = new GenericMenu();

            if (entry.IsFolder)
            {
                menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.asset.open")), false, () => Navigate(entry.Path));
            }
            else if (asset != null)
            {
                menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.asset.open")), false, () => AssetDatabase.OpenAsset(asset));
                menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.asset.showInProject")), false, () =>
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent(DesignerLocalization.T("ctx.asset.open")));
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Rename"), false, () => BeginRename(entry));
            if (!entry.IsFolder)
                menu.AddItem(new GUIContent("Duplicate"), false, () => Duplicate(entry));

            // Move acts on the whole selection when the clicked row is part of it, the way every other
            // file browser behaves; right-clicking outside the selection acts on that row alone.
            var moveTargets = MoveTargets(entry);
            menu.AddItem(new GUIContent(moveTargets.Count > 1
                    ? $"Move {moveTargets.Count} Items To Folder…"
                    : "Move To Folder…"),
                false, () => BeginMove(moveTargets));

            menu.AddItem(new GUIContent("Delete"), false, () => Delete(entry));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.asset.copyPath")), false,
                () => EditorGUIUtility.systemCopyBuffer = entry.Path);
            menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.asset.copyGuid")), false,
                () => EditorGUIUtility.systemCopyBuffer = AssetDatabase.AssetPathToGUID(entry.Path));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.asset.showInExplorer")), false,
                () => EditorUtility.RevealInFinder(entry.Path));
            menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.asset.revealFolder")), false,
                () => Navigate(DesignerAssetBrowser.ParentFolder(entry.Path)));

            menu.ShowAsContext();
        }

        private void Select(string path, bool additive)
        {
            if (!additive) _selectedPaths.Clear();
            if (additive && _selectedPaths.Contains(path)) _selectedPaths.Remove(path);
            else _selectedPaths.Add(path);
            foreach (var row in _rows)
                row.EnableInClassList("is-selected",
                    row.userData is DesignerAssetEntry entry && _selectedPaths.Contains(entry.Path));
        }

        private void SyncUnitySelection()
        {
            var objects = new List<Object>();
            foreach (var path in _selectedPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (asset != null) objects.Add(asset);
            }
            Selection.objects = objects.ToArray();
        }

        private void CreateFolder()
        {
            var name = ObjectNames.GetUniqueName(DesignerAssetBrowser.List(_folder)
                .ConvertAll(e => e.Name).ToArray(), "New Folder");
            var guid = AssetDatabase.CreateFolder(_folder, name);
            var path = AssetDatabase.GUIDToAssetPath(guid);
            Refresh();
            var entry = DesignerAssetBrowser.List(_folder).Find(e => e.Path == path);
            if (entry != null) BeginRename(entry);
        }

        private void BeginRename(DesignerAssetEntry entry)
        {
            var row = _rows.Find(candidate => candidate.userData is DesignerAssetEntry e && e.Path == entry.Path);
            var label = row?.Q<Label>(className: "nexui-assets-name");
            if (row == null || label == null) return;

            var field = new TextField { value = entry.Name };
            field.AddToClassList("nexui-assets-name");
            var parent = label.parent;
            var index = parent.IndexOf(label);
            parent.Remove(label);
            parent.Insert(index, field);
            field.Focus();
            field.SelectAll();

            var finished = false;
            void Finish(bool commit)
            {
                if (finished) return;
                finished = true;
                if (commit && !string.IsNullOrWhiteSpace(field.value) && field.value != entry.Name)
                {
                    var error = AssetDatabase.RenameAsset(entry.Path, field.value.Trim());
                    if (!string.IsNullOrEmpty(error)) Debug.LogWarning($"NexUI Assets: {error}");
                }
                Refresh();
            }
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) Finish(true);
                else if (evt.keyCode == KeyCode.Escape) Finish(false);
            });
            field.RegisterCallback<FocusOutEvent>(_ => Finish(true));
        }

        private void Duplicate(DesignerAssetEntry entry)
        {
            var extension = System.IO.Path.GetExtension(entry.Path);
            var withoutExtension = entry.Path.Substring(0, entry.Path.Length - extension.Length);
            var destination = AssetDatabase.GenerateUniqueAssetPath(withoutExtension + " Copy" + extension);
            if (!AssetDatabase.CopyAsset(entry.Path, destination))
                Debug.LogWarning($"NexUI Assets: could not duplicate '{entry.Path}'.");
            AssetDatabase.SaveAssets();
            Refresh();
        }

        /// <summary>The paths a move should act on when <paramref name="entry"/> was right-clicked.</summary>
        private List<string> MoveTargets(DesignerAssetEntry entry)
        {
            if (!_selectedPaths.Contains(entry.Path)) return new List<string> { entry.Path };
            var targets = new List<string>(_selectedPaths);
            targets.Sort(System.StringComparer.OrdinalIgnoreCase);
            return targets;
        }

        /// <summary>
        /// Opens the destination picker and applies the move.
        /// </summary>
        /// <remarks>
        /// The picker is given the same rule the move itself uses, so a folder it lets the user choose
        /// is a folder the move will accept. Confirmation is explicit because
        /// <see cref="AssetDatabase.MoveAsset"/> cannot be undone - unlike Rename, there is no way back
        /// from a mis-click across a large selection.
        /// </remarks>
        private void BeginMove(List<string> sources)
        {
            var effective = DesignerAssetBrowser.WithoutNestedSources(sources);
            if (effective.Count == 0) return;

            DesignerFolderPickerWindow.Open(
                effective.Count > 1 ? $"Move {effective.Count} Items" : "Move " + DesignerAssetBrowser.LeafName(effective[0]),
                _folder,
                folder => BlockedForAll(effective, folder),
                folder =>
                {
                    var label = effective.Count > 1
                        ? $"Move {effective.Count} items into '{DesignerAssetBrowser.LeafName(folder)}'?"
                        : $"Move '{DesignerAssetBrowser.LeafName(effective[0])}' into '{DesignerAssetBrowser.LeafName(folder)}'?";
                    if (!EditorUtility.DisplayDialog("Move assets?",
                            label + "\n\nMoving assets cannot be undone.", "Move", "Cancel")) return;

                    var result = DesignerAssetBrowser.Move(effective, folder);
                    foreach (var failure in result.Failed) Debug.LogWarning($"NexUI Assets: {failure}");
                    foreach (var skipped in result.Skipped) Debug.Log($"NexUI Assets: {skipped}");

                    _selectedPaths.Clear();
                    foreach (var moved in result.Moved) _selectedPaths.Add(moved);
                    Refresh();
                    _status.text = result.Summary();
                });
        }

        /// <summary>
        /// Why <paramref name="folder"/> is refused for the whole selection, or null when at least one
        /// source can go there. A folder is only disabled when nothing in the selection could move into
        /// it - disabling it because one of twenty items is already there would be unhelpful.
        /// </summary>
        private static string BlockedForAll(List<string> sources, string folder)
        {
            string firstReason = null;
            foreach (var source in sources)
            {
                var reason = DesignerAssetBrowser.MoveBlockedReason(source, folder);
                if (reason == null) return null;
                firstReason ??= reason;
            }
            return firstReason;
        }

        private void Delete(DesignerAssetEntry entry)
        {
            if (!EditorUtility.DisplayDialog("Delete asset?",
                    $"Move '{entry.Name}' to the system Trash?", "Move to Trash", "Cancel")) return;
            if (!AssetDatabase.MoveAssetToTrash(entry.Path))
                Debug.LogWarning($"NexUI Assets: could not move '{entry.Path}' to Trash.");
            _selectedPaths.Remove(entry.Path);
            Refresh();
        }

        private void ApplyIcon(VisualElement icon, Object asset, DesignerAssetKind kind)
        {
            icon.Clear();
            if (asset == null)
            {
                icon.Add(new Label(DesignerAssetBrowser.GlyphFor(kind)));
                return;
            }

            // GetAssetPreview is asynchronous: null means "not generated yet", so fall back to the
            // mini thumbnail now and schedule a repaint while previews are still loading.
            var preview = AssetPreview.GetAssetPreview(asset) ?? AssetPreview.GetMiniThumbnail(asset);
            if (preview != null)
            {
                icon.style.backgroundImage = new StyleBackground(preview);
                if (AssetPreview.IsLoadingAssetPreviews()) _previewsPending = true;
            }
            else
            {
                icon.Add(new Label(DesignerAssetBrowser.GlyphFor(kind)));
                _previewsPending = true;
            }
        }

        private void SchedulePreviewRefresh()
        {
            if (!_previewsPending) return;
            _previewsPending = false;
            schedule.Execute(() =>
            {
                if (panel == null) return;
                foreach (var row in _rows)
                {
                    if (row.userData is not DesignerAssetEntry entry || entry.IsFolder) continue;
                    var icon = row.Q(className: "nexui-assets-icon");
                    if (icon == null) continue;
                    ApplyIcon(icon, AssetDatabase.LoadAssetAtPath<Object>(entry.Path), entry.Kind);
                }
                SchedulePreviewRefresh();
            }).StartingIn(200);
        }

        /// <summary>
        /// Bridges Unity's asset post-processing to the panel. A static event keeps the
        /// <see cref="AssetPostprocessor"/> (which Unity instantiates itself) decoupled from any
        /// particular panel instance.
        /// </summary>
        private sealed class AssetPostprocessorHook : AssetPostprocessor
        {
            public static event System.Action Changed;

            private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                if (imported.Length + deleted.Length + moved.Length > 0)
                    Changed?.Invoke();
            }
        }
    }
}

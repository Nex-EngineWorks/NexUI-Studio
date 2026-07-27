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
    /// It does not try to be Unity's Project window - there is no rename, move, delete or create
    /// here, because those belong to the Project window and duplicating them would mean duplicating
    /// their safety rules. This is a <i>read-only picker</i> tuned for UI authoring: find a sprite or
    /// font fast, drag it onto an element, keep working without leaving the Designer.
    /// </summary>
    public sealed class NexUIAssetsPanel : VisualElement
    {
        private const string FolderPrefKey = "NexUI.Designer.Assets.Folder";
        private const string FilterPrefKey = "NexUI.Designer.Assets.Filter";
        private const float DragThreshold = 6f;

        private readonly VisualElement _breadcrumbs = new VisualElement();
        private readonly ScrollView _list = new ScrollView();
        private readonly Label _status = new Label();
        private readonly List<VisualElement> _rows = new List<VisualElement>();

        private string _folder;
        private string _search = string.Empty;
        private DesignerAssetKind _filter;
        private Vector2 _pointerDownPosition;
        private Object _pointerDownAsset;
        private bool _previewsPending;

        public NexUIAssetsPanel()
        {
            AddToClassList("nexui-assets-panel");

            _folder = EditorPrefs.GetString(FolderPrefKey, DesignerAssetBrowser.RootFolder);
            _filter = (DesignerAssetKind)EditorPrefs.GetInt(FilterPrefKey, (int)DesignerAssetKind.Other);

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
                row.tooltip = entry.Path;
                _rows.Add(row);
                return row;
            }

            var asset = AssetDatabase.LoadAssetAtPath<Object>(entry.Path);
            ApplyIcon(icon, asset, entry.Kind);
            row.tooltip = entry.Path;

            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (asset == null) return;
                if (evt.clickCount >= 2) AssetDatabase.OpenAsset(asset);
                else
                {
                    // Selecting in the Project window is what makes the panel useful next to Unity's
                    // own tooling - the user can still inspect the asset normally.
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                    SetSelectedRow(row);
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
                DragAndDrop.objectReferences = new[] { _pointerDownAsset };
                DragAndDrop.paths = new[] { AssetDatabase.GetAssetPath(_pointerDownAsset) };
                DragAndDrop.StartDrag(_pointerDownAsset.name);
                _pointerDownAsset = null;
            });
            row.RegisterCallback<PointerUpEvent>(_ => _pointerDownAsset = null);

            _rows.Add(row);
            return row;
        }

        private void SetSelectedRow(VisualElement selected)
        {
            foreach (var row in _rows)
                row.EnableInClassList("is-selected", row == selected);
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

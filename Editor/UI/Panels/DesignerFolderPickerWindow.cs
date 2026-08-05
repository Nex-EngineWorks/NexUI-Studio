using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.UI.Panels
{
    /// <summary>
    /// Destination picker for "Move To Folder": every project folder, searchable, with the ones the
    /// current selection cannot go into disabled and labelled with the reason.
    /// </summary>
    /// <remarks>
    /// A system folder dialog would have been less code, but it opens outside the project, returns an
    /// absolute path that then has to be validated back into <c>Assets/</c>, and cannot show why a
    /// folder is not a legal destination. Showing the project's own folders and disabling the illegal
    /// ones means the user finds out before choosing rather than after.
    /// </remarks>
    public sealed class DesignerFolderPickerWindow : EditorWindow
    {
        private const string NewFolderName = "New Folder";

        private Action<string> _onPick;
        private Func<string, string> _blockedReason;
        private ScrollView _list;
        private string _search = string.Empty;
        private string _selected;

        /// <param name="title">Window title; name the operation, not the widget.</param>
        /// <param name="initialFolder">Folder to scroll to and pre-select.</param>
        /// <param name="blockedReason">
        /// Why a candidate folder is not a legal destination, or null when it is. Called per folder.
        /// </param>
        /// <param name="onPick">Invoked with the chosen folder. Not called when the user cancels.</param>
        public static void Open(string title, string initialFolder,
            Func<string, string> blockedReason, Action<string> onPick)
        {
            var window = CreateInstance<DesignerFolderPickerWindow>();
            window.titleContent = new GUIContent(title);
            window._onPick = onPick;
            window._blockedReason = blockedReason;
            window._selected = initialFolder;
            window.minSize = new Vector2(320f, 360f);
            window.Build();
            window.ShowAuxWindow();
        }

        private void Build()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 6;
            rootVisualElement.style.paddingBottom = 6;

            var search = new ToolbarSearchField();
            search.style.marginBottom = 6;
            search.RegisterValueChangedCallback(evt =>
            {
                _search = evt.newValue ?? string.Empty;
                RebuildList();
            });
            rootVisualElement.Add(search);

            _list = new ScrollView { style = { flexGrow = 1 } };
            rootVisualElement.Add(_list);

            var buttons = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd, marginTop = 6 }
            };

            var newFolder = new Button(CreateSubFolder) { text = "New Folder", style = { marginRight = 6 } };
            newFolder.tooltip = "Create a folder inside the selected one and choose it.";
            buttons.Add(newFolder);

            var spacer = new VisualElement { style = { flexGrow = 1 } };
            buttons.Add(spacer);

            buttons.Add(new Button(Close) { text = "Cancel" });
            var confirm = new Button(Confirm) { text = "Move Here", style = { marginLeft = 6 } };
            confirm.name = "confirm";
            buttons.Add(confirm);
            rootVisualElement.Add(buttons);

            RebuildList();
        }

        private void RebuildList()
        {
            _list.Clear();
            foreach (var folder in DesignerAssetBrowser.AllFolders())
            {
                if (!string.IsNullOrWhiteSpace(_search) &&
                    folder.IndexOf(_search.Trim(), StringComparison.OrdinalIgnoreCase) < 0) continue;

                var reason = _blockedReason?.Invoke(folder);
                _list.Add(Row(folder, reason));
            }

            if (_list.childCount == 0)
                _list.Add(new Label("No folder matches.") { style = { opacity = 0.6f, marginTop = 8 } });
            UpdateConfirmState();
        }

        private VisualElement Row(string folder, string blockedReason)
        {
            var depth = DesignerAssetBrowser.Breadcrumbs(folder).Count - 1;
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 4 + depth * 12,
                    paddingTop = 2,
                    paddingBottom = 2
                }
            };

            var label = new Label(DesignerAssetBrowser.LeafName(folder)) { style = { flexGrow = 1 } };
            row.Add(label);

            if (blockedReason != null)
            {
                row.SetEnabled(false);
                row.tooltip = blockedReason;
                row.Add(new Label(blockedReason) { style = { opacity = 0.6f, fontSize = 10 } });
                return row;
            }

            row.tooltip = folder;
            if (folder == _selected) row.style.backgroundColor = new StyleColor(new Color(0.24f, 0.48f, 0.90f, 0.35f));

            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                _selected = folder;
                if (evt.clickCount >= 2) { Confirm(); return; }
                RebuildList();
            });
            return row;
        }

        private void UpdateConfirmState()
        {
            var confirm = rootVisualElement.Q<Button>("confirm");
            if (confirm == null) return;
            var reason = _selected == null ? "Pick a destination folder." : _blockedReason?.Invoke(_selected);
            confirm.SetEnabled(reason == null);
            confirm.tooltip = reason ?? $"Move into {_selected}";
        }

        /// <summary>
        /// Creates a folder inside the current selection so the user does not have to leave the dialog,
        /// go to the Project window, make a folder and start over.
        /// </summary>
        private void CreateSubFolder()
        {
            var parent = string.IsNullOrEmpty(_selected) || !AssetDatabase.IsValidFolder(_selected)
                ? DesignerAssetBrowser.RootFolder
                : _selected;

            var existing = new List<string>();
            foreach (var child in AssetDatabase.GetSubFolders(parent))
                existing.Add(DesignerAssetBrowser.LeafName(child));

            var guid = AssetDatabase.CreateFolder(parent, ObjectNames.GetUniqueName(existing.ToArray(), NewFolderName));
            var created = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(created))
            {
                Debug.LogWarning($"[NexUI Studio] Could not create a folder inside '{parent}'.");
                return;
            }

            _selected = created;
            RebuildList();
        }

        private void Confirm()
        {
            if (string.IsNullOrEmpty(_selected) || _blockedReason?.Invoke(_selected) != null) return;
            var pick = _onPick;
            var folder = _selected;
            Close();
            pick?.Invoke(folder);
        }
    }
}

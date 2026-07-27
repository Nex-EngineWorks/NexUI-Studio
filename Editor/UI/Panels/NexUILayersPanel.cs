using System;
using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Profiling;

namespace emiteat.NexUI.Designer.Editor.UI.Panels
{
    /// <summary>
    /// Real parent/child hierarchy tree for the Layers tab. The tree is derived from
    /// <see cref="DesignerElementMetadata.parentId"/> + <c>siblingIndex</c> (via
    /// <see cref="DesignerHierarchyUtility"/>) - the single source of truth. Supports foldout
    /// collapse (persisted per screen), search that keeps the ancestor path of matches and
    /// force-expands them, sibling reorder, and drag-and-drop reparent (before / into / after)
    /// with cycle rejection. Selection is two-way synced with the canvas through the context.
    /// </summary>
    public sealed class NexUILayersPanel : VisualElement
    {
        private static readonly ProfilerMarker RebuildMarker = new ProfilerMarker("NexUI.Designer.Hierarchy.Rebuild");
        private readonly NexUIDesignerContext _context;
        private readonly ToolbarSearchField _search;
        private readonly ScrollView _list;
        private readonly Label _empty;
        private string _filter = "";

        // Collapsed container ids (persisted per screen). Absent ⇒ expanded.
        private readonly HashSet<string> _collapsed = new HashSet<string>();
        private string _collapsedScreenKey;

        // Active drag state (panel-level so a drag can target any row).
        private DesignerElementMetadata _dragElement;
        private NexUILayerRow _dropTargetRow;
        private DropZone _dropZone;

        internal enum DropZone { None, Before, Into, After }

        public NexUILayersPanel(NexUIDesignerContext context)
        {
            _context = context;
            AddToClassList("nexui-layers-panel");

            _search = new ToolbarSearchField { tooltip = DesignerLocalization.T("tooltip.hierarchy.search") };
            _search.RegisterValueChangedCallback(evt =>
            {
                _filter = evt.newValue ?? "";
                Refresh();
            });
            Add(_search);

            _empty = new Label("Select a Metadata asset to show layers.");
            _empty.AddToClassList("nexui-empty-note");
            Add(_empty);

            _list = new ScrollView();
            _list.AddToClassList("nexui-layers-list");
            _list.style.flexGrow = 1;
            Add(_list);

            // Drop on empty space below the rows ⇒ move to root.
            _list.RegisterCallback<PointerMoveEvent>(OnListDragMove);
            _list.RegisterCallback<PointerUpEvent>(OnListDragEnd);

            var subscriptions = new ContextBoundSubscriptions(this);
            subscriptions.Add<DesignerMetadataAsset>(h => context.MetadataChanged += h, h => context.MetadataChanged -= h, _ => { LoadCollapsedState(); Refresh(); });
            subscriptions.Add(h => context.CanvasChanged += h, h => context.CanvasChanged -= h, Refresh);
            subscriptions.Add<System.Collections.Generic.IReadOnlyList<DesignerElementMetadata>>(h => context.MultiSelectionChanged += h, h => context.MultiSelectionChanged -= h, _ => RefreshSelectionOnly());
            LoadCollapsedState();
            Refresh();
        }

        // ---- collapse persistence ----------------------------------------------------------
        private string CollapsedPrefKey()
        {
            var screen = _context.Metadata != null ? _context.Metadata.screenId : "none";
            return "NexUI.Designer.Layers.Collapsed." + (string.IsNullOrEmpty(screen) ? "none" : screen);
        }

        private void LoadCollapsedState()
        {
            _collapsedScreenKey = CollapsedPrefKey();
            _collapsed.Clear();
            var raw = EditorPrefs.GetString(_collapsedScreenKey, "");
            if (!string.IsNullOrEmpty(raw))
                foreach (var id in raw.Split('|'))
                    if (!string.IsNullOrEmpty(id)) _collapsed.Add(id);
        }

        private void SaveCollapsedState()
            => EditorPrefs.SetString(_collapsedScreenKey ?? CollapsedPrefKey(), string.Join("|", _collapsed));

        internal bool IsCollapsed(string elementId) => _collapsed.Contains(elementId);

        internal void ToggleCollapsed(string elementId)
        {
            if (!_collapsed.Remove(elementId)) _collapsed.Add(elementId);
            SaveCollapsedState();
            Refresh();
        }

        // ---- row model ---------------------------------------------------------------------
        private void Refresh()
        {
            using var markerScope = RebuildMarker.Auto();
            _list.Clear();
            if (_context.Metadata == null || _context.Metadata.elements.Count == 0)
            {
                _empty.style.display = DisplayStyle.Flex;
                _empty.text = _context.Metadata == null ? "Select a Metadata asset to show layers." : "No layers yet.";
                return;
            }

            var rows = BuildRows(out var matchSet);
            _empty.style.display = rows.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _empty.text = "No layers match the filter.";
            foreach (var row in rows)
                _list.Add(new NexUILayerRow(_context, this, row.element, row.depth, row.hasChildren, row.dimmed, Refresh));
            RefreshSelectionOnly();
        }

        private struct RowInfo { public DesignerElementMetadata element; public int depth; public bool hasChildren; public bool dimmed; }

        private List<RowInfo> BuildRows(out HashSet<DesignerElementMetadata> matchSet)
        {
            var result = new List<RowInfo>();
            matchSet = null;
            var asset = _context.Metadata;
            if (asset == null) return result;

            var searching = !string.IsNullOrEmpty(_filter);
            HashSet<DesignerElementMetadata> visible = null;
            var matches = new HashSet<DesignerElementMetadata>();
            if (searching)
            {
                // Keep every match plus the full ancestor chain of each match, so the tree context
                // is preserved and ancestors force-expand regardless of collapse state.
                visible = new HashSet<DesignerElementMetadata>();
                foreach (var e in asset.elements)
                {
                    if (e == null || !Matches(e)) continue;
                    matches.Add(e);
                    var cur = e;
                    var guard = new HashSet<string>();
                    while (cur != null && guard.Add(cur.elementId))
                    {
                        visible.Add(cur);
                        cur = string.IsNullOrEmpty(cur.parentId) ? null : asset.Find(cur.parentId);
                    }
                }
                matchSet = matches;
            }

            void Visit(string parentId, int depth)
            {
                foreach (var child in DesignerHierarchyUtility.GetOrderedChildren(asset, parentId))
                {
                    var hasChildren = DesignerHierarchyUtility.CountChildren(asset, child) > 0;
                    if (searching)
                    {
                        if (!visible.Contains(child)) continue;
                        result.Add(new RowInfo { element = child, depth = depth, hasChildren = hasChildren, dimmed = !matches.Contains(child) });
                        Visit(child.elementId, depth + 1); // always descend while searching
                    }
                    else
                    {
                        result.Add(new RowInfo { element = child, depth = depth, hasChildren = hasChildren, dimmed = false });
                        if (hasChildren && !IsCollapsed(child.elementId))
                            Visit(child.elementId, depth + 1);
                    }
                }
            }

            Visit(string.Empty, 0);
            return result;
        }

        private bool Matches(DesignerElementMetadata element)
        {
            if (string.IsNullOrEmpty(_filter)) return true;
            return (!string.IsNullOrEmpty(element.elementId) && element.elementId.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrEmpty(element.displayName) && element.displayName.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrEmpty(element.elementType) && element.elementType.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void RefreshSelectionOnly()
        {
            foreach (var child in _list.Children())
                if (child is NexUILayerRow row)
                    row.RefreshSelection();
        }

        // ---- drag & drop -------------------------------------------------------------------
        internal void BeginDrag(DesignerElementMetadata element)
        {
            _dragElement = element;
            _dropTargetRow = null;
            _dropZone = DropZone.None;
        }

        internal bool IsDragging => _dragElement != null;

        private void OnListDragMove(PointerMoveEvent evt)
        {
            if (_dragElement == null) return;
            UpdateDropTarget(evt.position);
        }

        /// <summary>Recomputes the hovered row and drop zone (top⇒Before, middle⇒Into, bottom⇒After) and paints indicators.</summary>
        internal void UpdateDropTarget(Vector2 pointer)
        {
            if (_dragElement == null) return;
            ClearDropIndicators();
            _dropTargetRow = null;
            _dropZone = DropZone.None;

            foreach (var child in _list.Children())
            {
                if (child is not NexUILayerRow row) continue;
                var b = row.worldBound;
                if (pointer.y < b.yMin || pointer.y > b.yMax) continue;
                var t = (pointer.y - b.yMin) / Mathf.Max(1f, b.height);
                _dropTargetRow = row;
                _dropZone = t < 0.28f ? DropZone.Before : t > 0.72f ? DropZone.After : DropZone.Into;
                break;
            }

            // Below the last row ⇒ drop to root (After the last root row).
            if (_dropTargetRow == null)
                _dropZone = DropZone.None; // root drop handled on release

            if (_dropTargetRow != null)
            {
                var legal = IsLegalDrop(_dropTargetRow.Element, _dropZone);
                _dropTargetRow.ShowDropIndicator(_dropZone, legal);
            }
        }

        private bool IsLegalDrop(DesignerElementMetadata target, DropZone zone)
        {
            if (_dragElement == null || target == null) return false;
            if (target == _dragElement) return false;
            // Into: target becomes parent. Before/After: target's parent becomes parent.
            var newParentId = zone == DropZone.Into ? target.elementId : (target.parentId ?? string.Empty);
            return !DesignerHierarchyUtility.WouldCreateCycle(_context.Metadata, _dragElement.elementId, newParentId);
        }

        private void OnListDragEnd(PointerUpEvent evt) => CompleteDrag(evt.position);

        internal void CompleteDrag(Vector2 pointer)
        {
            if (_dragElement == null) return;
            var dragged = _dragElement;
            var target = _dropTargetRow;
            var zone = _dropZone;
            ClearDropIndicators();
            _dragElement = null;
            _dropTargetRow = null;
            _dropZone = DropZone.None;

            // Elements to move: the whole current selection if the dragged row is part of it,
            // else just the dragged element. The context keeps only top-most nodes & preserves order.
            IReadOnlyList<DesignerElementMetadata> movers =
                _context.IsSelected(dragged) && _context.SelectedElements.Count > 1
                    ? new List<DesignerElementMetadata>(_context.SelectedElements)
                    : new List<DesignerElementMetadata> { dragged };

            if (target == null)
            {
                // Dropped on empty space ⇒ move to canvas root (append).
                _context.ReparentElements(movers, null);
                return;
            }

            if (!IsLegalDrop(target.Element, zone)) return;

            if (zone == DropZone.Into)
            {
                _context.ReparentElements(movers, target.Element);
            }
            else
            {
                var parent = string.IsNullOrEmpty(target.Element.parentId) ? null : _context.Metadata.Find(target.Element.parentId);
                var siblings = DesignerHierarchyUtility.GetOrderedChildren(_context.Metadata, target.Element.parentId);
                var idx = siblings.IndexOf(target.Element);
                if (zone == DropZone.After) idx += 1;
                _context.ReparentElements(movers, parent, idx);
            }
        }

        internal void CancelDrag()
        {
            ClearDropIndicators();
            _dragElement = null;
            _dropTargetRow = null;
            _dropZone = DropZone.None;
        }

        private void ClearDropIndicators()
        {
            foreach (var child in _list.Children())
                if (child is NexUILayerRow row)
                    row.ClearDropIndicator();
        }
    }

    public sealed class NexUILayerRow : VisualElement
    {
        private readonly NexUIDesignerContext _context;
        private readonly NexUILayersPanel _panel;
        private readonly DesignerElementMetadata _element;
        private readonly Action _refresh;
        private readonly Label _name;
        private TextField _rename;
        private readonly Button _visibility;
        private readonly Button _lock;
        private readonly VisualElement _foldout;
        private readonly VisualElement _dropLine;

        internal DesignerElementMetadata Element => _element;

        public NexUILayerRow(NexUIDesignerContext context, NexUILayersPanel panel, DesignerElementMetadata element,
            int depth, bool hasChildren, bool dimmed, Action refresh)
        {
            _context = context;
            _panel = panel;
            _element = element;
            _refresh = refresh;

            AddToClassList("nexui-layer-row");
            if (dimmed) style.opacity = 0.55f;
            style.paddingLeft = Mathf.Min(60, depth * 14);

            // Foldout arrow (only for containers with children); a fixed-width spacer otherwise so
            // labels stay aligned.
            _foldout = new VisualElement { style = { width = 14, unityTextAlign = TextAnchor.MiddleCenter } };
            if (hasChildren)
            {
                var collapsed = _panel.IsCollapsed(element.elementId);
                var arrow = new Label(collapsed ? "▸" : "▾") { tooltip = "Expand / collapse children." };
                arrow.RegisterCallback<PointerDownEvent>(evt =>
                {
                    _panel.ToggleCollapsed(_element.elementId);
                    evt.StopPropagation();
                });
                _foldout.Add(arrow);
            }
            Add(_foldout);

            _visibility = IconButton(element.hiddenInDesigner ? "○" : "●", "Toggle Designer canvas visibility only (runtime visibility is in Style Inspector).", () =>
                _context.UpdateElement(_element, e => e.hiddenInDesigner = !e.hiddenInDesigner, "Toggle NexUI Element Hidden"));
            Add(_visibility);

            _lock = IconButton(element.locked ? "L" : "-", "Toggle lock.", () =>
                _context.UpdateElement(_element, e => e.locked = !e.locked, "Toggle NexUI Element Lock"));
            Add(_lock);

            // Keep the normal row name as a Label so it behaves like Unity's Hierarchy: users
            // can grab the visible name itself and drag it. A TextField consumes pointer input,
            // which made the previous implementation effectively draggable only from the tiny
            // icon/empty areas. Double-click switches the label to an inline rename field.
            _name = new Label(LabelFor(element)) { tooltip = element.elementType + "  [" + element.elementId + "]" };
            _name.AddToClassList("nexui-layer-name");
            _name.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || evt.clickCount < 2) return;
                BeginRename();
                evt.StopImmediatePropagation();
            });
            Add(_name);

            Add(IconButton("↑", "Move up among siblings.", () => { _context.MoveSiblingBy(_element, -1); _refresh?.Invoke(); }));
            Add(IconButton("↓", "Move down among siblings.", () => { _context.MoveSiblingBy(_element, 1); _refresh?.Invoke(); }));

            // Thin drop indicator line (before/after) painted during drag.
            _dropLine = new VisualElement();
            _dropLine.style.position = Position.Absolute;
            _dropLine.style.left = 0;
            _dropLine.style.right = 0;
            _dropLine.style.height = 2;
            _dropLine.style.display = DisplayStyle.None;
            _dropLine.pickingMode = PickingMode.Ignore;
            Add(_dropLine);

            RegisterCallback<PointerDownEvent>(OnPointerDownSelect, TrickleDown.TrickleDown);
            RegisterCallback<ContextClickEvent>(OnContext);
            RegisterCallback<PointerDownEvent>(OnDragStart);
            RegisterCallback<PointerMoveEvent>(OnDragMove);
            RegisterCallback<PointerUpEvent>(OnDragEnd);
            RefreshSelection();
        }

        public void RefreshSelection()
        {
            EnableInClassList("is-selected", _context.IsSelected(_element));
            EnableInClassList("is-key-object", _context.KeyObject == _element);
            _visibility.text = _element.hiddenInDesigner ? "○" : "●";
            _lock.text = _element.locked ? "L" : "-";
        }

        private void OnPointerDownSelect(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            if (evt.target is TextField || (evt.target as VisualElement)?.parent is TextField) return;
            ApplySelection(evt.shiftKey, evt.ctrlKey || evt.commandKey);
        }

        private void ApplySelection(bool additive, bool toggle)
        {
            if (additive)
                _context.AddToSelection(_element);
            else if (toggle)
                _context.ToggleSelection(_element);
            else if (_context.IsSelected(_element) && _context.SelectedElements.Count > 1)
                _context.SetKeyObject(_element);
            else if (!_context.IsSelected(_element) || _context.SelectedElements.Count != 1)
                _context.SelectMetadata(_element);
        }

        private void OnContext(ContextClickEvent evt)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Select"), false, () => _context.SelectMetadata(_element));
            menu.AddItem(new GUIContent("Select Parent"), false, () => _context.SelectParent(_element));
            menu.AddItem(new GUIContent("Select Children"), false, () => _context.SelectChildren(_element));
            menu.AddItem(new GUIContent("Set Key Object"), _context.KeyObject == _element, () => _context.SetKeyObject(_element));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Move to Root"), false, () => _context.MoveToRoot(_element));
            menu.AddItem(new GUIContent("Wrap in Container"), false, () => _context.WrapSelectionInContainer());
            if (_context.SelectedElements.Count > 1)
                menu.AddItem(new GUIContent("Group"), false, () => _context.GroupSelection());
            else
                menu.AddDisabledItem(new GUIContent("Group"));
            if (DesignerHierarchyUtility.CountChildren(_context.Metadata, _element) > 0)
                menu.AddItem(new GUIContent("Ungroup"), false, () => { _context.SelectMetadata(_element); _context.UngroupSelection(); });
            else
                menu.AddDisabledItem(new GUIContent("Ungroup"));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Duplicate"), false, () => _context.DuplicateSelection());
            menu.AddItem(new GUIContent("Delete (with children)"), false, () => { EnsureSelected(); _context.DeleteSelectedMetadata(true); });
            menu.AddItem(new GUIContent("Delete (keep children)"), false, () => { EnsureSelected(); _context.DeleteSelectedMetadata(false); });
            menu.ShowAsContext();
            evt.StopPropagation();
        }

        private void EnsureSelected()
        {
            if (!_context.IsSelected(_element)) _context.SelectMetadata(_element);
        }

        // ---- drag ----
        private Vector2 _dragStart;
        private bool _dragging;

        private void OnDragStart(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            if (evt.target is TextField || (evt.target as VisualElement)?.parent is TextField) return;
            _dragStart = evt.position;
            _dragging = false;
            this.CapturePointer(evt.pointerId);
        }

        private void OnDragMove(PointerMoveEvent evt)
        {
            if (!this.HasPointerCapture(evt.pointerId)) return;
            if (!_dragging)
            {
                var pointer = new Vector2(evt.position.x, evt.position.y);
                if ((pointer - _dragStart).sqrMagnitude < 36f) return;
                _dragging = true;
                _panel.BeginDrag(_element);
                EnableInClassList("is-dragging", true);
            }
            _panel.UpdateDropTarget(evt.position);
        }

        private void OnDragEnd(PointerUpEvent evt)
        {
            if (!this.HasPointerCapture(evt.pointerId)) return;
            this.ReleasePointer(evt.pointerId);
            EnableInClassList("is-dragging", false);
            if (_dragging)
                _panel.CompleteDrag(evt.position);
        }

        internal void ShowDropIndicator(NexUILayersPanel.DropZone zone, bool legal)
        {
            var color = legal ? new Color(0.25f, 0.55f, 0.95f) : new Color(0.9f, 0.3f, 0.3f);
            if (zone == NexUILayersPanel.DropZone.Into)
            {
                _dropLine.style.display = DisplayStyle.None;
                style.backgroundColor = new Color(color.r, color.g, color.b, legal ? 0.25f : 0.18f);
            }
            else
            {
                style.backgroundColor = StyleKeyword.Null;
                _dropLine.style.display = DisplayStyle.Flex;
                float topPos = zone == NexUILayersPanel.DropZone.Before ? 0f : Mathf.Max(0f, resolvedStyle.height - 2f);
                _dropLine.style.top = topPos;
                _dropLine.style.backgroundColor = color;
            }
        }

        internal void ClearDropIndicator()
        {
            _dropLine.style.display = DisplayStyle.None;
            style.backgroundColor = StyleKeyword.Null;
        }

        private void BeginRename()
        {
            if (_rename != null) return;
            _rename = new TextField { value = LabelFor(_element), tooltip = _name.tooltip };
            _rename.AddToClassList("nexui-layer-name");
            var index = IndexOf(_name);
            _name.RemoveFromHierarchy();
            Insert(index, _rename);
            _rename.RegisterCallback<FocusOutEvent>(_ => CommitRename());
            _rename.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    EndRename(false);
                    evt.StopPropagation();
                    return;
                }
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                CommitRename();
                evt.StopPropagation();
            });
            _rename.schedule.Execute(() =>
            {
                _rename?.Focus();
                _rename?.SelectAll();
            });
        }

        private void CommitRename() => EndRename(true);

        private void EndRename(bool commit)
        {
            if (_rename == null) return;
            var value = _rename.value;
            var index = IndexOf(_rename);
            _rename.RemoveFromHierarchy();
            _rename = null;
            _name.text = LabelFor(_element);
            Insert(index, _name);
            if (commit && !string.Equals(value, LabelFor(_element), StringComparison.Ordinal))
                _context.RenameElement(_element, value);
        }

        private static Button IconButton(string text, string tooltip, Action action)
        {
            var button = new Button(action) { text = text, tooltip = tooltip };
            button.AddToClassList("nexui-layer-icon-button");
            return button;
        }

        private static string LabelFor(DesignerElementMetadata element)
            => string.IsNullOrEmpty(element.displayName) ? element.elementId : element.displayName;
    }
}

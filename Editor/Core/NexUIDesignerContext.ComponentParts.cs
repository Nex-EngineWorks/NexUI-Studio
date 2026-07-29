using System;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Properties;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor
{
    public sealed partial class NexUIDesignerContext
    {
        private string _selectedComponentPartId;
        private DesignerElementMetadata _componentPartDragElement;
        private string _componentPartDragId;
        private bool _componentPartDragChanged;

        /// <summary>The selected library-owned part of the primary element, or null.</summary>
        public string SelectedComponentPartId => _selectedComponentPartId;
        public event Action<string> ComponentPartSelectionChanged;

        public void SelectComponentPart(DesignerElementMetadata element, string partId)
        {
            if (element == null || DesignerComponentRegistry.Get(element.elementType).GetPart(partId) == null)
                partId = null;
            if (element != null && SelectedMetadata != element)
                SelectMetadata(element);
            if (_selectedComponentPartId == partId) return;
            _selectedComponentPartId = partId;
            ComponentPartSelectionChanged?.Invoke(partId);
        }

        public DesignerComponentPartOverrideMetadata GetSelectedComponentPartOverride(bool create = false)
        {
            var element = SelectedMetadata;
            if (element == null || string.IsNullOrEmpty(_selectedComponentPartId)) return null;
            element.componentPartOverrides ??= new System.Collections.Generic.List<DesignerComponentPartOverrideMetadata>();
            return create
                ? DesignerComponentPartOverrideBag.GetOrCreate(element.componentPartOverrides, _selectedComponentPartId)
                : DesignerComponentPartOverrideBag.Find(element.componentPartOverrides, _selectedComponentPartId);
        }

        public void UpdateSelectedComponentPart(Action<DesignerComponentPartOverrideMetadata> change, string undoName)
        {
            if (SelectedMetadata == null || string.IsNullOrEmpty(_selectedComponentPartId) || change == null) return;
            var partId = _selectedComponentPartId;
            UpdateElement(SelectedMetadata, element =>
            {
                element.componentPartOverrides ??= new System.Collections.Generic.List<DesignerComponentPartOverrideMetadata>();
                var value = DesignerComponentPartOverrideBag.GetOrCreate(element.componentPartOverrides, partId);
                change(value);
                DesignerComponentPartOverrideBag.RemoveEmpty(element.componentPartOverrides, partId);
            }, undoName);
        }

        public void ResetSelectedComponentPart()
        {
            var element = SelectedMetadata;
            var partId = _selectedComponentPartId;
            if (element?.componentPartOverrides == null || string.IsNullOrEmpty(partId)) return;
            UpdateElement(element, e =>
            {
                for (var i = e.componentPartOverrides.Count - 1; i >= 0; i--)
                    if (e.componentPartOverrides[i] != null && e.componentPartOverrides[i].partId == partId)
                        e.componentPartOverrides.RemoveAt(i);
            }, "Reset NexUI Component Part");
        }

        public void BeginComponentPartDrag(DesignerElementMetadata element, string partId)
        {
            if (element == null || string.IsNullOrEmpty(partId)) return;
            SelectComponentPart(element, partId);
            if (element.locked) return;
            _componentPartDragElement = element;
            _componentPartDragId = partId;
            _componentPartDragChanged = false;
        }

        public void DragComponentPart(Vector2 delta)
        {
            if (_componentPartDragElement == null || string.IsNullOrEmpty(_componentPartDragId) || delta == Vector2.zero) return;
            if (!_componentPartDragChanged)
                RecordMetadata("Move NexUI Component Part");
            _componentPartDragElement.componentPartOverrides ??= new System.Collections.Generic.List<DesignerComponentPartOverrideMetadata>();
            var value = DesignerComponentPartOverrideBag.GetOrCreate(
                _componentPartDragElement.componentPartOverrides, _componentPartDragId);
            value.hasPosition = true;
            value.position += delta;
            _componentPartDragChanged = true;
            if (Metadata != null) UnityEditor.EditorUtility.SetDirty(Metadata);
            SetDirtyState(true);
        }

        public void EndComponentPartDrag()
        {
            var changed = _componentPartDragElement;
            var didChange = _componentPartDragChanged;
            _componentPartDragElement = null;
            _componentPartDragId = null;
            _componentPartDragChanged = false;
            if (changed != null && didChange)
            {
                MarkMetadataDirty();
                ElementChanged?.Invoke(changed);
            }
            else if (changed != null)
                CanvasChanged?.Invoke();
        }

        /// <summary>
        /// Creates a real authored child inside a component slot. Unlike generated preview rows,
        /// the result is a normal hierarchy element with its own Layout, Style, Motion and bindings.
        /// </summary>
        public DesignerElementMetadata CreateChildElement(DesignerElementMetadata parent, string typeId,
            string slotId = DesignerComponentSlot.Content)
        {
            if (Metadata == null || parent == null || !DesignerComponentRegistry.CanHaveChildren(parent.elementType))
                return null;
            var parentDescriptor = DesignerComponentRegistry.Get(parent.elementType);
            var slot = parentDescriptor.GetSlot(slotId) ?? parentDescriptor.GetSlot(parentDescriptor.DefaultSlotId);
            if (slot == null || slot.IsGeneratedContentSlot) return null;

            var descriptor = DesignerComponentRegistry.Get(typeId);
            if (!slot.Accepts(typeId)) return null;
            RecordMetadata("Add NexUI Component Content");
            var offset = new Vector2(12f, 12f + GetOrderedChildren(parent).Count * 28f);
            var size = Vector2.Min(descriptor.DefaultSize,
                new Vector2(Mathf.Max(24f, parent.rect.width - 24f), Mathf.Max(24f, parent.rect.height - 24f)));
            var child = new DesignerElementMetadata
            {
                elementId = NextElementId(descriptor),
                displayName = descriptor.DisplayName,
                elementType = typeId,
                parentId = parent.elementId,
                parentSlotId = slot.SlotId,
                siblingIndex = GetOrderedChildren(parent).Count,
                rect = new Rect(parent.rect.position + offset, size),
                text = descriptor.DefaultText ?? string.Empty,
                tint = descriptor.DefaultColor,
                shape = descriptor.DefaultShape,
                textColor = Color.white,
                fontSize = descriptor.Category == DesignerComponentCategory.Text ? 18 : 14,
                accessibilityRole = descriptor.DefaultAccessibilityRole
            };
            DesignerPropertyAdapter.SetBackgroundColor(child, child.tint);
            DesignerPropertyAdapter.SetTextColor(child, child.textColor);
            DesignerPropertyAdapter.SetFontSize(child, child.fontSize);
            Metadata.elements.Add(child);
            DesignerHierarchyUtility.NormalizeSiblingIndices(Metadata);
            MarkMetadataDirty();
            SelectMetadata(child);
            return child;
        }
    }
}

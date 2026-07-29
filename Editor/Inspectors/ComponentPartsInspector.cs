using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Inspectors
{
    /// <summary>
    /// Unity-like internal-part and authored-content editor. Library-owned visuals receive sparse
    /// transform overrides; real child content stays in the normal hierarchy and gets every NexUI
    /// inspector, binding and motion capability.
    /// </summary>
    public sealed class ComponentPartsInspector : DesignerInspectorBase
    {
        private readonly VisualElement _host;
        private bool _writing;

        public ComponentPartsInspector(NexUIDesignerContext context) : base(context, "inspector.componentParts")
        {
            _host = new VisualElement();
            Add(_host);
            Subscriptions.Add<DesignerElementMetadata>(
                h => context.MetadataSelectionChanged += h, h => context.MetadataSelectionChanged -= h, _ => Rebuild());
            Subscriptions.Add<string>(
                h => context.ComponentPartSelectionChanged += h, h => context.ComponentPartSelectionChanged -= h, _ => Rebuild());
            Subscriptions.Add<DesignerElementMetadata>(
                h => context.ElementChanged += h, h => context.ElementChanged -= h, _ => { if (!_writing) Rebuild(); });
            Subscriptions.Add(h => context.CanvasChanged += h, h => context.CanvasChanged -= h, () => { if (!_writing) Rebuild(); });
            Rebuild();
        }

        private void Rebuild()
        {
            _host.Clear();
            var element = Context.SelectedMetadata;
            if (element == null) { style.display = DisplayStyle.None; return; }
            var descriptor = DesignerComponentRegistry.Get(element.elementType);
            var hasContents = descriptor.CanHaveChildren || descriptor.Slots.Count > 0;
            if (descriptor.Parts.Count == 0 && !hasContents) { style.display = DisplayStyle.None; return; }
            style.display = DisplayStyle.Flex;

            var summary = new Label(DesignerLocalization.T("inspector.componentParts.description"));
            summary.AddToClassList("nexui-component-part-description");
            _host.Add(summary);

            if (descriptor.Parts.Count > 0)
            {
                var parts = new Foldout
                {
                    text = $"{DesignerLocalization.T("inspector.componentParts.internal")} ({descriptor.Parts.Count})",
                    value = true
                };
                parts.AddToClassList("nexui-component-parts-foldout");
                var strip = new VisualElement();
                strip.AddToClassList("nexui-component-part-strip");
                foreach (var part in descriptor.Parts)
                {
                    var captured = part;
                    var button = new Button(() => Context.SelectComponentPart(element, captured.PartId))
                    {
                        text = captured.DisplayName,
                        tooltip = captured.Description + BackendHint(captured)
                    };
                    button.AddToClassList("nexui-component-part-chip");
                    button.EnableInClassList("is-selected", Context.SelectedComponentPartId == captured.PartId);
                    strip.Add(button);
                }
                parts.Add(strip);

                var selected = descriptor.GetPart(Context.SelectedComponentPartId);
                if (selected != null)
                    parts.Add(BuildTransformEditor(element, selected));
                else
                {
                    var hint = new Label(DesignerLocalization.T("inspector.componentParts.selectHint"));
                    hint.AddToClassList("nexui-component-part-hint");
                    parts.Add(hint);
                }
                _host.Add(parts);
            }

            if (hasContents)
                _host.Add(BuildAuthoredContents(element, descriptor));
        }

        private VisualElement BuildTransformEditor(DesignerElementMetadata element, DesignerComponentPartDescriptor part)
        {
            var card = new VisualElement();
            card.AddToClassList("nexui-component-part-card");
            var header = new Label(part.DisplayName) { tooltip = part.Description };
            header.AddToClassList("nexui-component-part-card-title");
            card.Add(header);
            card.Add(new Label(part.Description) { tooltip = BackendHint(part) });

            var value = DesignerComponentPartOverrideBag.Find(element.componentPartOverrides, part.PartId);
            var position = new Vector2Field("Position") { value = value?.position ?? Vector2.zero,
                tooltip = "Local X/Y offset from the component library default. Drag this part directly on the canvas to edit it." };
            var size = new Vector2Field("Size Delta") { value = value?.sizeDelta ?? Vector2.zero,
                tooltip = "Width/height added to the library default size." };
            var rotation = new FloatField("Rotation") { value = value?.rotation ?? 0f,
                tooltip = "Local clockwise Z rotation in degrees." };
            var scale = new Vector2Field("Scale") { value = value != null && value.hasScale ? value.scale : Vector2.one,
                tooltip = "Local X/Y scale. This composes with the parent element transform." };
            var visible = new Toggle("Visible") { value = value == null || !value.hasVisibility || value.visible,
                tooltip = "Controls this internal part without hiding the whole component." };

            position.RegisterValueChangedCallback(evt => Write(part.PartId, v => { v.hasPosition = true; v.position = evt.newValue; }, "Move NexUI Component Part"));
            size.RegisterValueChangedCallback(evt => Write(part.PartId, v => { v.hasSizeDelta = true; v.sizeDelta = evt.newValue; }, "Resize NexUI Component Part"));
            rotation.RegisterValueChangedCallback(evt => Write(part.PartId, v => { v.hasRotation = true; v.rotation = evt.newValue; }, "Rotate NexUI Component Part"));
            scale.RegisterValueChangedCallback(evt => Write(part.PartId, v => { v.hasScale = true; v.scale = evt.newValue; }, "Scale NexUI Component Part"));
            visible.RegisterValueChangedCallback(evt => Write(part.PartId, v => { v.hasVisibility = true; v.visible = evt.newValue; }, "Toggle NexUI Component Part"));
            card.Add(position);
            card.Add(size);
            card.Add(rotation);
            card.Add(scale);
            card.Add(visible);

            var reset = new Button(() =>
            {
                _writing = true;
                try { Context.ResetSelectedComponentPart(); }
                finally { _writing = false; }
                Rebuild();
            })
            {
                text = DesignerLocalization.T("inspector.componentParts.reset"),
                tooltip = "Remove all overrides and return this part to the library default."
            };
            reset.SetEnabled(value != null && value.HasAnyOverride);
            reset.AddToClassList("nexui-component-part-reset");
            card.Add(reset);
            return card;
        }

        private VisualElement BuildAuthoredContents(DesignerElementMetadata parent, DesignerComponentDescriptor descriptor)
        {
            var children = Context.GetOrderedChildren(parent);
            var foldout = new Foldout
            {
                text = $"{DesignerLocalization.T("inspector.componentParts.contents")} ({children.Count})",
                value = true
            };
            foldout.AddToClassList("nexui-component-contents-foldout");
            var explanation = new Label(DesignerLocalization.T("inspector.componentParts.contentsDescription"));
            explanation.AddToClassList("nexui-component-part-hint");
            foldout.Add(explanation);

            foreach (var child in children)
            {
                var captured = child;
                var row = new Button(() => Context.SelectMetadata(captured))
                {
                    text = $"{captured.displayName}   ·   {captured.elementType}",
                    tooltip = "Select this authored child. Its Position, Size, Rotation, Scale, Style, Binding and Motion appear in the normal Inspector."
                };
                row.AddToClassList("nexui-component-content-row");
                foldout.Add(row);
            }

            var actions = new VisualElement();
            actions.AddToClassList("nexui-component-content-actions");
            var recommendedType = RecommendedChildType(descriptor);
            var addRecommended = new Button(() => Context.CreateChildElement(parent, recommendedType))
            {
                text = "+ " + DesignerComponentRegistry.Get(recommendedType).DisplayName,
                tooltip = "Create a real editable child in this component's Content slot."
            };
            actions.Add(addRecommended);
            if (recommendedType != "Label")
                actions.Add(new Button(() => Context.CreateChildElement(parent, "Label"))
                {
                    text = "+ Text",
                    tooltip = "Add an editable text child."
                });
            foldout.Add(actions);
            return foldout;
        }

        private void Write(string partId, System.Action<DesignerComponentPartOverrideMetadata> change, string undoName)
        {
            if (Context.SelectedComponentPartId != partId) return;
            _writing = true;
            try { Context.UpdateSelectedComponentPart(change, undoName); }
            finally { _writing = false; }
        }

        private static string RecommendedChildType(DesignerComponentDescriptor descriptor)
        {
            if (descriptor.TypeId == "UGUI.ToggleGroup") return "UGUI.Toggle";
            if (descriptor.TypeId == "UITK.RadioButtonGroup") return "UITK.RadioButton";
            if (descriptor.TypeId == "RadioGroup" || descriptor.TypeId == "CheckboxGroup") return "Checkbox";
            return descriptor.Category == DesignerComponentCategory.Container ? "Panel" : "Label";
        }

        private static string BackendHint(DesignerComponentPartDescriptor part)
            => part.PreviewOnly
                ? "\n\nDesigner preview override; backend support is partial."
                : "\n\nuGUI path: " + (string.IsNullOrEmpty(part.UGUIPath) ? "root" : part.UGUIPath);
    }
}

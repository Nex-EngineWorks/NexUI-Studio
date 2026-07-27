using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.Properties;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Inspectors
{
    public sealed class LayoutInspector : DesignerInspectorBase
    {
        private readonly Vector2Field _position;
        private readonly Vector2Field _size;
        private readonly EnumField _anchor;
        private readonly Vector2Field _minSize;
        private readonly Vector2Field _maxSize;
        private readonly Vector2Field _pivot;
        private readonly FloatField _rotation;
        private readonly Vector2Field _scale;
        private readonly Vector4Field _margin;
        private readonly FloatField _aspectRatio;
        private readonly EnumField _wrap;
        private readonly EnumField _align;
        private readonly EnumField _justify;
        private readonly EnumField _overflow;
        private readonly Toggle _locked;
        private bool _refreshing;

        public LayoutInspector(NexUIDesignerContext context) : base(context, "inspector.layout")
        {
            _position = new Vector2Field("Position") { tooltip = DesignerLocalization.T("tooltip.layout.position") };
            _size = new Vector2Field("Size") { tooltip = DesignerLocalization.T("tooltip.layout.size") };
            _anchor = new EnumField("Anchor", DesignerAnchorPreset.TopLeft) { tooltip = DesignerLocalization.T("tooltip.layout.anchor") };
            _minSize = new Vector2Field("Min Size") { tooltip = "0 means no minimum on that axis." };
            _maxSize = new Vector2Field("Max Size") { tooltip = "0 means unbounded on that axis." };
            _pivot = new Vector2Field("Pivot") { tooltip = "Normalized pivot used by uGUI and generated transform origin." };
            _rotation = new FloatField("Rotation") { tooltip = "Clockwise Z rotation in degrees." };
            _scale = new Vector2Field("Scale") { tooltip = "Local X/Y scale." };
            _margin = new Vector4Field("Margin L/T/R/B") { tooltip = "Element outer margin." };
            _aspectRatio = new FloatField("Aspect Ratio") { tooltip = "Width / height. 0 disables aspect enforcement." };
            _wrap = new EnumField("Wrap", DesignerLayoutWrap.NoWrap);
            _align = new EnumField("Align", DesignerLayoutAlignment.Start);
            _justify = new EnumField("Justify", DesignerJustifyContent.Start);
            _overflow = new EnumField("Overflow", DesignerOverflowMode.Visible);
            _locked = new Toggle("Locked") { tooltip = DesignerLocalization.T("tooltip.layout.locked") };
            Add(_position);
            Add(_size);
            Add(_anchor);
            Add(_minSize);
            Add(_maxSize);
            Add(_pivot);
            Add(_rotation);
            Add(_scale);
            Add(_margin);
            Add(_aspectRatio);
            Add(_wrap);
            Add(_align);
            Add(_justify);
            Add(_overflow);
            Add(_locked);

            _position.RegisterValueChangedCallback(evt =>
            {
                if (_refreshing || Context.SelectedMetadata == null) return;
                var r = Context.SelectedMetadata.rect;
                r.position = evt.newValue;
                Context.UpdateSelectedRect(r);
            });
            _size.RegisterValueChangedCallback(evt =>
            {
                if (_refreshing || Context.SelectedMetadata == null) return;
                var r = Context.SelectedMetadata.rect;
                r.size = new Vector2(Mathf.Max(24f, evt.newValue.x), Mathf.Max(24f, evt.newValue.y));
                Context.UpdateSelectedRect(r);
            });
            _anchor.RegisterValueChangedCallback(evt =>
            {
                if (_refreshing || Context.SelectedMetadata == null) return;
                Context.SetSelectedAnchor((DesignerAnchorPreset)evt.newValue);
            });
            _minSize.RegisterValueChangedCallback(evt => ChangeLayout(s => s.minSize = Vector2.Max(Vector2.zero, evt.newValue), "Edit NexUI Min Size"));
            _maxSize.RegisterValueChangedCallback(evt => ChangeLayout(s => s.maxSize = Vector2.Max(Vector2.zero, evt.newValue), "Edit NexUI Max Size"));
            _pivot.RegisterValueChangedCallback(evt => ChangeLayout(s => s.pivot = evt.newValue, "Edit NexUI Pivot"));
            _rotation.RegisterValueChangedCallback(evt => ChangeLayout(s => s.rotation = evt.newValue, "Edit NexUI Rotation"));
            _scale.RegisterValueChangedCallback(evt => ChangeLayout(s => s.scale = evt.newValue, "Edit NexUI Scale"));
            _margin.RegisterValueChangedCallback(evt => ChangeLayout(s =>
            {
                s.marginLeft = evt.newValue.x; s.marginTop = evt.newValue.y;
                s.marginRight = evt.newValue.z; s.marginBottom = evt.newValue.w;
            }, "Edit NexUI Margin"));
            _aspectRatio.RegisterValueChangedCallback(evt => ChangeLayout(s => s.aspectRatio = Mathf.Max(0f, evt.newValue), "Edit NexUI Aspect Ratio"));
            _wrap.RegisterValueChangedCallback(evt => ChangeLayout(s => s.wrap = (DesignerLayoutWrap)evt.newValue, "Edit NexUI Wrap"));
            _align.RegisterValueChangedCallback(evt => ChangeLayout(s => s.align = (DesignerLayoutAlignment)evt.newValue, "Edit NexUI Align"));
            _justify.RegisterValueChangedCallback(evt => ChangeLayout(s => s.justify = (DesignerJustifyContent)evt.newValue, "Edit NexUI Justify"));
            _overflow.RegisterValueChangedCallback(evt =>
            {
                if (_refreshing) return;
                Context.UpdateSelectedElement(e =>
                {
                    var style = DesignerPropertyAdapter.Layout(e);
                    style.hasOverrides = true;
                    style.overflow = (DesignerOverflowMode)evt.newValue;
                    e.clipChildren = style.overflow == DesignerOverflowMode.Hidden;
                }, "Edit NexUI Overflow");
            });
            _locked.RegisterValueChangedCallback(evt =>
            {
                if (_refreshing) return;
                Context.UpdateSelectedElement(e => e.locked = evt.newValue, "Toggle NexUI Element Lock");
            });

            Subscriptions.Add<DesignerElementMetadata>(h => context.MetadataSelectionChanged += h, h => context.MetadataSelectionChanged -= h, _ => Refresh());
            Subscriptions.Add(h => context.CanvasChanged += h, h => context.CanvasChanged -= h, Refresh);
            Refresh();
        }

        private void ChangeLayout(System.Action<DesignerLayoutStyleMetadata> change, string undoName)
        {
            if (_refreshing) return;
            Context.UpdateSelectedElement(e =>
            {
                var style = DesignerPropertyAdapter.Layout(e);
                style.hasOverrides = true;
                change(style);
            }, undoName);
        }

        private void Refresh()
        {
            _refreshing = true;
            var selected = Context.SelectedMetadata;
            SetEnabled(selected != null);
            if (selected != null)
            {
                _position.SetValueWithoutNotify(selected.rect.position);
                _size.SetValueWithoutNotify(selected.rect.size);
                _anchor.SetValueWithoutNotify(selected.anchorPreset);
                var layout = DesignerPropertyAdapter.Layout(selected);
                _minSize.SetValueWithoutNotify(layout.minSize);
                _maxSize.SetValueWithoutNotify(layout.maxSize);
                _pivot.SetValueWithoutNotify(layout.pivot);
                _rotation.SetValueWithoutNotify(layout.rotation);
                _scale.SetValueWithoutNotify(layout.scale);
                _margin.SetValueWithoutNotify(new Vector4(layout.marginLeft, layout.marginTop, layout.marginRight, layout.marginBottom));
                _aspectRatio.SetValueWithoutNotify(layout.aspectRatio);
                _wrap.SetValueWithoutNotify(layout.wrap);
                _align.SetValueWithoutNotify(layout.align);
                _justify.SetValueWithoutNotify(layout.justify);
                _overflow.SetValueWithoutNotify(layout.overflow);
                _locked.SetValueWithoutNotify(selected.locked);
            }
            _refreshing = false;
        }
    }
}

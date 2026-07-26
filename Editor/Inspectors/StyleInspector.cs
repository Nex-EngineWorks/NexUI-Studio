using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Inspectors
{
    public sealed class StyleInspector : DesignerInspectorBase
    {
        private readonly TextField _id;
        private readonly TextField _displayName;
        private readonly EnumField _type;
        private readonly TextField _text;
        private readonly TextField _classes;
        private readonly EnumField _shape;
        private readonly ColorField _tint;
        private readonly ColorField _textColor;
        private readonly IntegerField _fontSize;
        private readonly Toggle _hidden;
        private readonly FloatField _previewValue;
        private readonly FloatField _minValue;
        private readonly FloatField _maxValue;
        private readonly EnumField _fillDirection;
        private readonly Toggle _clockwise;
        private readonly ObjectField _previewImage;
        private bool _refreshing;

        private static readonly System.Collections.Generic.HashSet<string> ValuePreviewTypes = new()
        {
            "ProgressBar", "StatBar", "RadialFill"
        };

        private static readonly System.Collections.Generic.HashSet<string> LinearFillTypes = new()
        {
            "ProgressBar", "StatBar"
        };

        private static readonly System.Collections.Generic.HashSet<string> RadialFillTypes = new()
        {
            "RadialFill", "Spinner"
        };

        private static readonly System.Collections.Generic.HashSet<string> ImageTypes = new()
        {
            "Image", "IconButton"
        };

        public StyleInspector(NexUIDesignerContext context) : base(context, "inspector.style")
        {
            _id = new TextField("Element Id") { tooltip = DesignerLocalization.T("tooltip.style.id") };
            _displayName = new TextField("Name") { tooltip = DesignerLocalization.T("tooltip.style.displayName") };
            _type = new EnumField("Type", DesignerElementType.Panel) { tooltip = DesignerLocalization.T("tooltip.style.type") };
            _text = new TextField("Text") { tooltip = DesignerLocalization.T("tooltip.style.text") };
            _classes = new TextField("Classes") { tooltip = DesignerLocalization.T("tooltip.style.classes") };
            _shape = new EnumField("Shape", DesignerElementShape.Rounded) { tooltip = DesignerLocalization.T("tooltip.style.shape") };
            _tint = new ColorField("Tint") { tooltip = DesignerLocalization.T("tooltip.style.tint") };
            _textColor = new ColorField("Text Color") { tooltip = DesignerLocalization.T("tooltip.style.textColor") };
            _fontSize = new IntegerField("Font Size") { tooltip = DesignerLocalization.T("tooltip.style.fontSize") };
            _hidden = new Toggle("Hidden") { tooltip = DesignerLocalization.T("tooltip.style.hidden") };
            _previewValue = new FloatField("Preview Value") { tooltip = DesignerLocalization.T("tooltip.style.previewValue") };
            _minValue = new FloatField("Min Value") { tooltip = DesignerLocalization.T("tooltip.style.minValue") };
            _maxValue = new FloatField("Max Value") { tooltip = DesignerLocalization.T("tooltip.style.maxValue") };
            _fillDirection = new EnumField("Fill Direction", DesignerFillDirection.LeftToRight) { tooltip = DesignerLocalization.T("tooltip.style.fillDirection") };
            _clockwise = new Toggle("Clockwise") { tooltip = DesignerLocalization.T("tooltip.style.clockwise") };
            _previewImage = new ObjectField("Sprite") { objectType = typeof(Sprite), allowSceneObjects = false, tooltip = DesignerLocalization.T("tooltip.style.previewImage") };

            Add(_id);
            Add(_displayName);
            Add(_type);
            Add(_text);
            Add(_classes);
            Add(_shape);
            Add(_tint);
            Add(_textColor);
            Add(_fontSize);
            Add(_hidden);
            Add(_previewValue);
            Add(_minValue);
            Add(_maxValue);
            Add(_fillDirection);
            Add(_clockwise);
            Add(_previewImage);

            _id.RegisterValueChangedCallback(evt =>
            {
                if (_refreshing) return;
                Context.RenameElementId(Context.SelectedMetadata, evt.newValue);
                Refresh();
            });
            _displayName.RegisterValueChangedCallback(evt => Change(e => e.displayName = evt.newValue, "Rename NexUI Element Display"));
            _type.RegisterValueChangedCallback(evt => Change(e => e.elementType = evt.newValue.ToString(), "Change NexUI Element Type"));
            _text.RegisterValueChangedCallback(evt => Change(e => e.text = evt.newValue, "Edit NexUI Element Text"));
            _classes.RegisterValueChangedCallback(evt => Change(e =>
            {
                e.classes.Clear();
                foreach (var token in evt.newValue.Split(' '))
                    if (!string.IsNullOrWhiteSpace(token))
                        e.classes.Add(token.Trim());
            }, "Edit NexUI Element Classes"));
            _shape.RegisterValueChangedCallback(evt => Change(e => e.shape = (DesignerElementShape)evt.newValue, "Change NexUI Element Shape"));
            _tint.RegisterValueChangedCallback(evt => Change(e => e.tint = evt.newValue, "Edit NexUI Element Tint"));
            _textColor.RegisterValueChangedCallback(evt => Change(e => e.textColor = evt.newValue, "Edit NexUI Element Text Color"));
            _fontSize.RegisterValueChangedCallback(evt => Change(e => e.fontSize = Mathf.Clamp(evt.newValue, 8, 96), "Edit NexUI Element Font Size"));
            _hidden.RegisterValueChangedCallback(evt => Change(e => e.hiddenInDesigner = evt.newValue, "Toggle NexUI Element Hidden"));
            _previewValue.RegisterValueChangedCallback(evt => Change(e => e.previewValue = evt.newValue, "Edit NexUI Element Preview Value"));
            _minValue.RegisterValueChangedCallback(evt => Change(e => e.fill.minValue = evt.newValue, "Edit NexUI Element Min Value"));
            _maxValue.RegisterValueChangedCallback(evt => Change(e => e.fill.maxValue = evt.newValue, "Edit NexUI Element Max Value"));
            _fillDirection.RegisterValueChangedCallback(evt => Change(e => e.fill.direction = (DesignerFillDirection)evt.newValue, "Change NexUI Element Fill Direction"));
            _clockwise.RegisterValueChangedCallback(evt => Change(e => e.fill.clockwise = evt.newValue, "Toggle NexUI Element Fill Clockwise"));
            _previewImage.RegisterValueChangedCallback(evt => Change(e => e.previewImage = evt.newValue as Sprite, "Assign NexUI Element Sprite"));

            Subscriptions.Add<DesignerElementMetadata>(h => context.MetadataSelectionChanged += h, h => context.MetadataSelectionChanged -= h, _ => Refresh());
            Subscriptions.Add(h => context.CanvasChanged += h, h => context.CanvasChanged -= h, Refresh);
            Refresh();
        }

        private void Change(System.Action<DesignerElementMetadata> change, string undoName)
        {
            if (_refreshing) return;
            Context.UpdateSelectedElement(change, undoName);
        }

        private void Refresh()
        {
            _refreshing = true;
            var selected = Context.SelectedMetadata;
            SetEnabled(selected != null);
            if (selected != null)
            {
                _id.SetValueWithoutNotify(selected.elementId);
                _displayName.SetValueWithoutNotify(selected.displayName);
                if (System.Enum.TryParse(selected.elementType, out DesignerElementType parsed))
                    _type.SetValueWithoutNotify(parsed);
                _text.SetValueWithoutNotify(selected.text);
                _classes.SetValueWithoutNotify(string.Join(" ", selected.classes));
                _shape.SetValueWithoutNotify(selected.shape);
                _tint.SetValueWithoutNotify(selected.tint);
                _textColor.SetValueWithoutNotify(selected.textColor);
                _fontSize.SetValueWithoutNotify(selected.fontSize);
                _hidden.SetValueWithoutNotify(selected.hiddenInDesigner);
                _previewValue.SetValueWithoutNotify(selected.previewValue);
                _previewValue.style.display = ValuePreviewTypes.Contains(selected.elementType) ? DisplayStyle.Flex : DisplayStyle.None;

                _minValue.SetValueWithoutNotify(selected.fill.minValue);
                _maxValue.SetValueWithoutNotify(selected.fill.maxValue);
                var showRange = ValuePreviewTypes.Contains(selected.elementType);
                _minValue.style.display = showRange ? DisplayStyle.Flex : DisplayStyle.None;
                _maxValue.style.display = showRange ? DisplayStyle.Flex : DisplayStyle.None;

                _fillDirection.SetValueWithoutNotify(selected.fill.direction);
                _fillDirection.style.display = LinearFillTypes.Contains(selected.elementType) ? DisplayStyle.Flex : DisplayStyle.None;

                _clockwise.SetValueWithoutNotify(selected.fill.clockwise);
                _clockwise.style.display = RadialFillTypes.Contains(selected.elementType) ? DisplayStyle.Flex : DisplayStyle.None;

                _previewImage.SetValueWithoutNotify(selected.previewImage);
                _previewImage.style.display = ImageTypes.Contains(selected.elementType) ? DisplayStyle.Flex : DisplayStyle.None;
            }
            _refreshing = false;
        }
    }
}

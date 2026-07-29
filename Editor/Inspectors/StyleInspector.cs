using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.Properties;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Inspectors
{
    public sealed class StyleInspector : DesignerInspectorBase
    {
        private readonly TextField _id;
        private readonly TextField _displayName;
        private readonly PopupField<string> _type;
        private readonly TextField _text;
        private readonly TextField _classes;
        private readonly EnumField _shape;
        private readonly ColorField _tint;
        private readonly ColorField _textColor;
        private readonly IntegerField _fontSize;
        private readonly Toggle _hidden;
        private readonly Toggle _runtimeVisible;
        private readonly FloatField _previewValue;
        private readonly FloatField _minValue;
        private readonly FloatField _maxValue;
        private readonly EnumField _fillDirection;
        private readonly Toggle _clockwise;
        private readonly ObjectField _previewImage;
        private readonly FloatField _opacity;
        private readonly FloatField _borderWidth;
        private readonly ColorField _borderColor;
        private readonly FloatField _cornerRadius;
        private readonly Toggle _dropShadow;
        private readonly Toggle _innerShadow;
        private readonly Vector2Field _shadowOffset;
        private readonly ColorField _shadowColor;
        private readonly FloatField _blur;
        private readonly FloatField _outlineWidth;
        private readonly ColorField _outlineColor;
        private readonly Toggle _mask;
        private readonly Toggle _imageSlice;
        private readonly EnumField _imageFit;
        private readonly Toggle _crop;
        private readonly GradientField _gradient;
        private readonly ObjectField _material;
        private readonly ObjectField _fontAsset;
        private readonly TextField _fontFamily;
        private readonly EnumField _fontWeight;
        private readonly EnumFlagsField _fontStyle;
        private readonly Toggle _autoFontSize;
        private readonly FloatField _minFontSize;
        private readonly FloatField _maxFontSize;
        private readonly EnumField _textAlignment;
        private readonly Toggle _textWrapping;
        private readonly EnumField _textOverflow;
        private readonly Toggle _ellipsis;
        private readonly FloatField _lineHeight;
        private readonly FloatField _letterSpacing;
        private readonly FloatField _paragraphSpacing;
        private readonly Toggle _richText;
        private readonly TextField _localizationKey;
        private readonly ObjectField _fontFallback;
        private readonly Toggle _rightToLeft;
        private readonly Toggle _textShadow;
        private readonly Vector2Field _textShadowOffset;
        private readonly ColorField _textShadowColor;
        private readonly FloatField _textOutlineWidth;
        private readonly ColorField _textOutlineColor;
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
            var typeChoices = new List<string>();
            foreach (var descriptor in DesignerComponentRegistry.All)
                if (descriptor != null && !descriptor.IsGeneric) typeChoices.Add(descriptor.TypeId);
            _type = new PopupField<string>("Type", typeChoices, "Panel", TypeLabel, TypeLabel)
                { tooltip = DesignerLocalization.T("tooltip.style.type") };
            _text = new TextField("Text") { tooltip = DesignerLocalization.T("tooltip.style.text") };
            _classes = new TextField("Classes") { tooltip = DesignerLocalization.T("tooltip.style.classes") };
            _shape = new EnumField("Shape", DesignerElementShape.Rounded) { tooltip = DesignerLocalization.T("tooltip.style.shape") };
            _tint = new ColorField("Tint") { tooltip = DesignerLocalization.T("tooltip.style.tint") };
            _textColor = new ColorField("Text Color") { tooltip = DesignerLocalization.T("tooltip.style.textColor") };
            _fontSize = new IntegerField("Font Size") { tooltip = DesignerLocalization.T("tooltip.style.fontSize") };
            _hidden = new Toggle("Editor Hidden") { tooltip = "Hide only on the Designer canvas. Backend output is unchanged." };
            _runtimeVisible = new Toggle("Runtime Visible") { tooltip = "Write this element as visible/active in generated backend output." };
            _previewValue = new FloatField("Preview Value") { tooltip = DesignerLocalization.T("tooltip.style.previewValue") };
            _minValue = new FloatField("Min Value") { tooltip = DesignerLocalization.T("tooltip.style.minValue") };
            _maxValue = new FloatField("Max Value") { tooltip = DesignerLocalization.T("tooltip.style.maxValue") };
            _fillDirection = new EnumField("Fill Direction", DesignerFillDirection.LeftToRight) { tooltip = DesignerLocalization.T("tooltip.style.fillDirection") };
            _clockwise = new Toggle("Clockwise") { tooltip = DesignerLocalization.T("tooltip.style.clockwise") };
            _previewImage = new ObjectField("Sprite") { objectType = typeof(Sprite), allowSceneObjects = false, tooltip = DesignerLocalization.T("tooltip.style.previewImage") };
            _opacity = new FloatField("Opacity") { tooltip = "Element opacity from 0 to 1." };
            _borderWidth = new FloatField("Border Width");
            _borderColor = new ColorField("Border Color");
            _cornerRadius = new FloatField("Corner Radius");
            _dropShadow = new Toggle("Drop Shadow");
            _innerShadow = new Toggle("Inner Shadow") { tooltip = "Unsupported by stock backends; preserved and reported." };
            _shadowOffset = new Vector2Field("Shadow Offset");
            _shadowColor = new ColorField("Shadow Color");
            _blur = new FloatField("Blur") { tooltip = "Unsupported by stock backends; preserved and reported." };
            _outlineWidth = new FloatField("Outline Width");
            _outlineColor = new ColorField("Outline Color");
            _mask = new Toggle("Mask / Clip Content");
            _imageSlice = new Toggle("9-slice");
            _imageFit = new EnumField("Image Fit", DesignerImageFit.Contain);
            _crop = new Toggle("Crop");
            _gradient = new GradientField("Gradient") { tooltip = "Requires a backend-specific material/fallback." };
            _material = new ObjectField("Material") { objectType = typeof(Material), allowSceneObjects = false };
            _fontAsset = new ObjectField("Font Asset") { objectType = typeof(UnityEngine.Object), allowSceneObjects = false };
            _fontFamily = new TextField("Font Family");
            _fontWeight = new EnumField("Font Weight", DesignerFontWeight.Regular);
            _fontStyle = new EnumFlagsField("Font Style", DesignerFontStyle.Normal);
            _autoFontSize = new Toggle("Auto Font Size");
            _minFontSize = new FloatField("Min Font Size");
            _maxFontSize = new FloatField("Max Font Size");
            _textAlignment = new EnumField("Text Alignment", DesignerTextAlignment.MiddleCenter);
            _textWrapping = new Toggle("Text Wrapping");
            _textOverflow = new EnumField("Text Overflow", DesignerTextOverflow.Overflow);
            _ellipsis = new Toggle("Ellipsis");
            _lineHeight = new FloatField("Line Height");
            _letterSpacing = new FloatField("Letter Spacing");
            _paragraphSpacing = new FloatField("Paragraph Spacing");
            _richText = new Toggle("Rich Text");
            _localizationKey = new TextField("Localization Key");
            _fontFallback = new ObjectField("Font Fallback") { objectType = typeof(UnityEngine.Object), allowSceneObjects = false };
            _rightToLeft = new Toggle("Right To Left");
            _textShadow = new Toggle("Text Shadow");
            _textShadowOffset = new Vector2Field("Text Shadow Offset");
            _textShadowColor = new ColorField("Text Shadow Color");
            _textOutlineWidth = new FloatField("Text Outline Width");
            _textOutlineColor = new ColorField("Text Outline Color");

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
            Add(_runtimeVisible);
            Add(_previewValue);
            Add(_minValue);
            Add(_maxValue);
            Add(_fillDirection);
            Add(_clockwise);
            Add(_previewImage);
            Add(_opacity);
            Add(_borderWidth);
            Add(_borderColor);
            Add(_cornerRadius);
            Add(_dropShadow);
            Add(_innerShadow);
            Add(_shadowOffset);
            Add(_shadowColor);
            Add(_blur);
            Add(_outlineWidth);
            Add(_outlineColor);
            Add(_mask);
            Add(_imageSlice);
            Add(_imageFit);
            Add(_crop);
            Add(_gradient);
            Add(_material);
            Add(_fontAsset);
            Add(_fontFamily);
            Add(_fontWeight);
            Add(_fontStyle);
            Add(_autoFontSize);
            Add(_minFontSize);
            Add(_maxFontSize);
            Add(_textAlignment);
            Add(_textWrapping);
            Add(_textOverflow);
            Add(_ellipsis);
            Add(_lineHeight);
            Add(_letterSpacing);
            Add(_paragraphSpacing);
            Add(_richText);
            Add(_localizationKey);
            Add(_fontFallback);
            Add(_rightToLeft);
            Add(_textShadow);
            Add(_textShadowOffset);
            Add(_textShadowColor);
            Add(_textOutlineWidth);
            Add(_textOutlineColor);

            _id.RegisterValueChangedCallback(evt =>
            {
                if (_refreshing) return;
                Context.RenameElementId(Context.SelectedMetadata, evt.newValue);
                Refresh();
            });
            _displayName.RegisterValueChangedCallback(evt => Change(e => e.displayName = evt.newValue, "Rename NexUI Element Display"));
            _type.RegisterValueChangedCallback(evt => Change(e => e.elementType = evt.newValue, "Change NexUI Element Type"));
            _text.RegisterValueChangedCallback(evt => Change(e => e.text = evt.newValue, "Edit NexUI Element Text"));
            _classes.RegisterValueChangedCallback(evt => Change(e =>
            {
                e.classes.Clear();
                foreach (var token in evt.newValue.Split(' '))
                    if (!string.IsNullOrWhiteSpace(token))
                        e.classes.Add(token.Trim());
            }, "Edit NexUI Element Classes"));
            _shape.RegisterValueChangedCallback(evt => Change(e => e.shape = (DesignerElementShape)evt.newValue, "Change NexUI Element Shape"));
            _tint.RegisterValueChangedCallback(evt => Change(e => DesignerPropertyAdapter.SetBackgroundColor(e, evt.newValue), "Edit NexUI Element Tint"));
            _textColor.RegisterValueChangedCallback(evt => Change(e => DesignerPropertyAdapter.SetTextColor(e, evt.newValue), "Edit NexUI Element Text Color"));
            _fontSize.RegisterValueChangedCallback(evt => Change(e => DesignerPropertyAdapter.SetFontSize(e, Mathf.Clamp(evt.newValue, 8, 256)), "Edit NexUI Element Font Size"));
            _hidden.RegisterValueChangedCallback(evt => Change(e => e.hiddenInDesigner = evt.newValue, "Toggle NexUI Element Hidden"));
            _runtimeVisible.RegisterValueChangedCallback(evt => Change(e => e.runtimeVisible = evt.newValue, "Toggle NexUI Runtime Visibility"));
            _previewValue.RegisterValueChangedCallback(evt => Change(e => e.previewValue = evt.newValue, "Edit NexUI Element Preview Value"));
            _minValue.RegisterValueChangedCallback(evt => Change(e => e.fill.minValue = evt.newValue, "Edit NexUI Element Min Value"));
            _maxValue.RegisterValueChangedCallback(evt => Change(e => e.fill.maxValue = evt.newValue, "Edit NexUI Element Max Value"));
            _fillDirection.RegisterValueChangedCallback(evt => Change(e => e.fill.direction = (DesignerFillDirection)evt.newValue, "Change NexUI Element Fill Direction"));
            _clockwise.RegisterValueChangedCallback(evt => Change(e => e.fill.clockwise = evt.newValue, "Toggle NexUI Element Fill Clockwise"));
            _previewImage.RegisterValueChangedCallback(evt => Change(e => e.previewImage = evt.newValue as Sprite, "Assign NexUI Element Sprite"));
            _opacity.RegisterValueChangedCallback(evt => ChangeVisual(s => s.opacity = Mathf.Clamp01(evt.newValue), "Edit NexUI Opacity"));
            _borderWidth.RegisterValueChangedCallback(evt => ChangeVisual(s => s.borderWidth = Mathf.Max(0f, evt.newValue), "Edit NexUI Border Width"));
            _borderColor.RegisterValueChangedCallback(evt => ChangeVisual(s => s.borderColor = evt.newValue, "Edit NexUI Border Color"));
            _cornerRadius.RegisterValueChangedCallback(evt => ChangeVisual(s => s.cornerRadius = Mathf.Max(0f, evt.newValue), "Edit NexUI Corner Radius"));
            _dropShadow.RegisterValueChangedCallback(evt => ChangeVisual(s => s.dropShadow = evt.newValue, "Toggle NexUI Drop Shadow"));
            _innerShadow.RegisterValueChangedCallback(evt => ChangeVisual(s => s.innerShadow = evt.newValue, "Toggle NexUI Inner Shadow"));
            _shadowOffset.RegisterValueChangedCallback(evt => ChangeVisual(s => s.shadowOffset = evt.newValue, "Edit NexUI Shadow Offset"));
            _shadowColor.RegisterValueChangedCallback(evt => ChangeVisual(s => s.shadowColor = evt.newValue, "Edit NexUI Shadow Color"));
            _blur.RegisterValueChangedCallback(evt => ChangeVisual(s => s.blur = Mathf.Max(0f, evt.newValue), "Edit NexUI Blur"));
            _outlineWidth.RegisterValueChangedCallback(evt => ChangeVisual(s => s.outlineWidth = Mathf.Max(0f, evt.newValue), "Edit NexUI Outline Width"));
            _outlineColor.RegisterValueChangedCallback(evt => ChangeVisual(s => s.outlineColor = evt.newValue, "Edit NexUI Outline Color"));
            _mask.RegisterValueChangedCallback(evt => ChangeVisual(s => s.mask = evt.newValue, "Toggle NexUI Mask"));
            _imageSlice.RegisterValueChangedCallback(evt => ChangeVisual(s => s.imageSlice = evt.newValue, "Toggle NexUI 9-slice"));
            _imageFit.RegisterValueChangedCallback(evt => ChangeVisual(s => s.imageFit = (DesignerImageFit)evt.newValue, "Edit NexUI Image Fit"));
            _crop.RegisterValueChangedCallback(evt => ChangeVisual(s => s.crop = evt.newValue, "Toggle NexUI Image Crop"));
            _gradient.RegisterValueChangedCallback(evt => ChangeVisual(s => s.gradient = evt.newValue, "Edit NexUI Gradient"));
            _material.RegisterValueChangedCallback(evt => ChangeVisual(s => s.material = evt.newValue as Material, "Assign NexUI Material"));
            _fontAsset.RegisterValueChangedCallback(evt => ChangeTypography(s => s.fontAsset = evt.newValue, "Assign NexUI Font"));
            _fontFamily.RegisterValueChangedCallback(evt => ChangeTypography(s => s.fontFamily = evt.newValue, "Edit NexUI Font Family"));
            _fontWeight.RegisterValueChangedCallback(evt => ChangeTypography(s => s.fontWeight = (DesignerFontWeight)evt.newValue, "Edit NexUI Font Weight"));
            _fontStyle.RegisterValueChangedCallback(evt => ChangeTypography(s => s.fontStyle = (DesignerFontStyle)evt.newValue, "Edit NexUI Font Style"));
            _autoFontSize.RegisterValueChangedCallback(evt => ChangeTypography(s => s.autoSize = evt.newValue, "Toggle NexUI Auto Font Size"));
            _minFontSize.RegisterValueChangedCallback(evt => ChangeTypography(s => s.minFontSize = Mathf.Max(1f, evt.newValue), "Edit NexUI Min Font Size"));
            _maxFontSize.RegisterValueChangedCallback(evt => ChangeTypography(s => s.maxFontSize = Mathf.Max(1f, evt.newValue), "Edit NexUI Max Font Size"));
            _textAlignment.RegisterValueChangedCallback(evt => ChangeTypography(s => s.alignment = (DesignerTextAlignment)evt.newValue, "Edit NexUI Text Alignment"));
            _textWrapping.RegisterValueChangedCallback(evt => ChangeTypography(s => s.wrapping = evt.newValue, "Toggle NexUI Text Wrapping"));
            _textOverflow.RegisterValueChangedCallback(evt => ChangeTypography(s => s.overflow = (DesignerTextOverflow)evt.newValue, "Edit NexUI Text Overflow"));
            _ellipsis.RegisterValueChangedCallback(evt => ChangeTypography(s => s.ellipsis = evt.newValue, "Toggle NexUI Ellipsis"));
            _lineHeight.RegisterValueChangedCallback(evt => ChangeTypography(s => s.lineHeight = Mathf.Max(0f, evt.newValue), "Edit NexUI Line Height"));
            _letterSpacing.RegisterValueChangedCallback(evt => ChangeTypography(s => s.letterSpacing = evt.newValue, "Edit NexUI Letter Spacing"));
            _paragraphSpacing.RegisterValueChangedCallback(evt => ChangeTypography(s => s.paragraphSpacing = evt.newValue, "Edit NexUI Paragraph Spacing"));
            _richText.RegisterValueChangedCallback(evt => ChangeTypography(s => s.richText = evt.newValue, "Toggle NexUI Rich Text"));
            _localizationKey.RegisterValueChangedCallback(evt => ChangeTypography(s => s.localizationKey = evt.newValue, "Edit NexUI Localization Key"));
            _fontFallback.RegisterValueChangedCallback(evt => ChangeTypography(s => s.fontFallback = evt.newValue, "Assign NexUI Font Fallback"));
            _rightToLeft.RegisterValueChangedCallback(evt => ChangeTypography(s => s.rightToLeft = evt.newValue, "Toggle NexUI RTL"));
            _textShadow.RegisterValueChangedCallback(evt => ChangeTypography(s => s.textShadow = evt.newValue, "Toggle NexUI Text Shadow"));
            _textShadowOffset.RegisterValueChangedCallback(evt => ChangeTypography(s => s.shadowOffset = evt.newValue, "Edit NexUI Text Shadow Offset"));
            _textShadowColor.RegisterValueChangedCallback(evt => ChangeTypography(s => s.shadowColor = evt.newValue, "Edit NexUI Text Shadow Color"));
            _textOutlineWidth.RegisterValueChangedCallback(evt => ChangeTypography(s => s.outlineWidth = Mathf.Max(0f, evt.newValue), "Edit NexUI Text Outline Width"));
            _textOutlineColor.RegisterValueChangedCallback(evt => ChangeTypography(s => s.outlineColor = evt.newValue, "Edit NexUI Text Outline Color"));

            Subscriptions.Add<DesignerElementMetadata>(h => context.MetadataSelectionChanged += h, h => context.MetadataSelectionChanged -= h, _ => Refresh());
            Subscriptions.Add(h => context.CanvasChanged += h, h => context.CanvasChanged -= h, Refresh);
            Refresh();
        }

        private void Change(System.Action<DesignerElementMetadata> change, string undoName)
        {
            if (_refreshing) return;
            Context.UpdateSelectedElement(change, undoName);
        }

        private void ChangeVisual(System.Action<DesignerVisualStyleMetadata> change, string undoName)
            => Change(e => { var style = DesignerPropertyAdapter.Visual(e); style.hasOverrides = true; change(style); }, undoName);

        private void ChangeTypography(System.Action<DesignerTypographyMetadata> change, string undoName)
            => Change(e => { var style = DesignerPropertyAdapter.Typography(e); style.hasOverrides = true; change(style); }, undoName);

        private void Refresh()
        {
            _refreshing = true;
            var selected = Context.SelectedMetadata;
            SetEnabled(selected != null);
            if (selected != null)
            {
                _id.SetValueWithoutNotify(selected.elementId);
                _displayName.SetValueWithoutNotify(selected.displayName);
                if (!_type.choices.Contains(selected.elementType)) _type.choices.Add(selected.elementType);
                _type.SetValueWithoutNotify(selected.elementType);
                _text.SetValueWithoutNotify(selected.text);
                _classes.SetValueWithoutNotify(string.Join(" ", selected.classes));
                _shape.SetValueWithoutNotify(selected.shape);
                _tint.SetValueWithoutNotify(DesignerPropertyAdapter.BackgroundColor(selected));
                _textColor.SetValueWithoutNotify(DesignerPropertyAdapter.TextColor(selected));
                _fontSize.SetValueWithoutNotify(Mathf.RoundToInt(DesignerPropertyAdapter.FontSize(selected)));
                _hidden.SetValueWithoutNotify(selected.hiddenInDesigner);
                _runtimeVisible.SetValueWithoutNotify(selected.runtimeVisible);
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
                var visual = DesignerPropertyAdapter.Visual(selected);
                _opacity.SetValueWithoutNotify(DesignerPropertyAdapter.Opacity(selected));
                _borderWidth.SetValueWithoutNotify(visual.borderWidth);
                _borderColor.SetValueWithoutNotify(visual.borderColor);
                _cornerRadius.SetValueWithoutNotify(DesignerPropertyAdapter.CornerRadius(selected));
                _dropShadow.SetValueWithoutNotify(visual.dropShadow);
                _innerShadow.SetValueWithoutNotify(visual.innerShadow);
                _shadowOffset.SetValueWithoutNotify(visual.shadowOffset);
                _shadowColor.SetValueWithoutNotify(visual.shadowColor);
                _blur.SetValueWithoutNotify(visual.blur);
                _outlineWidth.SetValueWithoutNotify(visual.outlineWidth);
                _outlineColor.SetValueWithoutNotify(visual.outlineColor);
                _mask.SetValueWithoutNotify(visual.mask);
                _imageSlice.SetValueWithoutNotify(visual.imageSlice);
                _imageFit.SetValueWithoutNotify(visual.imageFit);
                _crop.SetValueWithoutNotify(visual.crop);
                _gradient.SetValueWithoutNotify(visual.gradient);
                _material.SetValueWithoutNotify(visual.material);
                var typography = DesignerPropertyAdapter.Typography(selected);
                _fontAsset.SetValueWithoutNotify(typography.fontAsset);
                _fontFamily.SetValueWithoutNotify(typography.fontFamily);
                _fontWeight.SetValueWithoutNotify(typography.fontWeight);
                _fontStyle.SetValueWithoutNotify(typography.fontStyle);
                _autoFontSize.SetValueWithoutNotify(typography.autoSize);
                _minFontSize.SetValueWithoutNotify(typography.minFontSize);
                _maxFontSize.SetValueWithoutNotify(typography.maxFontSize);
                _textAlignment.SetValueWithoutNotify(typography.alignment);
                _textWrapping.SetValueWithoutNotify(typography.wrapping);
                _textOverflow.SetValueWithoutNotify(typography.overflow);
                _ellipsis.SetValueWithoutNotify(typography.ellipsis);
                _lineHeight.SetValueWithoutNotify(typography.lineHeight);
                _letterSpacing.SetValueWithoutNotify(typography.letterSpacing);
                _paragraphSpacing.SetValueWithoutNotify(typography.paragraphSpacing);
                _richText.SetValueWithoutNotify(typography.richText);
                _localizationKey.SetValueWithoutNotify(typography.localizationKey);
                _fontFallback.SetValueWithoutNotify(typography.fontFallback);
                _rightToLeft.SetValueWithoutNotify(typography.rightToLeft);
                _textShadow.SetValueWithoutNotify(typography.textShadow);
                _textShadowOffset.SetValueWithoutNotify(typography.shadowOffset);
                _textShadowColor.SetValueWithoutNotify(typography.shadowColor);
                _textOutlineWidth.SetValueWithoutNotify(typography.outlineWidth);
                _textOutlineColor.SetValueWithoutNotify(typography.outlineColor);
            }
            _refreshing = false;
        }

        private static string TypeLabel(string typeId)
        {
            var descriptor = DesignerComponentRegistry.Get(typeId);
            var name = DesignerComponentPalette.DisplayName(descriptor);
            return descriptor.Family == DesignerComponentFamily.NexUI
                ? name
                : $"{name} ({descriptor.Family})";
        }
    }
}

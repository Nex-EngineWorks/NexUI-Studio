using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Properties
{
    /// <summary>
    /// Writes a typed <see cref="DesignerPropertyId"/>/<see cref="DesignerPropertyValue"/> pair onto a
    /// <see cref="DesignerElementMetadata"/>, and reads it back.
    ///
    /// This is the missing half of the Phase 1 typed-property model: the registry could describe,
    /// parse and serialize a property, but nothing could <i>apply</i> one. Component instance
    /// overrides and variant rules need exactly that, and so will responsive/variant preview later.
    ///
    /// Deliberately pure (no Undo, no dirty, no AssetDatabase) so it is unit-testable and safe to run
    /// against the throw-away expanded metadata the expander produces. Callers that apply to authored
    /// data are responsible for Undo.
    /// </summary>
    public static class DesignerPropertyApplier
    {
        /// <summary>
        /// Applies <paramref name="value"/> to <paramref name="element"/>. Returns false when the id
        /// is unknown/None, the element is null, or the property has no authored representation in
        /// metadata (in which case nothing is written - the caller reports it, never guesses).
        /// </summary>
        public static bool Apply(DesignerElementMetadata element, DesignerPropertyId id, DesignerPropertyValue value)
        {
            if (element == null || value == null || id == DesignerPropertyId.None) return false;

            switch (id)
            {
                // ---- Layout ---------------------------------------------------------------
                case DesignerPropertyId.Position:
                    element.rect.position = value.vector2Value;
                    return true;
                case DesignerPropertyId.Width:
                    element.rect.width = Mathf.Max(0f, value.floatValue);
                    return true;
                case DesignerPropertyId.Height:
                    element.rect.height = Mathf.Max(0f, value.floatValue);
                    return true;
                case DesignerPropertyId.MinWidth:
                    Layout(element).minSize = new Vector2(value.floatValue, Layout(element).minSize.y);
                    return true;
                case DesignerPropertyId.MinHeight:
                    Layout(element).minSize = new Vector2(Layout(element).minSize.x, value.floatValue);
                    return true;
                case DesignerPropertyId.MaxWidth:
                    Layout(element).maxSize = new Vector2(value.floatValue, Layout(element).maxSize.y);
                    return true;
                case DesignerPropertyId.MaxHeight:
                    Layout(element).maxSize = new Vector2(Layout(element).maxSize.x, value.floatValue);
                    return true;
                case DesignerPropertyId.Anchor:
                    element.anchorPreset = (DesignerAnchorPreset)value.intValue;
                    return true;
                case DesignerPropertyId.Pivot:
                    Layout(element).pivot = value.vector2Value;
                    return true;
                case DesignerPropertyId.Rotation:
                    Layout(element).rotation = value.floatValue;
                    return true;
                case DesignerPropertyId.Scale:
                    Layout(element).scale = value.vector2Value;
                    return true;
                case DesignerPropertyId.MarginLeft:   Layout(element).marginLeft = value.floatValue; return true;
                case DesignerPropertyId.MarginTop:    Layout(element).marginTop = value.floatValue; return true;
                case DesignerPropertyId.MarginRight:  Layout(element).marginRight = value.floatValue; return true;
                case DesignerPropertyId.MarginBottom: Layout(element).marginBottom = value.floatValue; return true;
                case DesignerPropertyId.AspectRatio:
                    Layout(element).aspectRatio = value.floatValue; return true;
                case DesignerPropertyId.Wrap:
                    Layout(element).wrap = (DesignerLayoutWrap)value.intValue; return true;
                case DesignerPropertyId.Align:
                    Layout(element).align = (DesignerLayoutAlignment)value.intValue; return true;
                case DesignerPropertyId.Justify:
                    Layout(element).justify = (DesignerJustifyContent)value.intValue; return true;
                case DesignerPropertyId.ChildOrder:
                    element.siblingIndex = value.intValue; return true;
                case DesignerPropertyId.Overflow:
                    Layout(element).overflow = (DesignerOverflowMode)value.intValue; return true;
                case DesignerPropertyId.Clip:
                    element.clipChildren = value.boolValue;
                    Layout(element).overflow = value.boolValue ? DesignerOverflowMode.Hidden : DesignerOverflowMode.Visible;
                    return true;

                // ---- Visual ---------------------------------------------------------------
                case DesignerPropertyId.BackgroundColor:
                case DesignerPropertyId.Tint:
                    DesignerPropertyAdapter.SetBackgroundColor(element, value.colorValue);
                    return true;
                case DesignerPropertyId.Sprite:
                    element.previewImage = value.assetValue as Sprite;
                    return true;
                case DesignerPropertyId.Opacity:
                    Visual(element).opacity = Mathf.Clamp01(value.floatValue); return true;
                case DesignerPropertyId.BorderWidth:
                    Visual(element).borderWidth = Mathf.Max(0f, value.floatValue); return true;
                case DesignerPropertyId.BorderColor:
                    Visual(element).borderColor = value.colorValue; return true;
                case DesignerPropertyId.CornerRadius:
                    Visual(element).cornerRadius = Mathf.Max(0f, value.floatValue); return true;
                case DesignerPropertyId.DropShadow:
                    Visual(element).dropShadow = value.boolValue; return true;
                case DesignerPropertyId.InnerShadow:
                    Visual(element).innerShadow = value.boolValue; return true;
                case DesignerPropertyId.OutlineWidth:
                    Visual(element).outlineWidth = Mathf.Max(0f, value.floatValue); return true;
                case DesignerPropertyId.OutlineColor:
                    Visual(element).outlineColor = value.colorValue; return true;
                case DesignerPropertyId.Blur:
                    Visual(element).blur = Mathf.Max(0f, value.floatValue); return true;
                case DesignerPropertyId.Mask:
                    Visual(element).mask = value.boolValue; return true;
                case DesignerPropertyId.Material:
                    Visual(element).material = value.assetValue as Material; return true;
                case DesignerPropertyId.ImageSlice:
                    Visual(element).imageSlice = value.boolValue; return true;
                case DesignerPropertyId.ImageFit:
                    Visual(element).imageFit = (DesignerImageFit)value.intValue; return true;
                case DesignerPropertyId.ImageFill:
                    element.previewValue = value.floatValue; return true;
                case DesignerPropertyId.Crop:
                    Visual(element).crop = value.boolValue; return true;

                // ---- Text -----------------------------------------------------------------
                case DesignerPropertyId.Text:
                    element.text = value.stringValue; return true;
                case DesignerPropertyId.FontAsset:
                    Typography(element).fontAsset = value.assetValue; return true;
                case DesignerPropertyId.FontFamily:
                    Typography(element).fontFamily = value.stringValue; return true;
                case DesignerPropertyId.FontWeight:
                    Typography(element).fontWeight = (DesignerFontWeight)value.intValue; return true;
                case DesignerPropertyId.FontStyle:
                    Typography(element).fontStyle = (DesignerFontStyle)value.intValue; return true;
                case DesignerPropertyId.FontSize:
                    DesignerPropertyAdapter.SetFontSize(element, value.floatValue); return true;
                case DesignerPropertyId.AutoFontSize:
                    Typography(element).autoSize = value.boolValue; return true;
                case DesignerPropertyId.TextAlignment:
                    Typography(element).alignment = (DesignerTextAlignment)value.intValue; return true;
                case DesignerPropertyId.TextWrapping:
                    Typography(element).wrapping = value.boolValue; return true;
                case DesignerPropertyId.TextOverflow:
                    Typography(element).overflow = (DesignerTextOverflow)value.intValue; return true;
                case DesignerPropertyId.Ellipsis:
                    Typography(element).ellipsis = value.boolValue; return true;
                case DesignerPropertyId.LineHeight:
                    Typography(element).lineHeight = value.floatValue; return true;
                case DesignerPropertyId.LetterSpacing:
                    Typography(element).letterSpacing = value.floatValue; return true;
                case DesignerPropertyId.ParagraphSpacing:
                    Typography(element).paragraphSpacing = value.floatValue; return true;
                case DesignerPropertyId.RichText:
                    Typography(element).richText = value.boolValue; return true;
                case DesignerPropertyId.LocalizationKey:
                    Typography(element).localizationKey = value.stringValue; return true;
                case DesignerPropertyId.FontFallback:
                    Typography(element).fontFallback = value.assetValue; return true;
                case DesignerPropertyId.RightToLeft:
                    Typography(element).rightToLeft = value.boolValue; return true;
                case DesignerPropertyId.TextColor:
                    DesignerPropertyAdapter.SetTextColor(element, value.colorValue); return true;
                case DesignerPropertyId.TextShadow:
                    Typography(element).textShadow = value.boolValue; return true;
                case DesignerPropertyId.TextOutline:
                    Typography(element).outlineWidth = value.boolValue ? Mathf.Max(1f, Typography(element).outlineWidth) : 0f;
                    return true;

                // ---- Behaviour ------------------------------------------------------------
                case DesignerPropertyId.RuntimeVisible:
                    element.runtimeVisible = value.boolValue; return true;

                // ---- Motion ---------------------------------------------------------------
                // A variant is a name inside the preset, so switching preset and switching variant
                // are separate overrides: a rule that only swaps the hover animation must not have
                // to restate which preset the element uses.
                case DesignerPropertyId.MotionPreset:
                    Motion(element).motionPreset = value.assetValue as emiteat.NexUI.Motion.UIMotionPreset; return true;
                case DesignerPropertyId.MotionId:
                    Motion(element).motionId = value.stringValue; return true;
                case DesignerPropertyId.MotionInitialVariant:
                    Motion(element).initialVariant = value.stringValue; return true;
                case DesignerPropertyId.MotionAnimateVariant:
                    Motion(element).animateVariant = value.stringValue; return true;
                case DesignerPropertyId.MotionExitVariant:
                    Motion(element).exitVariant = value.stringValue; return true;
                case DesignerPropertyId.MotionHoverVariant:
                    Motion(element).hoverVariant = value.stringValue; return true;
                case DesignerPropertyId.MotionPressedVariant:
                    Motion(element).pressedVariant = value.stringValue; return true;
                case DesignerPropertyId.MotionFocusVariant:
                    Motion(element).focusVariant = value.stringValue; return true;

                // ---- Theme ----------------------------------------------------------------
                case DesignerPropertyId.ThemeAsset:
                    Theme(element).themeRef = value.assetValue as emiteat.NexUI.Theme.UITheme; return true;
                case DesignerPropertyId.ThemeId:
                    Theme(element).themeId = value.stringValue; return true;
                case DesignerPropertyId.ThemeClasses:
                    Theme(element).classes = DesignerThemeValueCodec.ParseClasses(value.stringValue); return true;
                case DesignerPropertyId.ThemeTokens:
                    Theme(element).tokenOverrides = DesignerThemeValueCodec.ParseTokens(value.stringValue); return true;

                // Auto Layout lives on its own metadata block; Texture/Gradient have no authored
                // representation at all and fall through to false (reported, never guessed).
                default:
                    return ApplyAutoLayout(element, id, value);
            }
        }

        /// <summary>
        /// Reads the current value of <paramref name="id"/> from <paramref name="element"/> so the
        /// Inspector can show "instance value vs definition default" and Reset can restore it.
        /// Returns null when the property has no authored representation.
        /// </summary>
        public static DesignerPropertyValue Read(DesignerElementMetadata element, DesignerPropertyId id)
        {
            if (element == null || id == DesignerPropertyId.None) return null;
            switch (id)
            {
                case DesignerPropertyId.Position: return V2(element.rect.position);
                case DesignerPropertyId.Width: return F(element.rect.width);
                case DesignerPropertyId.Height: return F(element.rect.height);
                case DesignerPropertyId.Anchor: return I((int)element.anchorPreset);
                case DesignerPropertyId.BackgroundColor:
                case DesignerPropertyId.Tint: return C(DesignerPropertyAdapter.BackgroundColor(element));
                case DesignerPropertyId.Opacity: return F(DesignerPropertyAdapter.Opacity(element));
                case DesignerPropertyId.CornerRadius: return F(DesignerPropertyAdapter.CornerRadius(element));
                case DesignerPropertyId.Text: return S(element.text);
                case DesignerPropertyId.TextColor: return C(DesignerPropertyAdapter.TextColor(element));
                case DesignerPropertyId.FontSize: return F(DesignerPropertyAdapter.FontSize(element));
                case DesignerPropertyId.RuntimeVisible: return B(element.runtimeVisible);
                case DesignerPropertyId.Clip: return B(DesignerPropertyAdapter.Clip(element));
                case DesignerPropertyId.Sprite:
                    return new DesignerPropertyValue { type = DesignerPropertyValueType.AssetReference, assetValue = element.previewImage };
                case DesignerPropertyId.ChildOrder: return I(element.siblingIndex);
                case DesignerPropertyId.ImageFill: return F(element.previewValue);

                case DesignerPropertyId.MotionPreset: return A(Motion(element).motionPreset);
                case DesignerPropertyId.MotionId: return S(Motion(element).motionId);
                case DesignerPropertyId.MotionInitialVariant: return S(Motion(element).initialVariant);
                case DesignerPropertyId.MotionAnimateVariant: return S(Motion(element).animateVariant);
                case DesignerPropertyId.MotionExitVariant: return S(Motion(element).exitVariant);
                case DesignerPropertyId.MotionHoverVariant: return S(Motion(element).hoverVariant);
                case DesignerPropertyId.MotionPressedVariant: return S(Motion(element).pressedVariant);
                case DesignerPropertyId.MotionFocusVariant: return S(Motion(element).focusVariant);

                case DesignerPropertyId.ThemeAsset: return A(Theme(element).themeRef);
                case DesignerPropertyId.ThemeId: return S(Theme(element).themeId);
                case DesignerPropertyId.ThemeClasses: return S(DesignerThemeValueCodec.FormatClasses(Theme(element).classes));
                case DesignerPropertyId.ThemeTokens: return S(DesignerThemeValueCodec.FormatTokens(Theme(element).tokenOverrides));

                default: return null;
            }
        }

        private static DesignerMotionMetadata Motion(DesignerElementMetadata element)
            => element.motion ??= new DesignerMotionMetadata();

        private static DesignerThemeMetadata Theme(DesignerElementMetadata element)
            => element.theme ??= new DesignerThemeMetadata();

        private static DesignerPropertyValue A(UnityEngine.Object asset)
            => new DesignerPropertyValue { type = DesignerPropertyValueType.AssetReference, assetValue = asset };

        /// <summary>
        /// Auto-layout properties live on <see cref="DesignerAutoLayoutMetadata"/> rather than the
        /// typed style blocks. Kept separate so the main switch stays about the element's own fields.
        /// </summary>
        private static bool ApplyAutoLayout(DesignerElementMetadata element, DesignerPropertyId id, DesignerPropertyValue value)
        {
            var layout = element.autoLayout ??= new DesignerAutoLayoutMetadata();
            switch (id)
            {
                case DesignerPropertyId.Gap:           layout.spacing = value.floatValue; return true;
                case DesignerPropertyId.PaddingLeft:   layout.paddingLeft = value.floatValue; return true;
                case DesignerPropertyId.PaddingTop:    layout.paddingTop = value.floatValue; return true;
                case DesignerPropertyId.PaddingRight:  layout.paddingRight = value.floatValue; return true;
                case DesignerPropertyId.PaddingBottom: layout.paddingBottom = value.floatValue; return true;
                case DesignerPropertyId.LayoutDirection:
                    layout.direction = (DesignerAutoLayoutDirection)value.intValue;
                    return true;
                case DesignerPropertyId.WidthSizing:
                    layout.widthSizing = (DesignerAutoLayoutSizing)value.intValue;
                    return true;
                case DesignerPropertyId.HeightSizing:
                    layout.heightSizing = (DesignerAutoLayoutSizing)value.intValue;
                    return true;
                default:
                    return false;
            }
        }

        private static DesignerLayoutStyleMetadata Layout(DesignerElementMetadata e)
        {
            var l = DesignerPropertyAdapter.Layout(e);
            l.hasOverrides = true;
            return l;
        }

        private static DesignerVisualStyleMetadata Visual(DesignerElementMetadata e)
        {
            var v = DesignerPropertyAdapter.Visual(e);
            v.hasOverrides = true;
            return v;
        }

        private static DesignerTypographyMetadata Typography(DesignerElementMetadata e)
        {
            var t = DesignerPropertyAdapter.Typography(e);
            t.hasOverrides = true;
            return t;
        }

        private static DesignerPropertyValue F(float v) => new DesignerPropertyValue { type = DesignerPropertyValueType.Float, floatValue = v };
        private static DesignerPropertyValue I(int v) => new DesignerPropertyValue { type = DesignerPropertyValueType.Integer, intValue = v };
        private static DesignerPropertyValue B(bool v) => new DesignerPropertyValue { type = DesignerPropertyValueType.Boolean, boolValue = v };
        private static DesignerPropertyValue S(string v) => new DesignerPropertyValue { type = DesignerPropertyValueType.String, stringValue = v };
        private static DesignerPropertyValue C(Color v) => new DesignerPropertyValue { type = DesignerPropertyValueType.Color, colorValue = v };
        private static DesignerPropertyValue V2(Vector2 v) => new DesignerPropertyValue { type = DesignerPropertyValueType.Vector2, vector2Value = v };
    }
}

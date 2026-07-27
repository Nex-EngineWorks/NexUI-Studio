using System;
using UnityEngine;

namespace emiteat.NexUI.Designer
{
    public enum DesignerPropertyId
    {
        None = 0,
        Position,
        Width,
        Height,
        MinWidth,
        MinHeight,
        MaxWidth,
        MaxHeight,
        Anchor,
        Pivot,
        Rotation,
        Scale,
        MarginLeft,
        MarginTop,
        MarginRight,
        MarginBottom,
        PaddingLeft,
        PaddingTop,
        PaddingRight,
        PaddingBottom,
        Gap,
        AspectRatio,
        WidthSizing,
        HeightSizing,
        LayoutDirection,
        Wrap,
        Align,
        Justify,
        ChildOrder,
        Overflow,
        Clip,
        BackgroundColor,
        Sprite,
        Texture,
        Gradient,
        BorderWidth,
        BorderColor,
        CornerRadius,
        DropShadow,
        InnerShadow,
        OutlineWidth,
        OutlineColor,
        Opacity,
        Blur,
        Mask,
        Material,
        ImageSlice,
        ImageFit,
        ImageFill,
        Crop,
        Tint,
        Text,
        FontAsset,
        FontFamily,
        FontWeight,
        FontStyle,
        FontSize,
        AutoFontSize,
        TextAlignment,
        TextWrapping,
        TextOverflow,
        Ellipsis,
        LineHeight,
        LetterSpacing,
        ParagraphSpacing,
        RichText,
        LocalizationKey,
        FontFallback,
        RightToLeft,
        TextColor,
        TextShadow,
        TextOutline,
        RuntimeVisible
    }

    public enum DesignerPropertyValueType { None, Float, Integer, Boolean, String, Color, Vector2, AssetReference, Enum }
    public enum DesignerOverflowMode { Visible, Hidden }
    public enum DesignerLayoutWrap { NoWrap, Wrap }
    public enum DesignerLayoutAlignment { Start, Center, End, Stretch }
    public enum DesignerJustifyContent { Start, Center, End, SpaceBetween, SpaceAround }
    public enum DesignerImageFit { Stretch, Contain, Cover, Original }
    public enum DesignerFontWeight { Thin, Light, Regular, Medium, SemiBold, Bold, Black }
    [Flags] public enum DesignerFontStyle { Normal = 0, Bold = 1, Italic = 2, Underline = 4, Strikethrough = 8 }
    public enum DesignerTextAlignment { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum DesignerTextOverflow { Overflow, Clip, Ellipsis, Truncate }

    [Serializable]
    public sealed class DesignerPropertyValue
    {
        public DesignerPropertyValueType type;
        public float floatValue;
        public int intValue;
        public bool boolValue;
        public string stringValue;
        public Color colorValue = Color.white;
        public Vector2 vector2Value;
        public UnityEngine.Object assetValue;

        public DesignerPropertyValue Clone() => JsonUtility.FromJson<DesignerPropertyValue>(JsonUtility.ToJson(this));
    }

    [Serializable]
    public sealed class DesignerLayoutStyleMetadata
    {
        public bool hasOverrides;
        public Vector2 minSize;
        public Vector2 maxSize;
        public Vector2 pivot = new Vector2(0f, 1f);
        public float rotation;
        public Vector2 scale = Vector2.one;
        public float marginLeft;
        public float marginTop;
        public float marginRight;
        public float marginBottom;
        public float aspectRatio;
        public DesignerLayoutWrap wrap;
        public DesignerLayoutAlignment align = DesignerLayoutAlignment.Start;
        public DesignerJustifyContent justify = DesignerJustifyContent.Start;
        public DesignerOverflowMode overflow = DesignerOverflowMode.Visible;
    }

    [Serializable]
    public sealed class DesignerVisualStyleMetadata
    {
        public bool hasOverrides;
        public Color backgroundColor = new Color(0.15f, 0.22f, 0.34f, 1f);
        [Range(0f, 1f)] public float opacity = 1f;
        public float borderWidth;
        public Color borderColor = Color.clear;
        public float cornerRadius = 8f;
        public bool dropShadow;
        public Color shadowColor = new Color(0f, 0f, 0f, 0.35f);
        public Vector2 shadowOffset = new Vector2(0f, 2f);
        public float shadowBlur = 4f;
        public bool innerShadow;
        public float outlineWidth;
        public Color outlineColor = Color.clear;
        public float blur;
        public bool mask;
        public Material material;
        public bool imageSlice;
        public DesignerImageFit imageFit = DesignerImageFit.Contain;
        public bool crop;
        public Gradient gradient;
    }

    [Serializable]
    public sealed class DesignerTypographyMetadata
    {
        public bool hasOverrides;
        public UnityEngine.Object fontAsset;
        public string fontFamily;
        public DesignerFontWeight fontWeight = DesignerFontWeight.Regular;
        public DesignerFontStyle fontStyle = DesignerFontStyle.Normal;
        public float fontSize = 14f;
        public bool autoSize;
        public float minFontSize = 8f;
        public float maxFontSize = 72f;
        public DesignerTextAlignment alignment = DesignerTextAlignment.MiddleCenter;
        public bool wrapping = true;
        public DesignerTextOverflow overflow = DesignerTextOverflow.Overflow;
        public bool ellipsis;
        public float lineHeight = 1.2f;
        public float letterSpacing;
        public float paragraphSpacing;
        public bool richText = true;
        public string localizationKey;
        public UnityEngine.Object fontFallback;
        public bool rightToLeft;
        public Color color = Color.white;
        public bool textShadow;
        public Color shadowColor = new Color(0f, 0f, 0f, 0.5f);
        public Vector2 shadowOffset = new Vector2(1f, -1f);
        public float outlineWidth;
        public Color outlineColor = Color.black;
    }
}

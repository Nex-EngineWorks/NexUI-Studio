using System;
using System.Collections.Generic;
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
        RuntimeVisible,

        // Appended, never reordered: the numeric value is what every serialized override stores.
        // ---- Motion ---------------------------------------------------------------------------
        MotionPreset,
        MotionId,
        MotionInitialVariant,
        MotionAnimateVariant,
        MotionExitVariant,
        MotionHoverVariant,
        MotionPressedVariant,
        MotionFocusVariant,

        // ---- Theme ----------------------------------------------------------------------------
        ThemeAsset,
        ThemeId,

        /// <summary>Space-separated class list, the way USS and HTML write one.</summary>
        ThemeClasses,

        /// <summary>
        /// Token overrides as <c>key=value</c> pairs separated by <c>;</c>.
        /// </summary>
        /// <remarks>
        /// A list-valued property in a single-value slot. Encoding it as text keeps the whole override
        /// pipeline - typed value, JSON round-trip, exposed properties, variant rules - working on it
        /// unchanged, where a nested list would have needed a parallel path through every one of them.
        /// </remarks>
        ThemeTokens
    }

    /// <summary>
    /// How a <see cref="DesignerPropertyValue"/> is stored. The first nine are the typed fields that
    /// predate the universal component system; <see cref="Serialized"/> covers everything else -
    /// Vector3, Rect, AnimationCurve, arrays, nested [Serializable] types, and values whose type this
    /// build does not know.
    /// </summary>
    public enum DesignerPropertyValueType { None, Float, Integer, Boolean, String, Color, Vector2, AssetReference, Enum, Serialized, ElementReference }
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

        /// <summary>
        /// Serialized form of a value the seven typed fields above cannot express - Vector3, Rect,
        /// AnimationCurve, an array, a nested [Serializable] class, or a type this build does not
        /// recognise at all.
        /// </summary>
        /// <remarks>
        /// Kept as text rather than growing a field per type: a value carries one shape, and adding
        /// nine more fields would serialize nine empty ones on every entry. It also means a value
        /// written by a newer Designer survives a round trip through an older one instead of being
        /// dropped - the field is simply carried through untouched.
        /// </remarks>
        public string json;

        /// <summary>
        /// Object references that go with <see cref="json"/>: an array of Sprites, or the targets of
        /// a nested class. Unity cannot serialize an Object reference inside a JSON string, so the
        /// json holds indices into this list.
        /// </summary>
        public List<UnityEngine.Object> objectValues = new List<UnityEngine.Object>();

        /// <summary>
        /// Set when the property points at another element on this screen rather than at an asset.
        /// Stored by stable id so duplication and Definition instancing can re-map it.
        /// </summary>
        public DesignerObjectReference reference = new DesignerObjectReference();

        public DesignerPropertyValue Clone() => JsonUtility.FromJson<DesignerPropertyValue>(JsonUtility.ToJson(this));

        /// <summary>True when nothing was ever written into this value.</summary>
        public bool IsEmpty => type == DesignerPropertyValueType.None
                               && string.IsNullOrEmpty(json)
                               && string.IsNullOrEmpty(stringValue)
                               && assetValue == null
                               && (objectValues == null || objectValues.Count == 0);
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

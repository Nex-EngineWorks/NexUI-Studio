using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Properties
{
    public enum DesignerPropertyBackendSupport { Supported, Fallback, Unsupported, PreviewOnly }

    [Flags]
    public enum DesignerPropertyUsage { None = 0, Override = 1, Binding = 2, Animation = 4, All = Override | Binding | Animation }

    public sealed class DesignerPropertyDescriptor
    {
        public DesignerPropertyId Id;
        public string Path;
        public string DisplayName;
        public DesignerPropertyValueType ValueType;
        public DesignerPropertyValue DefaultValue;
        public DesignerPropertyBackendSupport UGUI;
        public DesignerPropertyBackendSupport UIToolkit;
        public string UGUIFallback;
        public string UIToolkitFallback;
        public DesignerPropertyUsage Usage;
    }

    /// <summary>Single source of truth for typed property identity, conversion and backend parity.</summary>
    public static class DesignerPropertyRegistry
    {
        private static readonly Dictionary<DesignerPropertyId, DesignerPropertyDescriptor> ById = new();
        private static readonly Dictionary<string, DesignerPropertyId> ByPath = new(StringComparer.OrdinalIgnoreCase);

        public static IEnumerable<DesignerPropertyDescriptor> All => ById.Values;

        static DesignerPropertyRegistry()
        {
            Add(DesignerPropertyId.Position, "position", DesignerPropertyValueType.Vector2);
            Add(DesignerPropertyId.Width, "width", DesignerPropertyValueType.Float);
            Add(DesignerPropertyId.Height, "height", DesignerPropertyValueType.Float);
            Add(DesignerPropertyId.MinWidth, "minWidth", DesignerPropertyValueType.Float);
            Add(DesignerPropertyId.MinHeight, "minHeight", DesignerPropertyValueType.Float);
            Add(DesignerPropertyId.MaxWidth, "maxWidth", DesignerPropertyValueType.Float, DesignerPropertyBackendSupport.Fallback, DesignerPropertyBackendSupport.Supported,
                uguiFallback: "uGUI LayoutElement has no maximum size; value remains metadata-only.");
            Add(DesignerPropertyId.MaxHeight, "maxHeight", DesignerPropertyValueType.Float, DesignerPropertyBackendSupport.Fallback, DesignerPropertyBackendSupport.Supported,
                uguiFallback: "uGUI LayoutElement has no maximum size; value remains metadata-only.");
            Add(DesignerPropertyId.Anchor, "anchor", DesignerPropertyValueType.Enum, DesignerPropertyBackendSupport.Supported, DesignerPropertyBackendSupport.Fallback,
                uiToolkitFallback: "Translated to absolute edge constraints; no native anchor model.");
            Add(DesignerPropertyId.Pivot, "pivot", DesignerPropertyValueType.Vector2);
            Add(DesignerPropertyId.Rotation, "rotation", DesignerPropertyValueType.Float);
            Add(DesignerPropertyId.Scale, "scale", DesignerPropertyValueType.Vector2);
            Add(DesignerPropertyId.MarginLeft, "margin.left", DesignerPropertyValueType.Float);
            Add(DesignerPropertyId.MarginTop, "margin.top", DesignerPropertyValueType.Float);
            Add(DesignerPropertyId.MarginRight, "margin.right", DesignerPropertyValueType.Float);
            Add(DesignerPropertyId.MarginBottom, "margin.bottom", DesignerPropertyValueType.Float);
            Add(DesignerPropertyId.PaddingLeft, "padding.left", DesignerPropertyValueType.Float);
            Add(DesignerPropertyId.PaddingTop, "padding.top", DesignerPropertyValueType.Float);
            Add(DesignerPropertyId.PaddingRight, "padding.right", DesignerPropertyValueType.Float);
            Add(DesignerPropertyId.PaddingBottom, "padding.bottom", DesignerPropertyValueType.Float);
            Add(DesignerPropertyId.Gap, "gap", DesignerPropertyValueType.Float, DesignerPropertyBackendSupport.Supported, DesignerPropertyBackendSupport.Fallback,
                uiToolkitFallback: "Generated as leading sibling margin for Unity USS compatibility.");
            Add(DesignerPropertyId.AspectRatio, "aspectRatio", DesignerPropertyValueType.Float, DesignerPropertyBackendSupport.Supported, DesignerPropertyBackendSupport.Fallback,
                uiToolkitFallback: "Maintained by generated fixed dimensions when native aspect-ratio is unavailable.");
            Add(DesignerPropertyId.WidthSizing, "widthSizing", DesignerPropertyValueType.Enum);
            Add(DesignerPropertyId.HeightSizing, "heightSizing", DesignerPropertyValueType.Enum);
            Add(DesignerPropertyId.LayoutDirection, "layout.direction", DesignerPropertyValueType.Enum);
            Add(DesignerPropertyId.Wrap, "layout.wrap", DesignerPropertyValueType.Enum, DesignerPropertyBackendSupport.Fallback, DesignerPropertyBackendSupport.Supported,
                uguiFallback: "uGUI wrap uses GridLayoutGroup; arbitrary flex wrapping is not native.");
            Add(DesignerPropertyId.Align, "layout.align", DesignerPropertyValueType.Enum);
            Add(DesignerPropertyId.Justify, "layout.justify", DesignerPropertyValueType.Enum, DesignerPropertyBackendSupport.Fallback, DesignerPropertyBackendSupport.Supported,
                uguiFallback: "Mapped to LayoutGroup childAlignment without space-around/between distribution.");
            Add(DesignerPropertyId.ChildOrder, "siblingIndex", DesignerPropertyValueType.Integer);
            Add(DesignerPropertyId.Overflow, "overflow", DesignerPropertyValueType.Enum);
            Add(DesignerPropertyId.Clip, "clip", DesignerPropertyValueType.Boolean);
            Add(DesignerPropertyId.BackgroundColor, "backgroundColor", DesignerPropertyValueType.Color);
            Add(DesignerPropertyId.Sprite, "sprite", DesignerPropertyValueType.AssetReference);
            Add(DesignerPropertyId.Texture, "texture", DesignerPropertyValueType.AssetReference, DesignerPropertyBackendSupport.Fallback, DesignerPropertyBackendSupport.Supported,
                uguiFallback: "Texture is applied through RawImage when no Sprite is supplied.");
            Add(DesignerPropertyId.Gradient, "gradient", DesignerPropertyValueType.AssetReference, DesignerPropertyBackendSupport.Unsupported, DesignerPropertyBackendSupport.Unsupported,
                "Requires a custom uGUI material.", "Unity USS has no portable serialized Gradient value.");
            Add(DesignerPropertyId.BorderWidth, "border.width", DesignerPropertyValueType.Float, DesignerPropertyBackendSupport.Fallback, DesignerPropertyBackendSupport.Supported,
                uguiFallback: "Approximated with Outline; it is not an inset border.");
            Add(DesignerPropertyId.BorderColor, "border.color", DesignerPropertyValueType.Color, DesignerPropertyBackendSupport.Fallback, DesignerPropertyBackendSupport.Supported,
                uguiFallback: "Approximated with Outline.");
            Add(DesignerPropertyId.CornerRadius, "cornerRadius", DesignerPropertyValueType.Float, DesignerPropertyBackendSupport.Fallback, DesignerPropertyBackendSupport.Supported,
                uguiFallback: "Requires a sliced/rounded source sprite; numeric radius is reported only.");
            Add(DesignerPropertyId.DropShadow, "shadow.drop", DesignerPropertyValueType.Boolean, DesignerPropertyBackendSupport.Supported, DesignerPropertyBackendSupport.Fallback,
                uiToolkitFallback: "Text shadow is supported; box shadow is reported as unsupported fallback.");
            Add(DesignerPropertyId.InnerShadow, "shadow.inner", DesignerPropertyValueType.Boolean, DesignerPropertyBackendSupport.Unsupported, DesignerPropertyBackendSupport.Unsupported);
            Add(DesignerPropertyId.OutlineWidth, "outline.width", DesignerPropertyValueType.Float, DesignerPropertyBackendSupport.Supported, DesignerPropertyBackendSupport.Fallback);
            Add(DesignerPropertyId.OutlineColor, "outline.color", DesignerPropertyValueType.Color, DesignerPropertyBackendSupport.Supported, DesignerPropertyBackendSupport.Fallback);
            Add(DesignerPropertyId.Opacity, "opacity", DesignerPropertyValueType.Float);
            Add(DesignerPropertyId.Blur, "blur", DesignerPropertyValueType.Float, DesignerPropertyBackendSupport.Unsupported, DesignerPropertyBackendSupport.Unsupported);
            Add(DesignerPropertyId.Mask, "mask", DesignerPropertyValueType.Boolean, DesignerPropertyBackendSupport.Supported, DesignerPropertyBackendSupport.Supported);
            Add(DesignerPropertyId.Material, "material", DesignerPropertyValueType.AssetReference, DesignerPropertyBackendSupport.Supported, DesignerPropertyBackendSupport.Unsupported);
            Add(DesignerPropertyId.ImageSlice, "image.slice", DesignerPropertyValueType.Boolean, DesignerPropertyBackendSupport.Supported, DesignerPropertyBackendSupport.Supported);
            Add(DesignerPropertyId.ImageFit, "image.fit", DesignerPropertyValueType.Enum);
            Add(DesignerPropertyId.ImageFill, "image.fill", DesignerPropertyValueType.Float, DesignerPropertyBackendSupport.Supported, DesignerPropertyBackendSupport.Unsupported);
            Add(DesignerPropertyId.Crop, "image.crop", DesignerPropertyValueType.Boolean);
            Add(DesignerPropertyId.Tint, "tint", DesignerPropertyValueType.Color);
            Add(DesignerPropertyId.Text, "text", DesignerPropertyValueType.String, usage: DesignerPropertyUsage.All);
            Add(DesignerPropertyId.FontAsset, "font.asset", DesignerPropertyValueType.AssetReference, DesignerPropertyBackendSupport.Supported, DesignerPropertyBackendSupport.Fallback,
                uiToolkitFallback: "Only Unity Font or compatible FontAsset references can be emitted to USS.");
            Add(DesignerPropertyId.FontFamily, "font.family", DesignerPropertyValueType.String, DesignerPropertyBackendSupport.Fallback, DesignerPropertyBackendSupport.Fallback);
            Add(DesignerPropertyId.FontWeight, "font.weight", DesignerPropertyValueType.Enum);
            Add(DesignerPropertyId.FontStyle, "font.style", DesignerPropertyValueType.Enum);
            Add(DesignerPropertyId.FontSize, "fontSize", DesignerPropertyValueType.Float);
            Add(DesignerPropertyId.AutoFontSize, "font.autoSize", DesignerPropertyValueType.Boolean, DesignerPropertyBackendSupport.Supported, DesignerPropertyBackendSupport.Unsupported);
            Add(DesignerPropertyId.TextAlignment, "text.alignment", DesignerPropertyValueType.Enum);
            Add(DesignerPropertyId.TextWrapping, "text.wrapping", DesignerPropertyValueType.Boolean);
            Add(DesignerPropertyId.TextOverflow, "text.overflow", DesignerPropertyValueType.Enum);
            Add(DesignerPropertyId.Ellipsis, "text.ellipsis", DesignerPropertyValueType.Boolean, DesignerPropertyBackendSupport.Fallback, DesignerPropertyBackendSupport.Supported);
            Add(DesignerPropertyId.LineHeight, "text.lineHeight", DesignerPropertyValueType.Float, DesignerPropertyBackendSupport.Fallback, DesignerPropertyBackendSupport.Supported);
            Add(DesignerPropertyId.LetterSpacing, "text.letterSpacing", DesignerPropertyValueType.Float);
            Add(DesignerPropertyId.ParagraphSpacing, "text.paragraphSpacing", DesignerPropertyValueType.Float, DesignerPropertyBackendSupport.Supported, DesignerPropertyBackendSupport.Unsupported);
            Add(DesignerPropertyId.RichText, "text.richText", DesignerPropertyValueType.Boolean);
            Add(DesignerPropertyId.LocalizationKey, "localizationKey", DesignerPropertyValueType.String, usage: DesignerPropertyUsage.Binding);
            Add(DesignerPropertyId.FontFallback, "font.fallback", DesignerPropertyValueType.AssetReference, DesignerPropertyBackendSupport.Fallback, DesignerPropertyBackendSupport.Fallback);
            Add(DesignerPropertyId.RightToLeft, "text.rtl", DesignerPropertyValueType.Boolean, DesignerPropertyBackendSupport.Supported, DesignerPropertyBackendSupport.Fallback);
            Add(DesignerPropertyId.TextColor, "textColor", DesignerPropertyValueType.Color);
            Add(DesignerPropertyId.TextShadow, "text.shadow", DesignerPropertyValueType.Boolean, DesignerPropertyBackendSupport.Supported, DesignerPropertyBackendSupport.Supported);
            Add(DesignerPropertyId.TextOutline, "text.outline", DesignerPropertyValueType.Boolean, DesignerPropertyBackendSupport.Supported, DesignerPropertyBackendSupport.Fallback);
            Add(DesignerPropertyId.RuntimeVisible, "runtimeVisible", DesignerPropertyValueType.Boolean, usage: DesignerPropertyUsage.Override | DesignerPropertyUsage.Binding);

            Alias("rect.x", DesignerPropertyId.Position); Alias("rect.y", DesignerPropertyId.Position);
            Alias("rect.width", DesignerPropertyId.Width); Alias("rect.height", DesignerPropertyId.Height);
            Alias("visible", DesignerPropertyId.RuntimeVisible); Alias("color", DesignerPropertyId.Tint);
            Alias("autoLayout.spacing", DesignerPropertyId.Gap);
            Alias("autoLayout.paddingLeft", DesignerPropertyId.PaddingLeft);
            Alias("autoLayout.paddingTop", DesignerPropertyId.PaddingTop);
            Alias("autoLayout.paddingRight", DesignerPropertyId.PaddingRight);
            Alias("autoLayout.paddingBottom", DesignerPropertyId.PaddingBottom);

            SetDefault(DesignerPropertyId.Pivot, new DesignerPropertyValue { type = DesignerPropertyValueType.Vector2, vector2Value = new Vector2(0f, 1f) });
            SetDefault(DesignerPropertyId.Scale, new DesignerPropertyValue { type = DesignerPropertyValueType.Vector2, vector2Value = Vector2.one });
            SetDefault(DesignerPropertyId.Opacity, new DesignerPropertyValue { type = DesignerPropertyValueType.Float, floatValue = 1f });
            SetDefault(DesignerPropertyId.FontSize, new DesignerPropertyValue { type = DesignerPropertyValueType.Float, floatValue = 14f });
            SetDefault(DesignerPropertyId.LineHeight, new DesignerPropertyValue { type = DesignerPropertyValueType.Float, floatValue = 1.2f });
            SetDefault(DesignerPropertyId.TextWrapping, new DesignerPropertyValue { type = DesignerPropertyValueType.Boolean, boolValue = true });
            SetDefault(DesignerPropertyId.RichText, new DesignerPropertyValue { type = DesignerPropertyValueType.Boolean, boolValue = true });
            SetDefault(DesignerPropertyId.RuntimeVisible, new DesignerPropertyValue { type = DesignerPropertyValueType.Boolean, boolValue = true });
            SetDefault(DesignerPropertyId.TextColor, new DesignerPropertyValue { type = DesignerPropertyValueType.Color, colorValue = Color.white });
        }

        public static DesignerPropertyDescriptor Get(DesignerPropertyId id)
            => ById.TryGetValue(id, out var descriptor) ? descriptor : null;

        public static DesignerPropertyId ResolveLegacyPath(string path)
            => !string.IsNullOrWhiteSpace(path) && ByPath.TryGetValue(path.Trim(), out var id) ? id : DesignerPropertyId.None;

        public static string PathFor(DesignerPropertyId id) => Get(id)?.Path ?? string.Empty;

        public static string EffectivePath(DesignerPropertyId id, string legacyPath)
            => id != DesignerPropertyId.None ? PathFor(id) : legacyPath;

        public static string EffectiveValue(DesignerPropertyId id, DesignerPropertyValue typedValue, string legacyValue)
            => id != DesignerPropertyId.None ? Serialize(typedValue) : legacyValue;

        public static string Serialize(DesignerPropertyValue value)
        {
            if (value == null) return string.Empty;
            switch (value.type)
            {
                case DesignerPropertyValueType.Float: return value.floatValue.ToString("R", CultureInfo.InvariantCulture);
                case DesignerPropertyValueType.Integer: return value.intValue.ToString(CultureInfo.InvariantCulture);
                case DesignerPropertyValueType.Boolean: return value.boolValue ? "true" : "false";
                case DesignerPropertyValueType.Color: return ColorUtility.ToHtmlStringRGBA(value.colorValue);
                case DesignerPropertyValueType.Vector2: return value.vector2Value.x.ToString("R", CultureInfo.InvariantCulture) + "," + value.vector2Value.y.ToString("R", CultureInfo.InvariantCulture);
                case DesignerPropertyValueType.AssetReference: return value.assetValue != null ? value.assetValue.name : value.stringValue ?? string.Empty;
                default: return value.stringValue ?? string.Empty;
            }
        }

        public static DesignerPropertyValue Parse(DesignerPropertyId id, string value)
        {
            var type = Get(id)?.ValueType ?? DesignerPropertyValueType.String;
            var parsed = new DesignerPropertyValue { type = type, stringValue = value };
            if (type == DesignerPropertyValueType.Float) float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed.floatValue);
            else if (type == DesignerPropertyValueType.Integer) int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed.intValue);
            else if (type == DesignerPropertyValueType.Boolean) bool.TryParse(value, out parsed.boolValue);
            else if (type == DesignerPropertyValueType.Color && !string.IsNullOrEmpty(value))
            {
                var html = value[0] == '#' ? value : "#" + value;
                ColorUtility.TryParseHtmlString(html, out parsed.colorValue);
            }
            else if (type == DesignerPropertyValueType.Vector2)
            {
                var parts = (value ?? string.Empty).Split(',');
                if (parts.Length == 2 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) parsed.vector2Value = new Vector2(x, y);
            }
            return parsed;
        }

        /// <summary>Strict conversion used by authoring validation; unlike Parse it never hides malformed input.</summary>
        public static bool TryParse(DesignerPropertyId id, string source, out DesignerPropertyValue value, out string error)
        {
            value = null;
            error = null;
            var descriptor = Get(id);
            if (descriptor == null)
            {
                error = $"Unknown property id '{id}'.";
                return false;
            }

            var parsed = Parse(id, source);
            var valid = true;
            switch (descriptor.ValueType)
            {
                case DesignerPropertyValueType.Float:
                    valid = float.TryParse(source, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
                            !float.IsNaN(parsed.floatValue) && !float.IsInfinity(parsed.floatValue);
                    break;
                case DesignerPropertyValueType.Integer:
                    valid = int.TryParse(source, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                    break;
                case DesignerPropertyValueType.Boolean:
                    valid = bool.TryParse(source, out _);
                    break;
                case DesignerPropertyValueType.Color:
                    var html = string.IsNullOrEmpty(source) || source[0] == '#' ? source : "#" + source;
                    valid = !string.IsNullOrEmpty(html) && ColorUtility.TryParseHtmlString(html, out _);
                    break;
                case DesignerPropertyValueType.Vector2:
                    var parts = (source ?? string.Empty).Split(',');
                    valid = parts.Length == 2 &&
                            float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                            !float.IsNaN(x) && !float.IsInfinity(x) && !float.IsNaN(y) && !float.IsInfinity(y);
                    break;
                case DesignerPropertyValueType.AssetReference:
                    error = "Asset references must be assigned through an Object field, not parsed from display text.";
                    return false;
                case DesignerPropertyValueType.Enum:
                    valid = !string.IsNullOrWhiteSpace(source);
                    break;
            }

            if (!valid)
            {
                error = $"'{source}' is not a valid {descriptor.ValueType} value for {descriptor.Path}.";
                return false;
            }
            value = parsed;
            return true;
        }

        private static void Add(DesignerPropertyId id, string path, DesignerPropertyValueType type,
            DesignerPropertyBackendSupport ugui = DesignerPropertyBackendSupport.Supported,
            DesignerPropertyBackendSupport uiToolkit = DesignerPropertyBackendSupport.Supported,
            string uguiFallback = null, string uiToolkitFallback = null,
            DesignerPropertyUsage usage = DesignerPropertyUsage.Override | DesignerPropertyUsage.Animation)
        {
            var descriptor = new DesignerPropertyDescriptor
            {
                Id = id, Path = path, DisplayName = SplitName(id.ToString()), ValueType = type,
                DefaultValue = new DesignerPropertyValue { type = type }, UGUI = ugui, UIToolkit = uiToolkit,
                UGUIFallback = uguiFallback, UIToolkitFallback = uiToolkitFallback, Usage = usage
            };
            ById[id] = descriptor;
            ByPath[path] = id;
        }

        private static void Alias(string path, DesignerPropertyId id) => ByPath[path] = id;

        private static void SetDefault(DesignerPropertyId id, DesignerPropertyValue value)
        {
            if (ById.TryGetValue(id, out var descriptor)) descriptor.DefaultValue = value;
        }

        private static string SplitName(string value)
        {
            for (var i = value.Length - 1; i > 0; i--)
                if (char.IsUpper(value[i]) && !char.IsUpper(value[i - 1])) value = value.Insert(i, " ");
            return value;
        }
    }
}

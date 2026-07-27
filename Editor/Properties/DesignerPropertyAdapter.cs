using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Properties
{
    /// <summary>Compatibility bridge between v3 typed style blocks and pre-v3 flat fields.</summary>
    public static class DesignerPropertyAdapter
    {
        public static DesignerLayoutStyleMetadata Layout(DesignerElementMetadata element)
            => element.layoutStyle ??= new DesignerLayoutStyleMetadata();

        public static DesignerVisualStyleMetadata Visual(DesignerElementMetadata element)
            => element.visualStyle ??= new DesignerVisualStyleMetadata();

        public static DesignerTypographyMetadata Typography(DesignerElementMetadata element)
            => element.typography ??= new DesignerTypographyMetadata();

        public static Color BackgroundColor(DesignerElementMetadata element)
            => element.visualStyle != null && element.visualStyle.hasOverrides ? element.visualStyle.backgroundColor : element.tint;

        public static float Opacity(DesignerElementMetadata element)
            => element.visualStyle != null && element.visualStyle.hasOverrides ? Mathf.Clamp01(element.visualStyle.opacity) : 1f;

        public static float CornerRadius(DesignerElementMetadata element)
        {
            if (element.visualStyle != null && element.visualStyle.hasOverrides) return Mathf.Max(0f, element.visualStyle.cornerRadius);
            switch (element.shape)
            {
                case DesignerElementShape.Rectangle: return 0f;
                case DesignerElementShape.Pill:
                case DesignerElementShape.Circle: return Mathf.Min(element.rect.width, element.rect.height) * 0.5f;
                default: return 8f;
            }
        }

        public static Color TextColor(DesignerElementMetadata element)
            => element.typography != null && element.typography.hasOverrides ? element.typography.color : element.textColor;

        public static float FontSize(DesignerElementMetadata element)
            => element.typography != null && element.typography.hasOverrides ? Mathf.Max(1f, element.typography.fontSize) : Mathf.Max(1f, element.fontSize);

        public static bool Clip(DesignerElementMetadata element)
            => element.clipChildren || (element.layoutStyle != null && element.layoutStyle.hasOverrides &&
                                        element.layoutStyle.overflow == DesignerOverflowMode.Hidden) ||
               (element.visualStyle != null && element.visualStyle.hasOverrides && element.visualStyle.crop);

        public static void SetBackgroundColor(DesignerElementMetadata element, Color color)
        {
            element.tint = color;
            var style = Visual(element);
            style.hasOverrides = true;
            style.backgroundColor = color;
        }

        public static void SetTextColor(DesignerElementMetadata element, Color color)
        {
            element.textColor = color;
            var typography = Typography(element);
            typography.hasOverrides = true;
            typography.color = color;
        }

        public static void SetFontSize(DesignerElementMetadata element, float size)
        {
            size = Mathf.Max(1f, size);
            element.fontSize = Mathf.RoundToInt(size);
            var typography = Typography(element);
            typography.hasOverrides = true;
            typography.fontSize = size;
        }
    }
}

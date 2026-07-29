using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Components.Preview
{
    /// <summary>Shared color math for canvas previews, including forced-state modulation.</summary>
    public static class DesignerPreviewColors
    {
        public static Color Lighten(Color c, float amount)
            => new Color(Mathf.Lerp(c.r, 1f, amount), Mathf.Lerp(c.g, 1f, amount), Mathf.Lerp(c.b, 1f, amount), c.a);

        public static Color Darken(Color c, float amount)
            => new Color(Mathf.Lerp(c.r, 0f, amount), Mathf.Lerp(c.g, 0f, amount), Mathf.Lerp(c.b, 0f, amount), c.a);

        public static readonly Color Error = new Color(0.86f, 0.28f, 0.28f);
        public static readonly Color Success = new Color(0.30f, 0.72f, 0.40f);
        public static readonly Color Warning = new Color(0.92f, 0.68f, 0.22f);
        public static readonly Color Accent = new Color(0.25f, 0.55f, 0.95f);

        /// <summary>Modulates a base tint for a forced state (hover/pressed/disabled/loading…).</summary>
        public static Color Modulate(Color tint, DesignerComponentState state)
        {
            if (state == DesignerComponentState.Hover) return Lighten(tint, 0.10f);
            if (state == DesignerComponentState.Pressed) return Darken(tint, 0.10f);
            if (state == DesignerComponentState.Disabled)
            {
                var g = (tint.r + tint.g + tint.b) / 3f;
                return new Color(Mathf.Lerp(tint.r, g, 0.7f), Mathf.Lerp(tint.g, g, 0.7f), Mathf.Lerp(tint.b, g, 0.7f), tint.a * 0.6f);
            }
            return tint;
        }

        /// <summary>An accent border color for a state, or null when the state needs no special border.</summary>
        public static Color? StateBorder(DesignerComponentState state)
        {
            switch (state)
            {
                case DesignerComponentState.Error: return Error;
                case DesignerComponentState.Success: return Success;
                case DesignerComponentState.Warning: return Warning;
                case DesignerComponentState.Selected:
                case DesignerComponentState.Focused: return Accent;
                default: return null;
            }
        }

        public static float StateOpacity(DesignerComponentState state)
        {
            if (state == DesignerComponentState.Disabled) return 0.6f;
            if (state == DesignerComponentState.Loading) return 0.75f;
            if (state == DesignerComponentState.Empty) return 0.5f;
            return 1f;
        }
    }

    /// <summary>Everything a preview renderer needs to build a component's canvas visual.</summary>
    public readonly struct DesignerPreviewContext
    {
        public readonly DesignerElementMetadata Element;
        public readonly DesignerComponentDescriptor Descriptor;
        public readonly DesignerComponentState State; // effective forced state for this element
        public readonly float Zoom;
        public readonly bool Interactive;

        public DesignerPreviewContext(DesignerElementMetadata element, DesignerComponentState state, float zoom, bool interactive)
        {
            Element = element;
            Descriptor = DesignerComponentRegistry.Get(element.elementType);
            State = state;
            Zoom = zoom;
            Interactive = interactive;
        }

        public Color Tint => DesignerPreviewColors.Modulate(Element.tint, State);
        public Color FillColor => DesignerPreviewColors.Lighten(Tint, 0.4f);
    }

    /// <summary>
    /// Builds a component's canvas preview: the Virtual Preview Parts (track/fill, ring, rows,
    /// skeleton bars, slot overlays…) that visualize the component but are never stored as authored
    /// elements. One renderer per component type, resolved through
    /// <see cref="DesignerComponentPreviewRegistry"/> - so adding a type means adding a renderer,
    /// not editing a viewport switch (spec §22-1/§22-2).
    /// </summary>
    public interface IUIDesignerComponentPreviewRenderer
    {
        /// <summary>Adds virtual preview parts into <paramref name="view"/> (the element's box view).</summary>
        void BuildPreview(VisualElement view, in DesignerPreviewContext ctx);
    }

    /// <summary>typeId → preview renderer. Unknown/leaf types get the no-op generic renderer.</summary>
    public static class DesignerComponentPreviewRegistry
    {
        private static readonly Dictionary<string, IUIDesignerComponentPreviewRenderer> _byId =
            new Dictionary<string, IUIDesignerComponentPreviewRenderer>();
        private static readonly IUIDesignerComponentPreviewRenderer _generic = new GenericPreviewRenderer();
        private static bool _built;

        public static IUIDesignerComponentPreviewRenderer Get(string typeId)
        {
            EnsureBuilt();
            return !string.IsNullOrEmpty(typeId) && _byId.TryGetValue(typeId, out var r) ? r : _generic;
        }

        private static void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            var linear = new LinearFillPreviewRenderer();
            _byId["ProgressBar"] = linear;
            _byId["StatBar"] = linear;
            _byId["RadialFill"] = new RadialPreviewRenderer(spin: false);
            _byId["Spinner"] = new RadialPreviewRenderer(spin: true);
            _byId["ChoiceList"] = new ChoiceListPreviewRenderer();
            _byId["List"] = new CollectionPreviewRenderer(grid: false);
            _byId["Grid"] = new CollectionPreviewRenderer(grid: true);
            _byId["Skeleton"] = new SkeletonPreviewRenderer();
            _byId["Image"] = new ImagePreviewRenderer(fullBleed: true);
            _byId["IconButton"] = new ImagePreviewRenderer(fullBleed: false);
            _byId["Slot"] = new SlotPreviewRenderer();
            _byId["Hotbar"] = new HotbarPreviewRenderer();

            // NexUI's extended library plus Unity's stock uGUI / UI Toolkit controls.
            DesignerStockPreviewRenderers.Register(_byId);
        }
    }
}

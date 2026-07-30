using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Designer.Editor.Components
{
    /// <summary>
    /// Every component type that can be attached to a Designer element: Unity's uGUI components, the
    /// UI Toolkit controls, and NexUI's own base components.
    /// </summary>
    /// <remarks>
    /// uGUI and NexUI entries carry their real runtime type, and their property schema is reflected
    /// from it (see <see cref="DesignerReflectedSchema"/>), so the Designer shows the same fields Unity
    /// does. Backend filtering is deliberate: a uGUI component cannot run on a UI Toolkit screen, so it
    /// is not offered there at all rather than being offered and then reported as unwritable.
    /// </remarks>
    public static class DesignerUIComponentRegistry
    {
        private static readonly Dictionary<string, DesignerUIComponentType> ById =
            new Dictionary<string, DesignerUIComponentType>(StringComparer.Ordinal);
        private static bool _built;

        public static IEnumerable<DesignerUIComponentType> All
        {
            get { EnsureBuilt(); return ById.Values; }
        }

        public static DesignerUIComponentType Get(string typeId)
        {
            EnsureBuilt();
            if (string.IsNullOrEmpty(typeId)) return null;
            return ById.TryGetValue(typeId, out var type) ? type : Unknown(typeId);
        }

        public static bool IsRegistered(string typeId)
        {
            EnsureBuilt();
            return !string.IsNullOrEmpty(typeId) && ById.ContainsKey(typeId);
        }

        /// <summary>Components attachable on a screen using <paramref name="backend"/>.</summary>
        public static IEnumerable<DesignerUIComponentType> ForBackend(DesignerUIComponentFamily backend)
        {
            EnsureBuilt();
            foreach (var type in ById.Values)
                if (type.SupportsBackend(backend)) yield return type;
        }

        /// <summary>
        /// A type the current build does not know. Returned instead of null so opening a screen that
        /// uses a component from a newer build never breaks - its values are preserved untouched.
        /// </summary>
        private static DesignerUIComponentType Unknown(string typeId) => new DesignerUIComponentType
        {
            TypeId = typeId,
            DisplayName = typeId,
            LocalizationKey = "component.type.unknown",
            Description = "This component type is not registered in this build. Its values are preserved.",
            Family = DesignerUIComponentFamily.Core,
            Category = DesignerUIComponentCategory.Other,
            AllowMultiple = true
        };

        private static void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            foreach (var type in BuildCore()) ById[type.TypeId] = type;
            foreach (var type in BuildUGUI()) ById[type.TypeId] = type;
            foreach (var type in BuildUIToolkit()) ById[type.TypeId] = type;
            foreach (var type in NexUIBaseComponentCatalog.Build()) ById[type.TypeId] = type;
        }

        // ---- Core (backend independent) ---------------------------------------------------

        private static IEnumerable<DesignerUIComponentType> BuildCore()
        {
            yield return new DesignerUIComponentType
            {
                TypeId = "Core.Element",
                DisplayName = "Element",
                LocalizationKey = "component.type.core.element",
                Description = "Position, size and anchoring. Every element has exactly one, like a RectTransform.",
                Family = DesignerUIComponentFamily.Core,
                Category = DesignerUIComponentCategory.Core,
                IsEssential = true
            };
        }

        // ---- uGUI --------------------------------------------------------------------------

        /// <summary>
        /// The uGUI components worth attaching from a UI authoring tool. Deliberately curated rather
        /// than "every MonoBehaviour in UnityEngine.UI": physics raycasters and event systems belong to
        /// the scene, not to a screen's elements.
        /// </summary>
        private static IEnumerable<DesignerUIComponentType> BuildUGUI()
        {
            yield return UGUI(typeof(Image), DesignerUIComponentCategory.Graphic, renderer: true, conflicts: GraphicConflicts);
            yield return UGUI(typeof(RawImage), DesignerUIComponentCategory.Graphic, renderer: true, conflicts: GraphicConflicts);
            yield return UGUI(typeof(Text), DesignerUIComponentCategory.Text, renderer: true, conflicts: GraphicConflicts);
            yield return UGUI(typeof(TextMeshProUGUI), DesignerUIComponentCategory.Text, renderer: true, conflicts: GraphicConflicts);

            yield return UGUI(typeof(Button), DesignerUIComponentCategory.Control, requires: new[] { "UGUI.Image" });
            yield return UGUI(typeof(Toggle), DesignerUIComponentCategory.Control);
            yield return UGUI(typeof(ToggleGroup), DesignerUIComponentCategory.Control);
            yield return UGUI(typeof(Slider), DesignerUIComponentCategory.Control);
            yield return UGUI(typeof(Scrollbar), DesignerUIComponentCategory.Control);
            yield return UGUI(typeof(Dropdown), DesignerUIComponentCategory.Control);
            yield return UGUI(typeof(TMP_Dropdown), DesignerUIComponentCategory.Control);
            yield return UGUI(typeof(InputField), DesignerUIComponentCategory.Control);
            yield return UGUI(typeof(TMP_InputField), DesignerUIComponentCategory.Control);
            yield return UGUI(typeof(ScrollRect), DesignerUIComponentCategory.Control);

            yield return UGUI(typeof(HorizontalLayoutGroup), DesignerUIComponentCategory.Layout, conflicts: LayoutConflicts);
            yield return UGUI(typeof(VerticalLayoutGroup), DesignerUIComponentCategory.Layout, conflicts: LayoutConflicts);
            yield return UGUI(typeof(GridLayoutGroup), DesignerUIComponentCategory.Layout, conflicts: LayoutConflicts);
            yield return UGUI(typeof(ContentSizeFitter), DesignerUIComponentCategory.Layout);
            yield return UGUI(typeof(AspectRatioFitter), DesignerUIComponentCategory.Layout);
            yield return UGUI(typeof(LayoutElement), DesignerUIComponentCategory.Layout);

            yield return UGUI(typeof(Mask), DesignerUIComponentCategory.Effect, requires: new[] { "UGUI.Image" });
            yield return UGUI(typeof(RectMask2D), DesignerUIComponentCategory.Effect);
            yield return UGUI(typeof(CanvasGroup), DesignerUIComponentCategory.Effect);
            yield return UGUI(typeof(Outline), DesignerUIComponentCategory.Effect, allowMultiple: true);
            yield return UGUI(typeof(Shadow), DesignerUIComponentCategory.Effect, allowMultiple: true);
            yield return UGUI(typeof(Canvas), DesignerUIComponentCategory.Other);
            yield return UGUI(typeof(GraphicRaycaster), DesignerUIComponentCategory.Other, requires: new[] { "UGUI.Canvas" });
        }

        private static readonly string[] GraphicConflicts =
        {
            "UGUI.Image", "UGUI.RawImage", "UGUI.Text", "UGUI.TextMeshProUGUI",
            "NX.RoundedRect", "NX.Gradient"
        };

        private static readonly string[] LayoutConflicts =
        {
            "UGUI.HorizontalLayoutGroup", "UGUI.VerticalLayoutGroup", "UGUI.GridLayoutGroup",
            "NX.FlowLayout", "NX.RadialLayout"
        };

        private static DesignerUIComponentType UGUI(Type type, DesignerUIComponentCategory category,
            bool renderer = false, bool allowMultiple = false, string[] requires = null, string[] conflicts = null)
        {
            var component = new DesignerUIComponentType
            {
                TypeId = "UGUI." + type.Name,
                DisplayName = DesignerReflectedSchema.Humanize(type.Name),
                LocalizationKey = "component.type.ugui." + char.ToLowerInvariant(type.Name[0]) + type.Name.Substring(1),
                Description = $"Unity's {type.Name} component.",
                Family = DesignerUIComponentFamily.UGUI,
                Category = category,
                BackingType = type,
                IsRenderer = renderer,
                AllowMultiple = allowMultiple,
                RequiredComponents = requires ?? Array.Empty<string>(),
                ConflictsWith = conflicts ?? Array.Empty<string>()
            };
            component.Properties.AddRange(DesignerReflectedSchema.Build(type));
            return component;
        }

        // ---- UI Toolkit ---------------------------------------------------------------------

        /// <summary>
        /// On a UI Toolkit screen the control *is* the element, so each stock control is offered as the
        /// element's renderer component. Their schemas come from the existing UI Toolkit catalog, whose
        /// descriptors already carry the UXML tag and attribute names the generator emits.
        /// </summary>
        private static IEnumerable<DesignerUIComponentType> BuildUIToolkit()
        {
            foreach (var descriptor in DesignerComponentRegistry.InFamily(DesignerComponentFamily.UIToolkit))
            {
                var shortId = descriptor.TypeId.Substring(descriptor.TypeId.IndexOf('.') + 1);
                var component = new DesignerUIComponentType
                {
                    TypeId = "UITK." + shortId,
                    DisplayName = descriptor.DisplayName,
                    LocalizationKey = descriptor.LocalizationKey,
                    Description = descriptor.Description,
                    Family = DesignerUIComponentFamily.UIToolkit,
                    Category = CategoryOf(descriptor.Category),
                    UxmlTag = descriptor.UxmlTag,
                    IsRenderer = true,
                    ConflictsWith = new[] { "UITK.*" }
                };
                component.Properties.AddRange(descriptor.Properties);
                yield return component;
            }
        }

        private static DesignerUIComponentCategory CategoryOf(DesignerComponentCategory category) => category switch
        {
            DesignerComponentCategory.Container => DesignerUIComponentCategory.Layout,
            DesignerComponentCategory.Text => DesignerUIComponentCategory.Text,
            DesignerComponentCategory.Media => DesignerUIComponentCategory.Graphic,
            DesignerComponentCategory.Input => DesignerUIComponentCategory.Control,
            DesignerComponentCategory.Data => DesignerUIComponentCategory.Data,
            DesignerComponentCategory.Game => DesignerUIComponentCategory.Game,
            _ => DesignerUIComponentCategory.Other
        };
    }
}

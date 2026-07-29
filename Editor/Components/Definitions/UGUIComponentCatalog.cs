using System.Collections.Generic;
using emiteat.NexUI.Accessibility;
using emiteat.NexUI.Designer.Editor.Backend;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Components
{
    /// <summary>
    /// Descriptors for Unity's stock uGUI controls - everything the editor's <c>GameObject &gt; UI</c>
    /// menu can create. Placing one of these in the Designer produces, on save, the exact same object
    /// hierarchy Unity itself would create (see <c>UGUIControlFactory</c>, which builds them through
    /// <see cref="UnityEngine.UI.DefaultControls"/> / <c>TMP_DefaultControls</c>), so the result is a
    /// normal Unity control that any existing script, animation or style expects.
    ///
    /// Type ids are namespaced (<c>UGUI.Button</c>) so a stock control can never collide with a NexUI
    /// component of the same name, and every descriptor carries an <see cref="DesignerComponentDescriptor.ElementIdPrefix"/>
    /// because generated element ids must stay dot-free (they become GameObject names and USS ids).
    ///
    /// On a UI Toolkit screen these types are preview-only: the canvas still shows them, and the Save
    /// Report says they were not written, rather than silently dropping the user's work.
    /// </summary>
    public static class UGUIComponentCatalog
    {
        public const string IdPrefix = "UGUI.";

        private const DesignerComponentState Interactive =
            DesignerComponentState.Normal | DesignerComponentState.Hover | DesignerComponentState.Pressed |
            DesignerComponentState.Focused | DesignerComponentState.Disabled;

        private const DesignerBindingChannel B_Text = DesignerBindingChannel.Text;
        private const DesignerBindingChannel B_Value = DesignerBindingChannel.Value;
        private const DesignerBindingChannel B_Vis = DesignerBindingChannel.Visibility;
        private const DesignerBindingChannel B_Class = DesignerBindingChannel.Class;
        private const DesignerBindingChannel B_Cmd = DesignerBindingChannel.Command;
        private const DesignerBindingChannel B_Inter = DesignerBindingChannel.Interactable;

        public static bool Owns(string typeId)
            => !string.IsNullOrEmpty(typeId) && typeId.StartsWith(IdPrefix, System.StringComparison.Ordinal);

        public static IEnumerable<DesignerComponentDescriptor> Build()
        {
            // ---- Basics (Image / Text / Panel) -------------------------------------------
            yield return New("Image", "Image", "image", DesignerComponentCategory.Media,
                DesignerPaletteGroup.UGUIBasic, 0, new Vector2(160, 120),
                "UnityEngine.UI.Image with sprite, type and color. Unity's standard UI graphic.",
                control: "Image", role: AccessibilityRole.Image,
                bindings: B_Value | B_Vis | B_Class);

            yield return New("RawImage", "Raw Image", "rawImage", DesignerComponentCategory.Media,
                DesignerPaletteGroup.UGUIBasic, 1, new Vector2(160, 120),
                "UnityEngine.UI.RawImage - draws a raw Texture (render textures, video) with UV rect.",
                control: "RawImage", role: AccessibilityRole.Image,
                bindings: B_Value | B_Vis | B_Class);

            yield return New("Panel", "Panel", "panel", DesignerComponentCategory.Container,
                DesignerPaletteGroup.UGUIBasic, 2, new Vector2(400, 300),
                "Unity's UI Panel: a stretched, semi-transparent Image used as a background surface.",
                control: "Panel", role: AccessibilityRole.Container,
                container: true, bindings: B_Vis | B_Class,
                color: new Color(1f, 1f, 1f, 0.39f));

            yield return New("Text", "Text - TextMeshPro", "text", DesignerComponentCategory.Text,
                DesignerPaletteGroup.UGUIBasic, 3, new Vector2(200, 50),
                "TextMeshProUGUI - Unity's current default text component.",
                control: "TextTMP", role: AccessibilityRole.Label,
                text: "New Text", bindings: B_Text | B_Vis | B_Class,
                color: new Color(0f, 0f, 0f, 0f));

            yield return New("TextLegacy", "Text (Legacy)", "text", DesignerComponentCategory.Text,
                DesignerPaletteGroup.UGUIBasic, 4, new Vector2(200, 50),
                "UnityEngine.UI.Text - the legacy text component. Prefer TextMeshPro for new UI.",
                control: "Text", role: AccessibilityRole.Label,
                text: "New Text", bindings: B_Text | B_Vis | B_Class,
                color: new Color(0f, 0f, 0f, 0f));

            // ---- Controls ----------------------------------------------------------------
            yield return New("Button", "Button", "button", DesignerComponentCategory.Input,
                DesignerPaletteGroup.UGUIControls, 0, new Vector2(160, 30),
                "UnityEngine.UI.Button with a legacy Text child, exactly as GameObject > UI > Button creates it.",
                control: "Button", role: AccessibilityRole.Button,
                text: "Button", interactive: true, canHaveChildren: true,
                states: Interactive | DesignerComponentState.Selected,
                bindings: B_Text | B_Vis | B_Class | B_Cmd | B_Inter,
                events: new[] { "Click" },
                slots: new[] { DesignerComponentRegistry.MakeSlot(DesignerComponentSlot.Content, "Content") },
                color: new Color(1f, 1f, 1f, 1f));

            yield return New("ButtonTMP", "Button - TextMeshPro", "button", DesignerComponentCategory.Input,
                DesignerPaletteGroup.UGUIControls, 1, new Vector2(160, 30),
                "UnityEngine.UI.Button whose label is a TextMeshProUGUI child.",
                control: "ButtonTMP", role: AccessibilityRole.Button,
                text: "Button", interactive: true, canHaveChildren: true,
                states: Interactive | DesignerComponentState.Selected,
                bindings: B_Text | B_Vis | B_Class | B_Cmd | B_Inter,
                events: new[] { "Click" },
                slots: new[] { DesignerComponentRegistry.MakeSlot(DesignerComponentSlot.Content, "Content") },
                color: new Color(1f, 1f, 1f, 1f));

            yield return New("Toggle", "Toggle", "toggle", DesignerComponentCategory.Input,
                DesignerPaletteGroup.UGUIControls, 2, new Vector2(160, 20),
                "UnityEngine.UI.Toggle with Background / Checkmark / Label children.",
                control: "Toggle", role: AccessibilityRole.Toggle,
                text: "Toggle", interactive: true,
                states: Interactive | DesignerComponentState.Selected,
                bindings: B_Text | B_Value | B_Vis | B_Class | B_Inter,
                events: new[] { "ValueChanged" },
                color: new Color(1f, 1f, 1f, 1f));

            yield return New("ToggleGroup", "Toggle Group", "toggleGroup", DesignerComponentCategory.Input,
                DesignerPaletteGroup.UGUIControls, 3, new Vector2(200, 120),
                "Container carrying UnityEngine.UI.ToggleGroup so only one child Toggle can be on.",
                control: "ToggleGroup", role: AccessibilityRole.Container,
                container: true, bindings: B_Value | B_Vis | B_Class,
                events: new[] { "SelectionChanged" },
                color: new Color(0f, 0f, 0f, 0f));

            yield return New("Slider", "Slider", "slider", DesignerComponentCategory.Input,
                DesignerPaletteGroup.UGUIControls, 4, new Vector2(160, 20),
                "UnityEngine.UI.Slider with Background / Fill Area / Handle Slide Area children.",
                control: "Slider", role: AccessibilityRole.Slider,
                interactive: true, valueComponent: true,
                states: Interactive, bindings: B_Value | B_Vis | B_Class | B_Inter,
                events: new[] { "ValueChanged" },
                color: new Color(1f, 1f, 1f, 1f));

            yield return New("Scrollbar", "Scrollbar", "scrollbar", DesignerComponentCategory.Input,
                DesignerPaletteGroup.UGUIControls, 5, new Vector2(160, 20),
                "UnityEngine.UI.Scrollbar with a Sliding Area / Handle.",
                control: "Scrollbar", role: AccessibilityRole.Slider,
                interactive: true, valueComponent: true,
                states: Interactive, bindings: B_Value | B_Vis | B_Class | B_Inter,
                events: new[] { "ValueChanged" },
                color: new Color(1f, 1f, 1f, 1f));

            yield return New("Dropdown", "Dropdown", "dropdown", DesignerComponentCategory.Input,
                DesignerPaletteGroup.UGUIControls, 6, new Vector2(160, 30),
                "UnityEngine.UI.Dropdown (legacy Text) including its Template hierarchy.",
                control: "Dropdown", role: AccessibilityRole.Button,
                interactive: true, canHaveChildren: true,
                states: Interactive, bindings: B_Value | B_Vis | B_Class | B_Inter,
                events: new[] { "ValueChanged" },
                slots: new[] { DesignerComponentRegistry.MakeSlot(DesignerComponentSlot.Content, "Content") },
                color: new Color(1f, 1f, 1f, 1f));

            yield return New("DropdownTMP", "Dropdown - TextMeshPro", "dropdown", DesignerComponentCategory.Input,
                DesignerPaletteGroup.UGUIControls, 7, new Vector2(160, 30),
                "TMP_Dropdown including its Template hierarchy.",
                control: "DropdownTMP", role: AccessibilityRole.Button,
                interactive: true, canHaveChildren: true,
                states: Interactive, bindings: B_Value | B_Vis | B_Class | B_Inter,
                events: new[] { "ValueChanged" },
                slots: new[] { DesignerComponentRegistry.MakeSlot(DesignerComponentSlot.Content, "Content") },
                color: new Color(1f, 1f, 1f, 1f));

            yield return New("InputField", "Input Field (Legacy)", "inputField", DesignerComponentCategory.Input,
                DesignerPaletteGroup.UGUIControls, 8, new Vector2(160, 30),
                "UnityEngine.UI.InputField with Placeholder / Text children.",
                control: "InputField", role: AccessibilityRole.TextField,
                text: "Enter text...", interactive: true, canHaveChildren: true,
                states: Interactive | DesignerComponentState.Error,
                bindings: B_Text | B_Vis | B_Class | B_Inter,
                events: new[] { "ValueChanged", "EndEdit", "Submit" },
                slots: new[] { DesignerComponentRegistry.MakeSlot(DesignerComponentSlot.Content, "Content") },
                color: new Color(1f, 1f, 1f, 1f));

            yield return New("InputFieldTMP", "Input Field - TextMeshPro", "inputField", DesignerComponentCategory.Input,
                DesignerPaletteGroup.UGUIControls, 9, new Vector2(160, 30),
                "TMP_InputField with Text Area / Placeholder children.",
                control: "InputFieldTMP", role: AccessibilityRole.TextField,
                text: "Enter text...", interactive: true, canHaveChildren: true,
                states: Interactive | DesignerComponentState.Error,
                bindings: B_Text | B_Vis | B_Class | B_Inter,
                events: new[] { "ValueChanged", "EndEdit", "Submit" },
                slots: new[] { DesignerComponentRegistry.MakeSlot(DesignerComponentSlot.Content, "Content") },
                color: new Color(1f, 1f, 1f, 1f));

            // ---- Containers / layout ------------------------------------------------------
            yield return New("ScrollView", "Scroll View", "scrollView", DesignerComponentCategory.Container,
                DesignerPaletteGroup.UGUIContainers, 0, new Vector2(200, 200),
                "UnityEngine.UI.ScrollRect with Viewport / Content / Scrollbars. Children are authored into Content.",
                control: "ScrollView", role: AccessibilityRole.Container,
                container: true, bindings: B_Value | B_Vis | B_Class,
                events: new[] { "ValueChanged" },
                color: new Color(1f, 1f, 1f, 1f));

            yield return New("Mask", "Mask", "mask", DesignerComponentCategory.Container,
                DesignerPaletteGroup.UGUIContainers, 1, new Vector2(240, 160),
                "Image + UnityEngine.UI.Mask: children are clipped to this element's sprite.",
                control: "Mask", role: AccessibilityRole.Container,
                container: true, bindings: B_Vis | B_Class);

            yield return New("RectMask2D", "Rect Mask 2D", "rectMask", DesignerComponentCategory.Container,
                DesignerPaletteGroup.UGUIContainers, 2, new Vector2(240, 160),
                "UnityEngine.UI.RectMask2D: rectangular, sprite-free clipping (cheaper than Mask).",
                control: "RectMask2D", role: AccessibilityRole.Container,
                container: true, bindings: B_Vis | B_Class,
                color: new Color(0f, 0f, 0f, 0f));

            yield return New("HorizontalLayoutGroup", "Horizontal Layout Group", "hLayout", DesignerComponentCategory.Container,
                DesignerPaletteGroup.UGUIContainers, 3, new Vector2(320, 80),
                "Empty container carrying HorizontalLayoutGroup - children flow left to right.",
                control: "HorizontalLayoutGroup", role: AccessibilityRole.Container,
                container: true, bindings: B_Vis | B_Class,
                color: new Color(0f, 0f, 0f, 0f));

            yield return New("VerticalLayoutGroup", "Vertical Layout Group", "vLayout", DesignerComponentCategory.Container,
                DesignerPaletteGroup.UGUIContainers, 4, new Vector2(240, 240),
                "Empty container carrying VerticalLayoutGroup - children flow top to bottom.",
                control: "VerticalLayoutGroup", role: AccessibilityRole.Container,
                container: true, bindings: B_Vis | B_Class,
                color: new Color(0f, 0f, 0f, 0f));

            yield return New("GridLayoutGroup", "Grid Layout Group", "gridLayout", DesignerComponentCategory.Container,
                DesignerPaletteGroup.UGUIContainers, 5, new Vector2(320, 240),
                "Empty container carrying GridLayoutGroup - children are placed on a fixed cell grid.",
                control: "GridLayoutGroup", role: AccessibilityRole.Container,
                container: true, bindings: B_Vis | B_Class,
                color: new Color(0f, 0f, 0f, 0f));

            yield return New("Canvas", "Nested Canvas", "canvas", DesignerComponentCategory.Container,
                DesignerPaletteGroup.UGUIContainers, 6, new Vector2(400, 300),
                "Nested Canvas + GraphicRaycaster for independent sorting/overrides inside a screen.",
                control: "Canvas", role: AccessibilityRole.Container,
                container: true, bindings: B_Vis,
                color: new Color(0f, 0f, 0f, 0f),
                uguiSupport: DesignerBackendSupport.Partial);
        }

        private static DesignerComponentDescriptor New(
            string shortId, string displayName, string idPrefix,
            DesignerComponentCategory category, string paletteGroup, int paletteOrder,
            Vector2 size, string description, string control,
            AccessibilityRole role = AccessibilityRole.None,
            string text = null,
            bool container = false, bool canHaveChildren = false, bool interactive = false, bool valueComponent = false,
            DesignerComponentState states = DesignerComponentState.Normal,
            DesignerBindingChannel bindings = DesignerBindingChannel.Visibility,
            string[] events = null,
            DesignerComponentSlot[] slots = null,
            Color? color = null,
            DesignerBackendSupport uguiSupport = DesignerBackendSupport.Full)
        {
            var descriptor = new DesignerComponentDescriptor
            {
                TypeId = IdPrefix + shortId,
                DisplayName = displayName,
                LocalizationKey = "component.ugui." + char.ToLowerInvariant(shortId[0]) + shortId.Substring(1),
                Category = category,
                Family = DesignerComponentFamily.UGUI,
                Icon = "U",
                Description = description,
                PaletteGroup = paletteGroup,
                PaletteOrder = paletteOrder,
                ElementIdPrefix = idPrefix,
                DefaultSize = size,
                // Stock controls are legitimately small (a 20px Toggle row), so they do not inherit
                // the NexUI component minimum.
                MinimumSize = new Vector2(8, 8),
                DefaultShape = DesignerElementShape.Rectangle,
                DefaultColor = color ?? new Color(1f, 1f, 1f, 1f),
                DefaultText = text,
                CanHaveChildren = container || canHaveChildren,
                IsContainer = container,
                IsInteractive = interactive,
                IsValueComponent = valueComponent,
                DefaultAccessibilityRole = role,
                SupportedStates = states,
                SupportedBindings = bindings,
                UGUIControl = control,
                // Stock uGUI controls have no UXML equivalent: on a UI Toolkit screen the canvas still
                // previews them, and the generator reports them instead of emitting a wrong tag.
                UGUISupport = uguiSupport,
                UIToolkitSupport = DesignerBackendSupport.PreviewOnly
            };

            if (events != null)
                descriptor.SupportedEvents.AddRange(events);
            if (slots != null)
                descriptor.Slots.AddRange(slots);
            else if (descriptor.CanHaveChildren)
                descriptor.Slots.Add(DesignerComponentRegistry.MakeSlot(DesignerComponentSlot.Content, "Content"));

            return descriptor;
        }
    }
}

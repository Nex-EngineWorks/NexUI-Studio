using System.Collections.Generic;
using emiteat.NexUI.Accessibility;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Components
{
    /// <summary>
    /// Descriptors for Unity's stock UI Toolkit controls - the entries UI Builder shows in its
    /// Library. Each one carries the UXML tag the code generator emits, so placing a
    /// <c>UITK.DropdownField</c> in the Designer produces a real <c>&lt;ui:DropdownField /&gt;</c> in
    /// the generated UXML rather than a styled <c>VisualElement</c> that only looks the part.
    ///
    /// On a uGUI screen these types are preview-only: the canvas shows them and the Save Report says
    /// they were not written to the prefab (they have no uGUI equivalent), which is the same honest
    /// reporting the rest of the Designer uses for partial backend support.
    /// </summary>
    public static class UIToolkitComponentCatalog
    {
        public const string IdPrefix = "UITK.";

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
            // ---- Basics -------------------------------------------------------------------
            yield return New("VisualElement", "Visual Element", "element", "ui:VisualElement",
                DesignerComponentCategory.Container, DesignerPaletteGroup.UITKBasic, 0, new Vector2(200, 120),
                "The base UI Toolkit element - an empty styled box used for layout and grouping.",
                container: true, role: AccessibilityRole.Container, bindings: B_Vis | B_Class,
                color: new Color(0f, 0f, 0f, 0f));

            yield return New("Label", "Label", "label", "ui:Label",
                DesignerComponentCategory.Text, DesignerPaletteGroup.UITKBasic, 1, new Vector2(200, 24),
                "Text display element.", text: "Label", hasText: true,
                role: AccessibilityRole.Label, bindings: B_Text | B_Vis | B_Class,
                color: new Color(0f, 0f, 0f, 0f));

            yield return New("Image", "Image", "image", "ui:Image",
                DesignerComponentCategory.Media, DesignerPaletteGroup.UITKBasic, 2, new Vector2(160, 120),
                "Displays a Texture/Sprite/VectorImage with a scale mode.",
                role: AccessibilityRole.Image, bindings: B_Value | B_Vis | B_Class);

            yield return New("Box", "Box", "box", "ui:Box",
                DesignerComponentCategory.Container, DesignerPaletteGroup.UITKBasic, 3, new Vector2(240, 140),
                "Styled container with a default border.", container: true,
                role: AccessibilityRole.Container, bindings: B_Vis | B_Class);

            yield return New("TextElement", "Text Element", "textElement", "ui:TextElement",
                DesignerComponentCategory.Text, DesignerPaletteGroup.UITKBasic, 4, new Vector2(200, 24),
                "Base retained-mode text element.", text: "Text", hasText: true,
                role: AccessibilityRole.Label, bindings: B_Text | B_Vis | B_Class,
                color: new Color(0f, 0f, 0f, 0f));

            yield return New("BindableElement", "Bindable Element", "bindable", "ui:BindableElement",
                DesignerComponentCategory.Container, DesignerPaletteGroup.UITKBasic, 5, new Vector2(200, 120),
                "Base VisualElement with a binding path.", container: true,
                role: AccessibilityRole.Container, bindings: B_Value | B_Vis | B_Class,
                color: new Color(0f, 0f, 0f, 0f));

            yield return New("IMGUIContainer", "IMGUI Container", "imgui", "ui:IMGUIContainer",
                DesignerComponentCategory.Container, DesignerPaletteGroup.UITKBasic, 6, new Vector2(240, 140),
                "Hosts an IMGUI onGUI handler. The handler must be assigned from code.",
                role: AccessibilityRole.Container, bindings: B_Vis,
                color: new Color(0f, 0f, 0f, 0f), uitkSupport: DesignerBackendSupport.Partial);

            yield return New("HelpBox", "Help Box", "helpBox", "ui:HelpBox",
                DesignerComponentCategory.Feedback, DesignerPaletteGroup.UITKBasic, 4, new Vector2(280, 48),
                "Inline info/warning/error message box.", text: "Message", hasText: true,
                role: AccessibilityRole.Label, bindings: B_Text | B_Vis);

            // ---- Controls -----------------------------------------------------------------
            yield return New("Button", "Button", "button", "ui:Button",
                DesignerComponentCategory.Input, DesignerPaletteGroup.UITKControls, 0, new Vector2(160, 28),
                "Clickable button.", text: "Button", hasText: true, interactive: true,
                role: AccessibilityRole.Button, states: Interactive,
                bindings: B_Text | B_Vis | B_Class | B_Cmd | B_Inter, events: new[] { "Click" });

            yield return New("Toggle", "Toggle", "toggle", "ui:Toggle",
                DesignerComponentCategory.Input, DesignerPaletteGroup.UITKControls, 1, new Vector2(180, 22),
                "Checkbox with an optional label.", text: "Toggle", hasText: true,
                hasTextAttribute: "label", interactive: true,
                role: AccessibilityRole.Toggle, states: Interactive,
                bindings: B_Text | B_Value | B_Vis | B_Class | B_Inter, events: new[] { "ValueChanged" });

            yield return New("Slider", "Slider", "slider", "ui:Slider",
                DesignerComponentCategory.Input, DesignerPaletteGroup.UITKControls, 2, new Vector2(200, 22),
                "Float slider between low and high values.", interactive: true, valueComponent: true,
                role: AccessibilityRole.Slider, states: Interactive,
                bindings: B_Value | B_Vis | B_Class | B_Inter, events: new[] { "ValueChanged" });

            yield return New("SliderInt", "Slider (Int)", "sliderInt", "ui:SliderInt",
                DesignerComponentCategory.Input, DesignerPaletteGroup.UITKControls, 3, new Vector2(200, 22),
                "Integer slider.", interactive: true, valueComponent: true,
                role: AccessibilityRole.Slider, states: Interactive,
                bindings: B_Value | B_Vis | B_Class | B_Inter, events: new[] { "ValueChanged" });

            yield return New("MinMaxSlider", "Min Max Slider", "minMaxSlider", "ui:MinMaxSlider",
                DesignerComponentCategory.Input, DesignerPaletteGroup.UITKControls, 4, new Vector2(200, 22),
                "Range slider with independent min and max handles.", interactive: true, valueComponent: true,
                role: AccessibilityRole.Slider, states: Interactive,
                bindings: B_Value | B_Vis | B_Class | B_Inter, events: new[] { "ValueChanged" });

            yield return New("ProgressBar", "Progress Bar", "progressBar", "ui:ProgressBar",
                DesignerComponentCategory.Feedback, DesignerPaletteGroup.UITKControls, 5, new Vector2(220, 22),
                "Determinate progress indicator with an optional title.", valueComponent: true,
                role: AccessibilityRole.ProgressIndicator,
                states: DesignerComponentState.Normal | DesignerComponentState.Indeterminate,
                bindings: B_Value | B_Text | B_Vis);

            yield return New("RadioButton", "Radio Button", "radio", "ui:RadioButton",
                DesignerComponentCategory.Input, DesignerPaletteGroup.UITKControls, 6, new Vector2(180, 22),
                "Single choice within a Radio Button Group.", text: "Option", hasText: true,
                hasTextAttribute: "label", interactive: true,
                role: AccessibilityRole.Toggle, states: Interactive,
                bindings: B_Text | B_Value | B_Vis | B_Inter, events: new[] { "ValueChanged" });

            yield return New("RadioButtonGroup", "Radio Button Group", "radioGroup", "ui:RadioButtonGroup",
                DesignerComponentCategory.Input, DesignerPaletteGroup.UITKControls, 7, new Vector2(220, 90),
                "Groups Radio Buttons so exactly one is selected.", container: true, interactive: true,
                role: AccessibilityRole.Container, states: Interactive,
                bindings: B_Value | B_Vis | B_Inter, events: new[] { "ValueChanged" });

            yield return New("DropdownField", "Dropdown Field", "dropdown", "ui:DropdownField",
                DesignerComponentCategory.Input, DesignerPaletteGroup.UITKControls, 8, new Vector2(200, 22),
                "Popup list of string choices.", interactive: true,
                role: AccessibilityRole.Button, states: Interactive,
                bindings: B_Value | B_Text | B_Vis | B_Inter, events: new[] { "ValueChanged" });

            yield return New("EnumField", "Enum Field", "enumField", "ui:EnumField",
                DesignerComponentCategory.Input, DesignerPaletteGroup.UITKControls, 9, new Vector2(200, 22),
                "Popup bound to an enum type.", interactive: true,
                role: AccessibilityRole.Button, states: Interactive,
                bindings: B_Value | B_Vis | B_Inter, events: new[] { "ValueChanged" });

            yield return New("Scroller", "Scroller", "scroller", "ui:Scroller",
                DesignerComponentCategory.Input, DesignerPaletteGroup.UITKControls, 10, new Vector2(200, 18),
                "Low-level slider with decrement/increment buttons, used by ScrollView.",
                interactive: true, valueComponent: true, role: AccessibilityRole.Slider, states: Interactive,
                bindings: B_Value | B_Vis | B_Inter, events: new[] { "ValueChanged" });

            yield return New("RepeatButton", "Repeat Button", "repeatButton", "ui:RepeatButton",
                DesignerComponentCategory.Input, DesignerPaletteGroup.UITKControls, 11, new Vector2(160, 28),
                "Button that repeatedly invokes its action while pressed.", text: "Repeat", hasText: true,
                interactive: true, role: AccessibilityRole.Button, states: Interactive,
                bindings: B_Text | B_Vis | B_Cmd | B_Inter, events: new[] { "Click", "Repeat" });

            yield return New("ToggleButtonGroup", "Toggle Button Group", "toggleButtons", "ui:ToggleButtonGroup",
                DesignerComponentCategory.Input, DesignerPaletteGroup.UITKControls, 12, new Vector2(240, 28),
                "Mutually exclusive or multi-select row of toggle buttons.", interactive: true, valueComponent: true,
                role: AccessibilityRole.Container, states: Interactive,
                bindings: B_Value | B_Vis | B_Inter, events: new[] { "ValueChanged" });

            // ---- Fields --------------------------------------------------------------------
            yield return New("TextField", "Text Field", "textField", "ui:TextField",
                DesignerComponentCategory.Input, DesignerPaletteGroup.UITKFields, 0, new Vector2(220, 22),
                "Editable single or multi-line text input.", interactive: true,
                role: AccessibilityRole.TextField, states: Interactive | DesignerComponentState.Error,
                bindings: B_Text | B_Value | B_Vis | B_Inter, events: new[] { "ValueChanged" });

            yield return New("IntegerField", "Integer Field", "intField", "ui:IntegerField",
                DesignerComponentCategory.Input, DesignerPaletteGroup.UITKFields, 1, new Vector2(220, 22),
                "Integer number input.", interactive: true,
                role: AccessibilityRole.TextField, states: Interactive,
                bindings: B_Value | B_Vis | B_Inter, events: new[] { "ValueChanged" });

            yield return New("FloatField", "Float Field", "floatField", "ui:FloatField",
                DesignerComponentCategory.Input, DesignerPaletteGroup.UITKFields, 2, new Vector2(220, 22),
                "Floating point number input.", interactive: true,
                role: AccessibilityRole.TextField, states: Interactive,
                bindings: B_Value | B_Vis | B_Inter, events: new[] { "ValueChanged" });

            yield return New("Vector2Field", "Vector2 Field", "vector2Field", "ui:Vector2Field",
                DesignerComponentCategory.Input, DesignerPaletteGroup.UITKFields, 3, new Vector2(240, 22),
                "Two-component vector input.", interactive: true, states: Interactive,
                bindings: B_Value | B_Vis | B_Inter, events: new[] { "ValueChanged" });

            yield return New("Vector3Field", "Vector3 Field", "vector3Field", "ui:Vector3Field",
                DesignerComponentCategory.Input, DesignerPaletteGroup.UITKFields, 4, new Vector2(240, 22),
                "Three-component vector input.", interactive: true, states: Interactive,
                bindings: B_Value | B_Vis | B_Inter, events: new[] { "ValueChanged" });

            yield return New("Vector4Field", "Vector4 Field", "vector4Field", "ui:Vector4Field",
                DesignerComponentCategory.Input, DesignerPaletteGroup.UITKFields, 5, new Vector2(240, 22),
                "Four-component vector input.", interactive: true, states: Interactive,
                bindings: B_Value | B_Vis | B_Inter, events: new[] { "ValueChanged" });

            yield return New("RectField", "Rect Field", "rectField", "ui:RectField",
                DesignerComponentCategory.Input, DesignerPaletteGroup.UITKFields, 6, new Vector2(240, 44),
                "Rect (x/y/w/h) input.", interactive: true, states: Interactive,
                bindings: B_Value | B_Vis | B_Inter, events: new[] { "ValueChanged" });

            yield return New("ObjectField", "Object Field", "objectField", "uie:ObjectField",
                DesignerComponentCategory.Input, DesignerPaletteGroup.UITKFields, 7, new Vector2(240, 22),
                "Asset/Object picker. Editor-only: UnityEditor.UIElements is not available in a player build.",
                interactive: true, states: Interactive,
                bindings: B_Value | B_Vis | B_Inter, events: new[] { "ValueChanged" },
                uitkSupport: DesignerBackendSupport.Partial);

            yield return Field("DoubleField", "Double Field", "doubleField", "ui:DoubleField", 8, "Double-precision number input.");
            yield return Field("LongField", "Long Field", "longField", "ui:LongField", 9, "Signed 64-bit integer input.");
            yield return Field("UnsignedIntegerField", "Unsigned Integer Field", "uintField", "ui:UnsignedIntegerField", 10, "Unsigned integer input.");
            yield return Field("UnsignedLongField", "Unsigned Long Field", "ulongField", "ui:UnsignedLongField", 11, "Unsigned 64-bit integer input.");
            yield return Field("Vector2IntField", "Vector2Int Field", "vector2IntField", "ui:Vector2IntField", 12, "Two-component integer vector input.");
            yield return Field("Vector3IntField", "Vector3Int Field", "vector3IntField", "ui:Vector3IntField", 13, "Three-component integer vector input.");
            yield return Field("RectIntField", "RectInt Field", "rectIntField", "ui:RectIntField", 14, "Integer rectangle input.");
            yield return Field("BoundsField", "Bounds Field", "boundsField", "ui:BoundsField", 15, "3D bounds center and size input.");
            yield return Field("BoundsIntField", "BoundsInt Field", "boundsIntField", "ui:BoundsIntField", 16, "Integer 3D bounds input.");
            yield return Field("Hash128Field", "Hash128 Field", "hashField", "ui:Hash128Field", 17, "Hash128 value input.");
            yield return Field("AngleField", "Angle Field", "angleField", "ui:AngleField", 18, "Angle input with drag editing.");
            yield return Field("LengthField", "Length Field", "lengthField", "ui:LengthField", 19, "UI Toolkit Length value and unit input.");
            yield return Field("TimeValueField", "Time Value Field", "timeField", "ui:TimeValueField", 20, "Time value input.");
            yield return Field("TranslateField", "Translate Field", "translateField", "ui:TranslateField", 21, "Style translate value input.");
            yield return Field("RotateField", "Rotate Field", "rotateField", "ui:RotateField", 22, "Style rotate value input.");
            yield return Field("ScaleField", "Scale Field", "scaleField", "ui:ScaleField", 23, "Style scale value input.");
            yield return Field("TransformOriginField", "Transform Origin Field", "originField", "ui:TransformOriginField", 24, "Style transform-origin input.");
            yield return Field("GUIDField", "GUID Field", "guidField", "ui:GUIDField", 25, "GUID value input.");
            yield return Field("ButtonStripField", "Button Strip Field", "buttonStrip", "ui:ButtonStripField", 26, "Compact selection field rendered as a button strip.");

            // ---- Containers ------------------------------------------------------------------
            yield return New("ScrollView", "Scroll View", "scrollView", "ui:ScrollView",
                DesignerComponentCategory.Container, DesignerPaletteGroup.UITKContainers, 0, new Vector2(280, 220),
                "Scrollable content area with automatic scrollers.", container: true,
                role: AccessibilityRole.Container, bindings: B_Vis | B_Class);

            yield return New("ListView", "List View", "listView", "ui:ListView",
                DesignerComponentCategory.Data, DesignerPaletteGroup.UITKContainers, 1, new Vector2(280, 260),
                "Virtualized list bound to an items source.", container: true, collection: true,
                role: AccessibilityRole.List,
                states: DesignerComponentState.Normal | DesignerComponentState.Empty,
                bindings: B_Value | B_Vis, events: new[] { "SelectionChanged", "ItemsChosen" },
                slots: new[] { DesignerComponentRegistry.MakeSlot("item", "Item Template", 0, 1, template: true) });

            yield return New("MultiColumnListView", "Multi Column List View", "columnList", "ui:MultiColumnListView",
                DesignerComponentCategory.Data, DesignerPaletteGroup.UITKContainers, 2, new Vector2(320, 260),
                "Virtualized list with resizable columns.", container: true, collection: true,
                role: AccessibilityRole.List,
                states: DesignerComponentState.Normal | DesignerComponentState.Empty,
                bindings: B_Value | B_Vis, events: new[] { "SelectionChanged" },
                slots: new[] { DesignerComponentRegistry.MakeSlot("item", "Item Template", 0, 1, template: true) });

            yield return New("TreeView", "Tree View", "treeView", "ui:TreeView",
                DesignerComponentCategory.Data, DesignerPaletteGroup.UITKContainers, 3, new Vector2(280, 260),
                "Hierarchical, virtualized tree bound to an items source.", container: true, collection: true,
                role: AccessibilityRole.List,
                states: DesignerComponentState.Normal | DesignerComponentState.Empty,
                bindings: B_Value | B_Vis, events: new[] { "SelectionChanged" },
                slots: new[] { DesignerComponentRegistry.MakeSlot("item", "Item Template", 0, 1, template: true) });

            yield return New("MultiColumnTreeView", "Multi Column Tree View", "columnTree", "ui:MultiColumnTreeView",
                DesignerComponentCategory.Data, DesignerPaletteGroup.UITKContainers, 4, new Vector2(360, 280),
                "Hierarchical virtualized tree with resizable columns.", container: true, collection: true,
                role: AccessibilityRole.List,
                states: DesignerComponentState.Normal | DesignerComponentState.Empty,
                bindings: B_Value | B_Vis, events: new[] { "SelectionChanged" },
                slots: new[] { DesignerComponentRegistry.MakeSlot("item", "Item Template", 0, 1, template: true) });

            yield return New("Foldout", "Foldout", "foldout", "ui:Foldout",
                DesignerComponentCategory.Container, DesignerPaletteGroup.UITKContainers, 4, new Vector2(260, 120),
                "Collapsible section with a header toggle.", text: "Foldout", hasText: true,
                container: true, role: AccessibilityRole.Container,
                bindings: B_Text | B_Value | B_Vis, events: new[] { "ValueChanged" });

            yield return New("GroupBox", "Group Box", "groupBox", "ui:GroupBox",
                DesignerComponentCategory.Container, DesignerPaletteGroup.UITKContainers, 5, new Vector2(260, 140),
                "Titled grouping container.", text: "Group", hasText: true,
                container: true, role: AccessibilityRole.Container, bindings: B_Text | B_Vis);

            yield return New("TabView", "Tab View", "tabView", "ui:TabView",
                DesignerComponentCategory.Container, DesignerPaletteGroup.UITKContainers, 6, new Vector2(320, 240),
                "Tab strip whose children are Tab elements.", container: true,
                role: AccessibilityRole.Container, bindings: B_Value | B_Vis,
                events: new[] { "ActiveTabChanged" },
                slots: new[] { DesignerComponentRegistry.MakeSlot(DesignerComponentSlot.Content, "Tabs", 0, int.MaxValue, accepted: new[] { "UITK.Tab" }) });

            yield return New("Tab", "Tab", "tab", "ui:Tab",
                DesignerComponentCategory.Container, DesignerPaletteGroup.UITKContainers, 7, new Vector2(300, 200),
                "One page of a Tab View.", text: "Tab", hasText: true, hasTextAttribute: "label",
                container: true, role: AccessibilityRole.Container, bindings: B_Text | B_Vis);

            yield return New("TwoPaneSplitView", "Two Pane Split View", "splitView", "ui:TwoPaneSplitView",
                DesignerComponentCategory.Container, DesignerPaletteGroup.UITKContainers, 8, new Vector2(360, 240),
                "Two resizable panes separated by a draggable handle.", container: true,
                role: AccessibilityRole.Container, bindings: B_Vis,
                slots: new[]
                {
                    DesignerComponentRegistry.MakeSlot("first", "First Pane", 0, 1),
                    DesignerComponentRegistry.MakeSlot("second", "Second Pane", 0, 1)
                });

            yield return New("PopupWindow", "Popup Window", "popupWindow", "ui:PopupWindow",
                DesignerComponentCategory.Container, DesignerPaletteGroup.UITKContainers, 9, new Vector2(280, 180),
                "Popup-styled content container.", container: true,
                role: AccessibilityRole.Dialog, bindings: B_Vis | B_Class);
        }

        private static DesignerComponentDescriptor Field(string shortId, string displayName, string idPrefix,
            string uxmlTag, int order, string description)
            => New(shortId, displayName, idPrefix, uxmlTag,
                DesignerComponentCategory.Input, DesignerPaletteGroup.UITKFields, order, new Vector2(240, 22),
                description, interactive: true, valueComponent: true,
                role: AccessibilityRole.TextField, states: Interactive,
                bindings: B_Value | B_Vis | B_Inter, events: new[] { "ValueChanged" });

        private static DesignerComponentDescriptor New(
            string shortId, string displayName, string idPrefix, string uxmlTag,
            DesignerComponentCategory category, string paletteGroup, int paletteOrder,
            Vector2 size, string description,
            string text = null, bool hasText = false, string hasTextAttribute = null,
            bool container = false, bool interactive = false, bool valueComponent = false, bool collection = false,
            AccessibilityRole role = AccessibilityRole.None,
            DesignerComponentState states = DesignerComponentState.Normal,
            DesignerBindingChannel bindings = DesignerBindingChannel.Visibility,
            string[] events = null,
            DesignerComponentSlot[] slots = null,
            Color? color = null,
            DesignerBackendSupport uitkSupport = DesignerBackendSupport.Full)
        {
            var descriptor = new DesignerComponentDescriptor
            {
                TypeId = IdPrefix + shortId,
                DisplayName = displayName,
                LocalizationKey = "component.uitk." + char.ToLowerInvariant(shortId[0]) + shortId.Substring(1),
                Category = category,
                Family = DesignerComponentFamily.UIToolkit,
                Icon = "E",
                Description = description,
                PaletteGroup = paletteGroup,
                PaletteOrder = paletteOrder,
                ElementIdPrefix = idPrefix,
                DefaultSize = size,
                // Stock controls are legitimately small (a 22px field row), so they do not inherit
                // the NexUI component minimum.
                MinimumSize = new Vector2(8, 8),
                DefaultShape = DesignerElementShape.Rectangle,
                DefaultColor = color ?? new Color(0.17f, 0.21f, 0.28f, 1f),
                DefaultText = text,
                CanHaveChildren = container,
                IsContainer = container,
                IsInteractive = interactive,
                IsValueComponent = valueComponent,
                IsCollectionComponent = collection,
                DefaultAccessibilityRole = role,
                SupportedStates = states,
                SupportedBindings = bindings,
                UxmlTag = uxmlTag,
                UxmlHasText = hasText,
                UxmlTextAttribute = hasTextAttribute,
                // Stock UI Toolkit controls have no uGUI equivalent; the canvas previews them and the
                // uGUI save path reports them instead of writing a misleading GameObject.
                UGUISupport = DesignerBackendSupport.PreviewOnly,
                UIToolkitSupport = uitkSupport
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

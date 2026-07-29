using emiteat.NexUI.Designer.Editor.Components;

namespace emiteat.NexUI.Designer.Editor.Components.Definitions
{
    /// <summary>Attaches editable internal-part contracts after every component catalog is built.</summary>
    internal static class NexUIComponentPartSchemas
    {
        public static void Apply(DesignerComponentDescriptor descriptor)
        {
            if (descriptor == null) return;
            switch (descriptor.UGUIControl)
            {
                case "Button":
                    Add(descriptor, "label", "Label", "Button caption.", "Text");
                    break;
                case "ButtonTMP":
                    Add(descriptor, "label", "Label", "Button caption.", "Text (TMP)");
                    break;
                case "Toggle":
                    Add(descriptor, "background", "Background", "Toggle box or radio background.", "Background");
                    Add(descriptor, "checkmark", "Checkmark", "Selected-state mark drawn inside the background.", "Background/Checkmark");
                    Add(descriptor, "label", "Label", "Caption displayed beside the toggle.", "Label");
                    break;
                case "Slider":
                    Add(descriptor, "track", "Track", "The slider's background rail.", "Background");
                    Add(descriptor, "fill", "Fill", "Filled value region.", "Fill Area/Fill");
                    Add(descriptor, "handle", "Handle", "The draggable value handle.", "Handle Slide Area/Handle");
                    break;
                case "Scrollbar":
                    // The stock Scrollbar rail is the authored element root itself. Its Transform
                    // belongs to the normal Layout Inspector; treating it as an internal child
                    // would fight ApplyRect on repeated saves.
                    Add(descriptor, "track", "Track", "Scrollbar background rail (use element Layout for backend output).", null, true);
                    Add(descriptor, "handle", "Handle", "The draggable scrollbar handle.", "Sliding Area/Handle");
                    break;
                case "Dropdown":
                case "DropdownTMP":
                    Add(descriptor, "label", "Caption", "Currently selected option label.", "Label");
                    Add(descriptor, "arrow", "Arrow", "Dropdown disclosure arrow.", "Arrow");
                    Add(descriptor, "template", "Popup Template", "Popup list hierarchy shown when opened.", "Template");
                    break;
                case "InputField":
                case "InputFieldTMP":
                    Add(descriptor, "text", "Text", "Editable text region.", descriptor.UGUIControl == "InputFieldTMP" ? "Text Area/Text" : "Text");
                    Add(descriptor, "placeholder", "Placeholder", "Hint shown when the field is empty.", descriptor.UGUIControl == "InputFieldTMP" ? "Text Area/Placeholder" : "Placeholder");
                    break;
                case "ScrollView":
                    Add(descriptor, "viewport", "Viewport", "Clipped visible region.", "Viewport");
                    Add(descriptor, "content", "Content", "Scrollable content transform.", "Viewport/Content");
                    Add(descriptor, "vertical-scrollbar", "Vertical Scrollbar", "Vertical scrolling control.", "Scrollbar Vertical");
                    Add(descriptor, "horizontal-scrollbar", "Horizontal Scrollbar", "Horizontal scrolling control.", "Scrollbar Horizontal");
                    break;
            }

            if (descriptor.TypeId == "UITK.Button")
                Add(descriptor, "label", "Label", "Button caption.", null, true);
            if (descriptor.TypeId == "UITK.Toggle" || descriptor.TypeId == "UITK.RadioButton")
            {
                Add(descriptor, "background", "Background", "Toggle input background.", null, false, ".unity-toggle__input");
                Add(descriptor, "checkmark", "Checkmark", "Selected-state mark.", null, false, ".unity-toggle__checkmark");
                Add(descriptor, "label", "Label", "Toggle caption.", null, false, ".unity-toggle__text");
            }
            if (descriptor.TypeId == "UITK.Slider" || descriptor.TypeId == "UITK.SliderInt" ||
                descriptor.TypeId == "UITK.MinMaxSlider" || descriptor.TypeId == "UITK.Scroller")
            {
                Add(descriptor, "track", "Track", "Slider tracker.", null, false, ".unity-base-slider__tracker");
                Add(descriptor, "fill", "Fill", "Slider fill region.", null, true);
                Add(descriptor, "handle", "Handle", "Slider dragger.", null, false, ".unity-base-slider__dragger");
            }
            if (descriptor.TypeId == "UITK.ScrollView")
            {
                Add(descriptor, "viewport", "Viewport", "Clipped visible region.", null, false, ".unity-scroll-view__content-viewport");
                Add(descriptor, "content", "Content", "Scrollable content container.", null, false, ".unity-scroll-view__content-container");
                Add(descriptor, "vertical-scrollbar", "Vertical Scrollbar", "Vertical scroller.", null, false, ".unity-scroll-view__vertical-scroller");
                Add(descriptor, "horizontal-scrollbar", "Horizontal Scrollbar", "Horizontal scroller.", null, false, ".unity-scroll-view__horizontal-scroller");
            }

            // NexUI controls share the same visual vocabulary even when their backend mapping is
            // partial. These remain useful preview overrides and are reported honestly on save.
            if (descriptor.Parts.Count == 0)
            {
                if (descriptor.IsValueComponent)
                {
                    Add(descriptor, "track", "Track", "Background track for this value component.", null, true);
                    Add(descriptor, "fill", "Fill", "Value-driven fill region.", null, true);
                }
                if (descriptor.TypeId == "Switch")
                {
                    Add(descriptor, "track", "Track", "Switch background track.", null, true);
                    Add(descriptor, "handle", "Handle", "Switch thumb.", null, true);
                }
                if (descriptor.TypeId == "Checkbox")
                {
                    Add(descriptor, "background", "Background", "Checkbox box.", null, true);
                    Add(descriptor, "checkmark", "Checkmark", "Selected-state mark.", null, true);
                    Add(descriptor, "label", "Label", "Checkbox caption.", null, true);
                }
            }
        }

        private static void Add(DesignerComponentDescriptor descriptor, string id, string name,
            string description, string uguiPath, bool previewOnly = false, string uitkSelector = null)
        {
            if (descriptor.GetPart(id) != null) return;
            descriptor.Parts.Add(new DesignerComponentPartDescriptor
            {
                PartId = id,
                DisplayName = name,
                Description = description,
                UGUIPath = uguiPath,
                UIToolkitSelector = uitkSelector,
                PreviewOnly = previewOnly || (uguiPath == null && uitkSelector == null)
            });
        }
    }
}

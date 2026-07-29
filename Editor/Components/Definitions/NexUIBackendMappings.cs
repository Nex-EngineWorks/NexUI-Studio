using System.Collections.Generic;

namespace emiteat.NexUI.Designer.Editor.Components
{
    /// <summary>
    /// Maps NexUI components onto Unity's stock uGUI controls wherever an honest equivalent exists.
    /// </summary>
    /// <remarks>
    /// A NexUI Slider saved to a uGUI screen should be a real <c>UnityEngine.UI.Slider</c> - with its
    /// min/max, whole-numbers and direction actually set - not a styled box that only looks like one.
    /// That is what makes the generated prefab usable by ordinary Unity code, animation and tooling.
    ///
    /// Only exact matches are listed. A Quest Card or a Battle Pass Track has no stock equivalent, so
    /// it keeps writing structure and style, and the Save Report says what it could not express -
    /// which is the honest outcome rather than emitting a misleading component.
    /// </remarks>
    internal static class NexUIBackendMappings
    {
        /// <summary>NexUI type id → <c>UGUIControlFactory</c> control key.</summary>
        private static readonly Dictionary<string, string> UGUIControls = new Dictionary<string, string>
        {
            // Value controls
            { "Slider", "Slider" }, { "VolumeSlider", "Slider" }, { "Scrubber", "Slider" },
            { "SettingsSliderRow", "Slider" },

            // Toggles
            { "Checkbox", "Toggle" }, { "Switch", "Toggle" }, { "ToggleButton", "Toggle" },
            { "SettingsToggleRow", "Toggle" }, { "CrossplayToggle", "Toggle" },
            { "RadioGroup", "ToggleGroup" }, { "CheckboxGroup", "ToggleGroup" },

            // Choice
            { "Dropdown", "DropdownTMP" }, { "ComboBox", "DropdownTMP" },
            { "LanguageSelector", "DropdownTMP" }, { "SortSelector", "DropdownTMP" },

            // Text input
            { "TextField", "InputFieldTMP" }, { "TextArea", "InputFieldTMP" },
            { "SearchField", "InputFieldTMP" }, { "NumberField", "InputFieldTMP" },
            { "PasswordField", "InputFieldTMP" }, { "CouponField", "InputFieldTMP" },
            { "CommentInput", "InputFieldTMP" }, { "SessionCode", "InputFieldTMP" },

            // Buttons
            { "Button", "ButtonTMP" }, { "IconButton", "ButtonTMP" }, { "Link", "ButtonTMP" },
            { "PurchaseButton", "ButtonTMP" }, { "FloatingActionButton", "ButtonTMP" },
            { "HoldButton", "ButtonTMP" }, { "RepeatButton", "ButtonTMP" },
            { "BackButton", "ButtonTMP" }, { "CloseButton", "ButtonTMP" },
            { "NavItem", "ButtonTMP" }, { "MenuItem", "ButtonTMP" },
            { "FollowButton", "ButtonTMP" }, { "AdRewardButton", "ButtonTMP" },

            // Scrolling
            { "ScrollArea", "ScrollView" },

            // Text
            { "Label", "TextTMP" }, { "Heading", "TextTMP" }, { "Caption", "TextTMP" },
            { "Subtitle", "TextTMP" }, { "Overline", "TextTMP" }, { "RichText", "TextTMP" },
            { "MarkdownText", "TextTMP" }, { "Quote", "TextTMP" }, { "CodeBlock", "TextTMP" },
            { "Timestamp", "TextTMP" }, { "CurrencyText", "TextTMP" }, { "PercentText", "TextTMP" },
            { "NumberTicker", "TextTMP" }, { "CountdownText", "TextTMP" }, { "SubtitleBar", "TextTMP" },

            // Images
            { "Image", "Image" }, { "Icon", "Image" }, { "Thumbnail", "Image" },
            { "CoverImage", "Image" }, { "PlaceholderImage", "Image" }, { "Logo", "Image" },
            { "NineSliceImage", "Image" }, { "MaskedImage", "Image" }, { "PortraitFrame", "Image" },
            { "Avatar", "Image" }, { "RarityFrame", "Image" }, { "QRCode", "Image" },
            { "CharacterPortrait", "Image" },

            // Raw textures
            { "RenderTextureView", "RawImage" }, { "VideoView", "RawImage" }, { "ModelView", "RawImage" },
            { "Minimap", "RawImage" },
        };

        /// <summary>Controls whose uGUI equivalent covers the component's core behaviour, not just its look.</summary>
        private static readonly HashSet<string> FullOnUGUI = new HashSet<string>
        {
            "Slider", "Checkbox", "Switch", "ToggleButton", "Dropdown", "ComboBox",
            "TextField", "TextArea", "SearchField", "NumberField", "PasswordField",
            "Button", "IconButton", "ScrollArea", "Label", "Image", "Icon"
        };

        public static void Apply(DesignerComponentDescriptor descriptor)
        {
            if (descriptor == null || descriptor.Family != DesignerComponentFamily.NexUI) return;
            if (!string.IsNullOrEmpty(descriptor.UGUIControl)) return;
            if (!UGUIControls.TryGetValue(descriptor.TypeId, out var control)) return;

            descriptor.UGUIControl = control;
            if (FullOnUGUI.Contains(descriptor.TypeId))
                descriptor.UGUISupport = DesignerBackendSupport.Full;
        }
    }
}

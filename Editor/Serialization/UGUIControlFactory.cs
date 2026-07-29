using emiteat.NexUI.Designer.Editor.Components;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Designer.Editor.Serialization
{
    /// <summary>
    /// Creates and updates the stock controls exposed by <see cref="UGUIComponentCatalog"/>.
    /// DefaultControls/TMP_DefaultControls are the same factories used by Unity's GameObject > UI
    /// menu, so newly generated prefab elements have the normal Unity hierarchy and references.
    /// </summary>
    internal static class UGUIControlFactory
    {
        private static DefaultControls.Resources _resources;
        private static TMP_DefaultControls.Resources _tmpResources;
        private static bool _resourcesLoaded;

        public static GameObject Create(DesignerElementMetadata element)
        {
            var descriptor = DesignerComponentRegistry.Get(element?.elementType);
            var control = descriptor.UGUIControl;
            GameObject go;

            switch (control)
            {
                case "Image": go = DefaultControls.CreateImage(Resources); break;
                case "RawImage": go = DefaultControls.CreateRawImage(Resources); break;
                case "Panel": go = DefaultControls.CreatePanel(Resources); break;
                case "Text": go = DefaultControls.CreateText(Resources); break;
                case "TextTMP": go = TMP_DefaultControls.CreateText(TmpResources); break;
                case "Button": go = DefaultControls.CreateButton(Resources); break;
                case "ButtonTMP": go = TMP_DefaultControls.CreateButton(TmpResources); break;
                case "Toggle": go = DefaultControls.CreateToggle(Resources); break;
                case "Slider": go = DefaultControls.CreateSlider(Resources); break;
                case "Scrollbar": go = DefaultControls.CreateScrollbar(Resources); break;
                case "Dropdown": go = DefaultControls.CreateDropdown(Resources); break;
                case "DropdownTMP": go = TMP_DefaultControls.CreateDropdown(TmpResources); break;
                case "InputField": go = DefaultControls.CreateInputField(Resources); break;
                case "InputFieldTMP": go = TMP_DefaultControls.CreateInputField(TmpResources); break;
                case "ScrollView": go = DefaultControls.CreateScrollView(Resources); break;
                default: go = CreateSimple(control); break;
            }

            go.name = element?.elementId ?? descriptor.DisplayName ?? "Element";
            return go;
        }

        public static Transform ContentParent(GameObject parent, string parentTypeId)
        {
            if (parent == null) return null;
            var control = DesignerComponentRegistry.Get(parentTypeId).UGUIControl;
            if (control == "ScrollView")
            {
                var viewport = parent.transform.Find("Viewport");
                var content = viewport != null ? viewport.Find("Content") : null;
                if (content != null) return content;
            }
            return parent.transform;
        }

        public static void MatchLayer(GameObject go, GameObject parent)
        {
            if (go == null || parent == null) return;
            SetLayerRecursively(go, parent.layer);
        }

        public static void EnsureAndApply(GameObject go, DesignerElementMetadata element, DesignerSaveReport report)
        {
            if (go == null || element == null) return;
            var control = DesignerComponentRegistry.Get(element.elementType).UGUIControl;
            if (string.IsNullOrEmpty(control)) return;

            EnsureRootComponent(go, control, report, element.elementId);
            var min = element.fill != null ? element.fill.minValue : 0f;
            var max = element.fill != null ? element.fill.maxValue : 100f;
            if (max <= min) max = min + 1f;

            switch (control)
            {
                case "Image":
                case "Panel":
                case "Mask":
                    var image = go.GetComponent<Image>();
                    if (image != null && control == "Image")
                    {
                        image.sprite = element.previewImage;
                        image.preserveAspect = element.previewImage != null;
                    }
                    break;
                case "RawImage":
                    var raw = go.GetComponent<RawImage>();
                    if (raw != null) raw.texture = element.previewImage != null ? element.previewImage.texture : null;
                    break;
                case "Toggle":
                    var toggle = go.GetComponent<Toggle>();
                    if (toggle != null) toggle.SetIsOnWithoutNotify(element.previewValue > min);
                    break;
                case "Slider":
                    var slider = go.GetComponent<Slider>();
                    if (slider != null)
                    {
                        slider.minValue = min;
                        slider.maxValue = max;
                        slider.SetValueWithoutNotify(Mathf.Clamp(element.previewValue, min, max));
                        slider.direction = SliderDirection(element.fill?.direction ?? DesignerFillDirection.LeftToRight);
                    }
                    break;
                case "Scrollbar":
                    var scrollbar = go.GetComponent<Scrollbar>();
                    if (scrollbar != null)
                    {
                        scrollbar.SetValueWithoutNotify(Mathf.InverseLerp(min, max, element.previewValue));
                        scrollbar.direction = ScrollbarDirection(element.fill?.direction ?? DesignerFillDirection.LeftToRight);
                    }
                    break;
                case "Dropdown":
                    ApplyOptions(go.GetComponent<Dropdown>(), element);
                    break;
                case "DropdownTMP":
                    ApplyOptions(go.GetComponent<TMP_Dropdown>(), element);
                    break;
                case "InputField":
                    var input = go.GetComponent<InputField>();
                    if (input != null && element.text != null) input.SetTextWithoutNotify(element.text);
                    break;
                case "InputFieldTMP":
                    var tmpInput = go.GetComponent<TMP_InputField>();
                    if (tmpInput != null && element.text != null) tmpInput.SetTextWithoutNotify(element.text);
                    break;
                case "ScrollView":
                    var scroll = go.GetComponent<ScrollRect>();
                    if (scroll != null) scroll.verticalNormalizedPosition = 1f - Mathf.InverseLerp(min, max, element.previewValue);
                    break;
            }
        }

        private static GameObject CreateSimple(string control)
        {
            var go = new GameObject(string.IsNullOrEmpty(control) ? "Element" : control, typeof(RectTransform));
            switch (control)
            {
                case "ToggleGroup": go.AddComponent<ToggleGroup>(); break;
                case "Mask":
                    go.AddComponent<Image>();
                    go.AddComponent<Mask>();
                    break;
                case "RectMask2D": go.AddComponent<RectMask2D>(); break;
                case "HorizontalLayoutGroup": go.AddComponent<HorizontalLayoutGroup>(); break;
                case "VerticalLayoutGroup": go.AddComponent<VerticalLayoutGroup>(); break;
                case "GridLayoutGroup": go.AddComponent<GridLayoutGroup>(); break;
                case "Canvas":
                    go.AddComponent<Canvas>();
                    go.AddComponent<GraphicRaycaster>();
                    break;
            }
            return go;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private static void EnsureRootComponent(GameObject go, string control, DesignerSaveReport report, string elementId)
        {
            System.Type required = control switch
            {
                "Image" => typeof(Image), "Panel" => typeof(Image), "RawImage" => typeof(RawImage),
                "Text" => typeof(Text), "TextTMP" => typeof(TextMeshProUGUI),
                "Button" => typeof(Button), "ButtonTMP" => typeof(Button), "Toggle" => typeof(Toggle),
                "ToggleGroup" => typeof(ToggleGroup), "Slider" => typeof(Slider), "Scrollbar" => typeof(Scrollbar),
                "Dropdown" => typeof(Dropdown), "DropdownTMP" => typeof(TMP_Dropdown),
                "InputField" => typeof(InputField), "InputFieldTMP" => typeof(TMP_InputField),
                "ScrollView" => typeof(ScrollRect), "Mask" => typeof(Mask), "RectMask2D" => typeof(RectMask2D),
                "HorizontalLayoutGroup" => typeof(HorizontalLayoutGroup),
                "VerticalLayoutGroup" => typeof(VerticalLayoutGroup), "GridLayoutGroup" => typeof(GridLayoutGroup),
                "Canvas" => typeof(Canvas), _ => null
            };
            if (required == null || go.GetComponent(required) != null) return;

            try
            {
                go.AddComponent(required);
                report.MarkChanged($"Added {required.Name} to '{elementId}'");
                if (control is "Button" or "ButtonTMP")
                {
                    if (go.GetComponent<Image>() == null) go.AddComponent<Image>();
                    go.GetComponent<Button>().targetGraphic = go.GetComponent<Graphic>();
                }
            }
            catch (System.Exception ex)
            {
                report.Warn($"Could not add {required.Name} to '{elementId}': {ex.Message}");
            }
        }

        private static void ApplyOptions(Dropdown dropdown, DesignerElementMetadata element)
        {
            if (dropdown == null || element.previewOptions == null || element.previewOptions.Count == 0) return;
            dropdown.ClearOptions();
            dropdown.AddOptions(element.previewOptions);
            dropdown.SetValueWithoutNotify(Mathf.Clamp(Mathf.RoundToInt(element.previewValue), 0, element.previewOptions.Count - 1));
            dropdown.RefreshShownValue();
        }

        private static void ApplyOptions(TMP_Dropdown dropdown, DesignerElementMetadata element)
        {
            if (dropdown == null || element.previewOptions == null || element.previewOptions.Count == 0) return;
            dropdown.ClearOptions();
            dropdown.AddOptions(element.previewOptions);
            dropdown.SetValueWithoutNotify(Mathf.Clamp(Mathf.RoundToInt(element.previewValue), 0, element.previewOptions.Count - 1));
            dropdown.RefreshShownValue();
        }

        private static Slider.Direction SliderDirection(DesignerFillDirection direction) => direction switch
        {
            DesignerFillDirection.RightToLeft => Slider.Direction.RightToLeft,
            DesignerFillDirection.BottomToTop => Slider.Direction.BottomToTop,
            DesignerFillDirection.TopToBottom => Slider.Direction.TopToBottom,
            _ => Slider.Direction.LeftToRight
        };

        private static Scrollbar.Direction ScrollbarDirection(DesignerFillDirection direction) => direction switch
        {
            DesignerFillDirection.RightToLeft => Scrollbar.Direction.RightToLeft,
            DesignerFillDirection.BottomToTop => Scrollbar.Direction.BottomToTop,
            DesignerFillDirection.TopToBottom => Scrollbar.Direction.TopToBottom,
            _ => Scrollbar.Direction.LeftToRight
        };

        private static DefaultControls.Resources Resources
        {
            get { LoadResources(); return _resources; }
        }

        private static TMP_DefaultControls.Resources TmpResources
        {
            get { LoadResources(); return _tmpResources; }
        }

        private static void LoadResources()
        {
            if (_resourcesLoaded) return;
            _resourcesLoaded = true;
            var standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            var input = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd");
            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
            var dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
            var mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd");
            _resources = new DefaultControls.Resources
            {
                standard = standard, background = background, inputField = input, knob = knob,
                checkmark = checkmark, dropdown = dropdown, mask = mask
            };
            _tmpResources = new TMP_DefaultControls.Resources
            {
                standard = standard, background = background, inputField = input, knob = knob,
                checkmark = checkmark, dropdown = dropdown, mask = mask
            };
        }
    }
}

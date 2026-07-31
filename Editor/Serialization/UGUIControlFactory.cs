using System.Collections.Generic;
using emiteat.NexUI.Components;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Integrations.UGUI;
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
            var go = CreateByControl(control);

            go.name = element?.elementId ?? descriptor.DisplayName ?? "Element";
            return go;
        }

        public static Transform ContentParent(GameObject parent, string parentTypeId)
        {
            if (parent == null) return null;
            var control = DesignerComponentRegistry.Get(parentTypeId).UGUIControl;
            if (control == "ScrollView" || control == "CollectionView")
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
                    if (toggle != null)
                    {
                        toggle.SetIsOnWithoutNotify(element.previewValue > min);
                        // An authored Toggle child of a Toggle Group behaves like Unity's normal
                        // hierarchy immediately; no string key or manual prefab wiring required.
                        toggle.group = go.transform.parent != null
                            ? go.transform.parent.GetComponentInParent<ToggleGroup>()
                            : null;
                    }
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

                case "CollectionView":
                    ApplyCollection(go, element, report);
                    break;
            }

            ApplyComponentProperties(go, element, control, report);
            ApplyPartOverrides(go, element, report);
        }

        private static void ApplyPartOverrides(GameObject go, DesignerElementMetadata element,
            DesignerSaveReport report)
        {
            var descriptor = DesignerComponentRegistry.Get(element.elementType);
            if (descriptor.Parts.Count == 0) return;
            element.componentPartOverrides ??= new List<DesignerComponentPartOverrideMetadata>();

            foreach (var part in descriptor.Parts)
            {
                var value = DesignerComponentPartOverrideBag.Find(element.componentPartOverrides, part.PartId);
                if (part.PreviewOnly || part.UGUIPath == null)
                {
                    if (value != null && value.HasAnyOverride)
                        report.MarkPreviewOnly("Component part transform",
                            $"'{element.elementId}/{part.DisplayName}' is preview-only for uGUI; its metadata was preserved.",
                            element.elementId);
                    continue;
                }

                var target = string.IsNullOrEmpty(part.UGUIPath)
                    ? go.transform as RectTransform
                    : go.transform.Find(part.UGUIPath) as RectTransform;
                if (target == null)
                {
                    if (value != null && value.HasAnyOverride)
                        report.MarkSkipped($"'{element.elementId}' has no uGUI part at '{part.UGUIPath}' for {part.DisplayName}.");
                    continue;
                }

                var baseline = target.GetComponent<DesignerUGUIPartBaselineTag>();
                if (value == null || !value.HasAnyOverride)
                {
                    if (baseline != null && baseline.ownerStableId == element.stableId && baseline.partId == part.PartId)
                    {
                        baseline.Restore(target);
                        Object.DestroyImmediate(baseline);
                        report.MarkChanged($"Reset component part '{element.elementId}/{part.DisplayName}'");
                    }
                    continue;
                }

                if (baseline == null)
                {
                    baseline = target.gameObject.AddComponent<DesignerUGUIPartBaselineTag>();
                    baseline.Capture(target, element.stableId, part.PartId);
                }
                else if (baseline.ownerStableId != element.stableId || baseline.partId != part.PartId)
                {
                    baseline.Capture(target, element.stableId, part.PartId);
                }

                baseline.Restore(target);
                if (value.hasPosition)
                    target.anchoredPosition = baseline.anchoredPosition + new Vector2(value.position.x, -value.position.y);
                if (value.hasSizeDelta)
                    target.sizeDelta = baseline.sizeDelta + value.sizeDelta;
                if (value.hasRotation)
                    target.localEulerAngles = baseline.localEulerAngles + new Vector3(0f, 0f, value.rotation);
                if (value.hasScale)
                    target.localScale = Vector3.Scale(baseline.localScale, new Vector3(value.scale.x, value.scale.y, 1f));
                if (value.hasVisibility)
                    target.gameObject.SetActive(value.visible);
                report.MarkChanged($"Applied component part '{element.elementId}/{part.DisplayName}'");
            }

            foreach (var value in element.componentPartOverrides)
                if (value != null && value.HasAnyOverride && descriptor.GetPart(value.partId) == null)
                    report.Warn($"'{element.elementId}' preserves an unknown component part override '{value.partId}'.");
        }

        /// <summary>
        /// Writes the component's authored properties onto the real Unity components. Only properties
        /// the user actually set are considered, and anything uGUI cannot express is named in the Save
        /// Report rather than dropped silently - the same honesty rule the rest of the save path uses.
        /// </summary>
        private static void ApplyComponentProperties(GameObject go, DesignerElementMetadata element,
            string control, DesignerSaveReport report)
        {
            var id = element.elementId;

            // Every Selectable-backed control shares interactability.
            var selectable = go.GetComponent<Selectable>();
            if (selectable != null && Overridden(element, "interactable"))
                selectable.interactable = Get.Bool(element, "interactable", true);
            if (selectable != null && Overridden(element, "transition"))
                selectable.transition = SelectableTransition(Get.EnumName(element, "transition"));
            if (selectable != null && Overridden(element, "navigation.enabled"))
            {
                var navigation = selectable.navigation;
                navigation.mode = Get.Bool(element, "navigation.enabled", true)
                    ? Navigation.Mode.Automatic
                    : Navigation.Mode.None;
                selectable.navigation = navigation;
            }

            if (Overridden(element, "clipContent"))
            {
                var shouldClip = Get.Bool(element, "clipContent");
                var mask = go.GetComponent<RectMask2D>();
                if (shouldClip && mask == null)
                {
                    go.AddComponent<RectMask2D>();
                    report.MarkChanged($"Enabled content clipping on '{id}'");
                }
                else if (!shouldClip && mask != null)
                    report.MarkUserImpact("Content clipping", $"'{id}' already has a RectMask2D; it was preserved instead of removing a possibly user-authored component.", id);
            }

            switch (control)
            {
                case "Slider":
                {
                    var slider = go.GetComponent<Slider>();
                    if (slider == null) break;
                    if (Overridden(element, "value.min")) slider.minValue = Get.Float(element, "value.min", slider.minValue);
                    if (Overridden(element, "value.max")) slider.maxValue = Get.Float(element, "value.max", slider.maxValue);
                    if (slider.maxValue <= slider.minValue) slider.maxValue = slider.minValue + 1f;
                    if (Overridden(element, "value.wholeNumbers")) slider.wholeNumbers = Get.Bool(element, "value.wholeNumbers");
                    if (Overridden(element, "value.direction"))
                        slider.direction = SliderDirection(FillDirectionOf(Get.EnumName(element, "value.direction")));
                    if (Overridden(element, "slider.step"))
                        report.MarkUnsupported("Slider step", $"'{id}' step snapping has no stock uGUI equivalent; the value was written but snapping is the runtime's job.", id);
                    break;
                }
                case "Toggle":
                {
                    var toggle = go.GetComponent<Toggle>();
                    if (toggle != null && Overridden(element, "toggle.isOn"))
                        toggle.SetIsOnWithoutNotify(Get.Bool(element, "toggle.isOn"));
                    if (Overridden(element, "toggle.allowIndeterminate"))
                        report.MarkUnsupported("Indeterminate toggle", $"'{id}' has no third state on stock uGUI Toggle.", id);
                    if (Overridden(element, "toggle.group") && !string.IsNullOrWhiteSpace(Get.String(element, "toggle.group")))
                        report.MarkUnsupported("Toggle group key", $"'{id}' stores group '{Get.String(element, "toggle.group")}', but stock uGUI needs a scene ToggleGroup reference.", id);
                    break;
                }
                case "Dropdown":
                case "DropdownTMP":
                {
                    var options = Overridden(element, "choice.options")
                        ? SplitOptions(Get.String(element, "choice.options"))
                        : new List<string>();
                    if (options.Count > 0)
                    {
                        if (go.GetComponent<Dropdown>() is { } legacy)
                        {
                            legacy.ClearOptions();
                            legacy.AddOptions(options);
                            legacy.SetValueWithoutNotify(Mathf.Clamp(Get.Int(element, "choice.value"), 0, options.Count - 1));
                        }
                        if (go.GetComponent<TMP_Dropdown>() is { } tmp)
                        {
                            tmp.ClearOptions();
                            tmp.AddOptions(options);
                            tmp.SetValueWithoutNotify(Mathf.Clamp(Get.Int(element, "choice.value"), 0, options.Count - 1));
                        }
                        report.MarkChanged($"Applied {options.Count} dropdown options to '{id}'");
                    }
                    else if (Overridden(element, "choice.value"))
                    {
                        if (go.GetComponent<Dropdown>() is { } legacy && legacy.options.Count > 0)
                            legacy.SetValueWithoutNotify(Mathf.Clamp(Get.Int(element, "choice.value"), 0, legacy.options.Count - 1));
                        if (go.GetComponent<TMP_Dropdown>() is { } tmp && tmp.options.Count > 0)
                            tmp.SetValueWithoutNotify(Mathf.Clamp(Get.Int(element, "choice.value"), 0, tmp.options.Count - 1));
                    }
                    if (Overridden(element, "choice.searchable"))
                        report.MarkUnsupported("Searchable dropdown", $"'{id}' search has no stock uGUI Dropdown equivalent.", id);
                    break;
                }
                case "InputField":
                {
                    var input = go.GetComponent<InputField>();
                    if (input == null) break;
                    if (Overridden(element, "input.maxLength")) input.characterLimit = Get.Int(element, "input.maxLength");
                    if (Overridden(element, "input.readOnly")) input.readOnly = Get.Bool(element, "input.readOnly");
                    if (Overridden(element, "input.contentType"))
                        input.contentType = LegacyContentType(Get.EnumName(element, "input.contentType"));
                    if (Overridden(element, "input.lineType"))
                        input.lineType = LegacyLineType(Get.EnumName(element, "input.lineType"));
                    ApplyPlaceholder(input.placeholder, element, report, id);
                    break;
                }
                case "InputFieldTMP":
                {
                    var input = go.GetComponent<TMP_InputField>();
                    if (input == null) break;
                    if (Overridden(element, "input.maxLength")) input.characterLimit = Get.Int(element, "input.maxLength");
                    if (Overridden(element, "input.readOnly")) input.readOnly = Get.Bool(element, "input.readOnly");
                    if (Overridden(element, "input.contentType"))
                        input.contentType = TmpContentType(Get.EnumName(element, "input.contentType"));
                    if (Overridden(element, "input.lineType"))
                        input.lineType = TmpLineType(Get.EnumName(element, "input.lineType"));
                    ApplyPlaceholder(input.placeholder, element, report, id);
                    break;
                }
                case "ScrollView":
                {
                    var scroll = go.GetComponent<ScrollRect>();
                    if (scroll == null) break;
                    if (Overridden(element, "scroll.horizontal")) scroll.horizontal = Get.Bool(element, "scroll.horizontal");
                    if (Overridden(element, "scroll.vertical")) scroll.vertical = Get.Bool(element, "scroll.vertical", true);
                    if (Overridden(element, "scroll.movement"))
                        scroll.movementType = Get.EnumName(element, "scroll.movement") switch
                        {
                            "Unrestricted" => ScrollRect.MovementType.Unrestricted,
                            "Clamped" => ScrollRect.MovementType.Clamped,
                            _ => ScrollRect.MovementType.Elastic
                        };
                    if (Overridden(element, "scroll.elasticity")) scroll.elasticity = Get.Float(element, "scroll.elasticity", 0.1f);
                    if (Overridden(element, "scroll.inertia")) scroll.inertia = Get.Bool(element, "scroll.inertia", true);
                    if (Overridden(element, "scroll.decelerationRate")) scroll.decelerationRate = Get.Float(element, "scroll.decelerationRate", 0.135f);
                    if (Overridden(element, "scroll.sensitivity")) scroll.scrollSensitivity = Get.Float(element, "scroll.sensitivity", 1f);
                    ApplyScrollbarVisibility(scroll, element);
                    break;
                }
                case "Image":
                case "Panel":
                case "Mask":
                case "RawImage":
                {
                    var graphic = go.GetComponent<Graphic>();
                    if (graphic == null) break;
                    if (Overridden(element, "media.raycastTarget")) graphic.raycastTarget = Get.Bool(element, "media.raycastTarget", true);
                    // maskable lives on MaskableGraphic, not on Graphic itself.
                    if (Overridden(element, "media.maskable") && graphic is MaskableGraphic maskable)
                        maskable.maskable = Get.Bool(element, "media.maskable", true);
                    if (Overridden(element, "media.tint")) graphic.color = Get.Color(element, "media.tint");
                    if (graphic is Image image)
                    {
                        if (Overridden(element, "media.sprite"))
                            image.sprite = Get.Asset(element, "media.sprite") as Sprite;
                        if (Overridden(element, "media.preserveAspect")) image.preserveAspect = Get.Bool(element, "media.preserveAspect", true);
                    }
                    break;
                }
                case "Text":
                {
                    var text = go.GetComponent<Text>();
                    if (text == null) break;
                    if (Overridden(element, "text.richText")) text.supportRichText = Get.Bool(element, "text.richText", true);
                    if (Overridden(element, "text.autoSize")) text.resizeTextForBestFit = Get.Bool(element, "text.autoSize");
                    if (Overridden(element, "text.maxLines") && Get.Int(element, "text.maxLines") > 0)
                        report.MarkUnsupported("Maximum text lines", $"'{id}' uses max lines, which legacy uGUI Text cannot enforce directly.", id);
                    break;
                }
                case "TextTMP":
                {
                    var text = go.GetComponent<TextMeshProUGUI>();
                    if (text == null) break;
                    if (Overridden(element, "text.richText")) text.richText = Get.Bool(element, "text.richText", true);
                    if (Overridden(element, "text.autoSize")) text.enableAutoSizing = Get.Bool(element, "text.autoSize");
                    if (Overridden(element, "text.maxLines")) text.maxVisibleLines = Mathf.Max(0, Get.Int(element, "text.maxLines"));
                    break;
                }
            }

            // Value components that are not a stock control still map onto a filled Image.
            if (string.IsNullOrEmpty(control) && Overridden(element, "value.segments") && Get.Int(element, "value.segments") > 0)
                report.MarkPreviewOnly("Segmented fill", $"'{id}' segmented fill is drawn on the canvas only; stock uGUI has no segmented Image.", id);
        }

        private static void ApplyPlaceholder(Graphic placeholder, DesignerElementMetadata element,
            DesignerSaveReport report, string id)
        {
            if (!Overridden(element, "input.placeholder")) return;
            var text = Get.String(element, "input.placeholder");
            switch (placeholder)
            {
                case TMP_Text tmp: tmp.text = text; break;
                case Text legacy: legacy.text = text; break;
                default:
                    report.MarkSkipped($"'{id}' has no placeholder object to write its placeholder text to.");
                    return;
            }
            report.MarkChanged($"Applied placeholder text to '{id}'");
        }

        private static void ApplyScrollbarVisibility(ScrollRect scroll, DesignerElementMetadata element)
        {
            if (Overridden(element, "scroll.verticalBar"))
            {
                var mode = Get.EnumName(element, "scroll.verticalBar");
                if (scroll.verticalScrollbar != null) scroll.verticalScrollbar.gameObject.SetActive(mode != "Hidden");
                if (mode != "Hidden") scroll.verticalScrollbarVisibility = ScrollbarVisibility(mode);
            }
            if (Overridden(element, "scroll.horizontalBar"))
            {
                var mode = Get.EnumName(element, "scroll.horizontalBar");
                if (scroll.horizontalScrollbar != null) scroll.horizontalScrollbar.gameObject.SetActive(mode != "Hidden");
                if (mode != "Hidden") scroll.horizontalScrollbarVisibility = ScrollbarVisibility(mode);
            }
        }

        private static ScrollRect.ScrollbarVisibility ScrollbarVisibility(string mode) => mode switch
        {
            "AlwaysVisible" => ScrollRect.ScrollbarVisibility.Permanent,
            "Hidden" => ScrollRect.ScrollbarVisibility.AutoHide,
            _ => ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport
        };

        private static Selectable.Transition SelectableTransition(string name) => name switch
        {
            "None" => Selectable.Transition.None,
            "SpriteSwap" => Selectable.Transition.SpriteSwap,
            "Animation" => Selectable.Transition.Animation,
            _ => Selectable.Transition.ColorTint
        };

        private static InputField.ContentType LegacyContentType(string name) => name switch
        {
            "IntegerNumber" => InputField.ContentType.IntegerNumber,
            "DecimalNumber" => InputField.ContentType.DecimalNumber,
            "Alphanumeric" => InputField.ContentType.Alphanumeric,
            "Name" => InputField.ContentType.Name,
            "EmailAddress" => InputField.ContentType.EmailAddress,
            "Password" => InputField.ContentType.Password,
            "Pin" => InputField.ContentType.Pin,
            "Custom" => InputField.ContentType.Custom,
            _ => InputField.ContentType.Standard
        };

        private static InputField.LineType LegacyLineType(string name) => name switch
        {
            "MultiLineSubmit" => InputField.LineType.MultiLineSubmit,
            "MultiLineNewline" => InputField.LineType.MultiLineNewline,
            _ => InputField.LineType.SingleLine
        };

        private static TMP_InputField.ContentType TmpContentType(string name) => name switch
        {
            "IntegerNumber" => TMP_InputField.ContentType.IntegerNumber,
            "DecimalNumber" => TMP_InputField.ContentType.DecimalNumber,
            "Alphanumeric" => TMP_InputField.ContentType.Alphanumeric,
            "Name" => TMP_InputField.ContentType.Name,
            "EmailAddress" => TMP_InputField.ContentType.EmailAddress,
            "Password" => TMP_InputField.ContentType.Password,
            "Pin" => TMP_InputField.ContentType.Pin,
            "Custom" => TMP_InputField.ContentType.Custom,
            _ => TMP_InputField.ContentType.Standard
        };

        private static TMP_InputField.LineType TmpLineType(string name) => name switch
        {
            "MultiLineSubmit" => TMP_InputField.LineType.MultiLineSubmit,
            "MultiLineNewline" => TMP_InputField.LineType.MultiLineNewline,
            _ => TMP_InputField.LineType.SingleLine
        };

        private static DesignerFillDirection FillDirectionOf(string name) => name switch
        {
            "RightToLeft" => DesignerFillDirection.RightToLeft,
            "BottomToTop" => DesignerFillDirection.BottomToTop,
            "TopToBottom" => DesignerFillDirection.TopToBottom,
            _ => DesignerFillDirection.LeftToRight
        };

        private static List<string> SplitOptions(string csv)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(csv)) return result;
            foreach (var part in csv.Split(','))
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 0) result.Add(trimmed);
            }
            return result;
        }

        private static bool Overridden(DesignerElementMetadata element, string key)
            => DesignerComponentPropertyAccess.IsOverridden(element, key);

        /// <summary>Short alias so the mapping switch above stays readable.</summary>
        private static class Get
        {
            public static float Float(DesignerElementMetadata e, string key, float fallback = 0f)
                => DesignerComponentPropertyAccess.GetFloat(e, key, fallback);
            public static int Int(DesignerElementMetadata e, string key, int fallback = 0)
                => DesignerComponentPropertyAccess.GetInt(e, key, fallback);
            public static bool Bool(DesignerElementMetadata e, string key, bool fallback = false)
                => DesignerComponentPropertyAccess.GetBool(e, key, fallback);
            public static string String(DesignerElementMetadata e, string key, string fallback = "")
                => DesignerComponentPropertyAccess.GetString(e, key, fallback);
            public static Color Color(DesignerElementMetadata e, string key)
                => DesignerComponentPropertyAccess.GetColor(e, key);
            public static Object Asset(DesignerElementMetadata e, string key)
                => DesignerComponentPropertyAccess.GetAsset(e, key);
            public static string EnumName(DesignerElementMetadata e, string key)
                => DesignerComponentPropertyAccess.GetEnum(e, key);
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

        private static GameObject CreateByControl(string control)
        {
            switch (control)
            {
                case "Image": return DefaultControls.CreateImage(Resources);
                case "RawImage": return DefaultControls.CreateRawImage(Resources);
                case "Panel": return DefaultControls.CreatePanel(Resources);
                case "Text": return DefaultControls.CreateText(Resources);
                case "TextTMP": return TMP_DefaultControls.CreateText(TmpResources);
                case "Button": return DefaultControls.CreateButton(Resources);
                case "ButtonTMP": return TMP_DefaultControls.CreateButton(TmpResources);
                case "Toggle": return DefaultControls.CreateToggle(Resources);
                case "Slider": return DefaultControls.CreateSlider(Resources);
                case "Scrollbar": return DefaultControls.CreateScrollbar(Resources);
                case "Dropdown": return DefaultControls.CreateDropdown(Resources);
                case "DropdownTMP": return TMP_DefaultControls.CreateDropdown(TmpResources);
                case "InputField": return DefaultControls.CreateInputField(Resources);
                case "InputFieldTMP": return TMP_DefaultControls.CreateInputField(TmpResources);
                case "ScrollView": return DefaultControls.CreateScrollView(Resources);
                case "CollectionView": return CreateCollectionView();
                default: return CreateSimple(control);
            }
        }

        /// <summary>
        /// A collection is Unity's own ScrollView plus <see cref="NXCollectionView"/> on the root.
        /// </summary>
        /// <remarks>
        /// Built from <c>DefaultControls.CreateScrollView</c> rather than assembled by hand, so the
        /// result is the hierarchy every Unity user already knows - Viewport/Content, a working
        /// mask, real scrollbars - and any project code that expects a ScrollRect keeps working.
        /// The content is anchored to the top-left because the collection positions items itself.
        /// </remarks>
        private static GameObject CreateCollectionView()
        {
            var go = DefaultControls.CreateScrollView(Resources);
            var scroll = go.GetComponent<ScrollRect>();
            if (scroll != null && scroll.content != null)
            {
                var content = scroll.content;
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0f, 1f);
                content.sizeDelta = Vector2.zero;
                content.anchoredPosition = Vector2.zero;
            }
            if (go.GetComponent<NXCollectionView>() == null) go.AddComponent<NXCollectionView>();
            return go;
        }

        /// <summary>
        /// Writes the authored collection settings onto the prefab's runtime component, and points it
        /// at the authored item template and state roots.
        /// </summary>
        private static void ApplyCollection(GameObject go, DesignerElementMetadata element, DesignerSaveReport report)
        {
            var view = go.GetComponent<NXCollectionView>();
            if (view == null) return;

            var options = DesignerCollectionOptions.Read(element);
            view.ApplyAuthoredOptions(options);

            var problems = new List<string>();
            if (!options.Validate(problems))
                foreach (var problem in problems)
                    report.MarkPreviewOnly("Collection options", $"'{element.elementId}': {problem}", element.elementId);

            // The template is whichever child the Designer placed in the template slot. It is left
            // inactive so the pool clones it rather than showing it as a real row.
            var scroll = go.GetComponent<ScrollRect>();
            var content = scroll != null ? scroll.content : null;
            if (content == null) return;

            var template = FindTemplateChild(content);
            if (template != null)
            {
                view.ItemTemplate = template;
                template.gameObject.SetActive(false);
            }
            else
            {
                report.MarkSkipped($"'{element.elementId}' has no item template child, so the collection " +
                                   "will run with no rows. Add a child in the Item Template slot.");
            }

            view.SetStateViews(FindChild(go.transform, "loading"), FindChild(go.transform, "empty"),
                FindChild(go.transform, "error"));
            EditorUtility.SetDirty(view);
        }

        /// <summary>
        /// The first child of Content is the template: the Designer writes exactly one element into
        /// the template slot, and Validation reports it when there is more than one.
        /// </summary>
        private static RectTransform FindTemplateChild(RectTransform content)
            => content.childCount > 0 ? content.GetChild(0) as RectTransform : null;

        private static GameObject FindChild(Transform root, string name)
        {
            var child = root.Find(name);
            return child != null ? child.gameObject : null;
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
                "ScrollView" => typeof(ScrollRect), "CollectionView" => typeof(NXCollectionView),
                "Mask" => typeof(Mask), "RectMask2D" => typeof(RectMask2D),
                "HorizontalLayoutGroup" => typeof(HorizontalLayoutGroup),
                "VerticalLayoutGroup" => typeof(VerticalLayoutGroup), "GridLayoutGroup" => typeof(GridLayoutGroup),
                "Canvas" => typeof(Canvas), _ => null
            };
            if (required == null || go.GetComponent(required) != null) return;

            GameObject template = null;
            try
            {
                // Screens authored before stock-control mapping contain plain RectTransforms.
                // Adding only Slider/Dropdown/etc. would leave their serialized child references
                // null. Transplant the same hierarchy Unity's GameObject > UI menu creates while
                // preserving every existing component and authored child on the matched object.
                template = CreateByControl(control);
                foreach (var source in template.GetComponents<Component>())
                {
                    if (source == null || source is Transform || source.GetType() == required) continue;
                    if (go.GetComponent(source.GetType()) != null) continue;
                    var added = go.AddComponent(source.GetType());
                    EditorUtility.CopySerialized(source, added);
                }

                if ((control == "Button" || control == "ButtonTMP") &&
                    (go.GetComponentInChildren<TMP_Text>(true) != null || go.GetComponentInChildren<Text>(true) != null))
                {
                    while (template.transform.childCount > 0)
                        Object.DestroyImmediate(template.transform.GetChild(0).gameObject);
                }
                else
                {
                    while (template.transform.childCount > 0)
                        template.transform.GetChild(0).SetParent(go.transform, false);
                }

                var target = go.AddComponent(required);
                var sourceRequired = template.GetComponent(required);
                if (sourceRequired != null) EditorUtility.CopySerialized(sourceRequired, target);
                RepairRootGraphicReference(go, template, target);
                SetLayerRecursively(go, go.layer);
                report.MarkChanged($"Upgraded '{elementId}' to a complete {required.Name} control hierarchy");
            }
            catch (System.Exception ex)
            {
                report.Warn($"Could not add {required.Name} to '{elementId}': {ex.Message}");
            }
            finally
            {
                if (template != null) Object.DestroyImmediate(template);
            }
        }

        private static void RepairRootGraphicReference(GameObject targetRoot, GameObject templateRoot, Component component)
        {
            if (!(component is Selectable selectable) || selectable.targetGraphic == null) return;
            if (selectable.targetGraphic.gameObject != templateRoot) return;
            var graphicType = selectable.targetGraphic.GetType();
            selectable.targetGraphic = targetRoot.GetComponent(graphicType) as Graphic ?? targetRoot.GetComponent<Graphic>();
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

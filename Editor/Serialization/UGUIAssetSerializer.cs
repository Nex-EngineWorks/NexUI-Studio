using System.Collections.Generic;
using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Integrations.UGUI;
using emiteat.NexUI.Designer.Editor.Properties;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Designer.Editor.Serialization
{
    /// <summary>
    /// Persists Designer metadata for a uGUI (prefab-based) screen. Metadata is always
    /// saved to its own asset. When the screen's backend asset is a prefab, the serializer
    /// also applies Designer-owned layout / text / tint / component data to the prefab using
    /// the safe LoadPrefabContents → SaveAsPrefabAsset → UnloadPrefabContents pattern so
    /// existing references and user-authored content are preserved.
    /// </summary>
    public sealed class UGUIAssetSerializer : IDesignerAssetSerializer
    {
        public DesignerSaveReport Save(UIScreenDefinition definition, DesignerMetadataAsset metadata)
        {
            var report = new DesignerSaveReport();

            if (metadata != null)
            {
                DesignerMetadataUtility.MarkDirty(metadata);
                report.MarkChanged("Designer metadata asset");
            }
            if (definition != null)
                DesignerMetadataUtility.MarkDirty(definition);

            var prefab = definition != null ? definition.backendAsset.asset as GameObject : null;
            if (prefab == null)
            {
                report.MarkSkipped("No uGUI prefab assigned to the screen backend asset (metadata saved only).");
                SaveDirtyAssets(metadata, definition);
                return report;
            }

            var path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
            {
                report.Warn($"Backend asset '{prefab.name}' is not a prefab asset; prefab changes were skipped (metadata saved only).");
                SaveDirtyAssets(metadata, definition);
                return report;
            }

            if (metadata == null || metadata.elements.Count == 0)
            {
                report.MarkSkipped("No metadata elements to apply to prefab.");
                SaveDirtyAssets(metadata, definition);
                return report;
            }

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                ApplyMetadata(root, metadata, report);
                if (!report.HasErrors)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    report.MarkChanged($"Prefab '{System.IO.Path.GetFileName(path)}'");
                }
                else
                {
                    report.MarkSkipped($"Prefab '{System.IO.Path.GetFileName(path)}' was not written because identity validation failed.");
                }
            }
            catch (System.Exception e)
            {
                report.Error($"Failed to write prefab: {e.Message}");
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }

            SaveDirtyAssets(metadata, definition);
            return report;
        }

        private static void SaveDirtyAssets(DesignerMetadataAsset metadata, UIScreenDefinition definition)
        {
            if (metadata != null) AssetDatabase.SaveAssetIfDirty(metadata);
            if (definition != null) AssetDatabase.SaveAssetIfDirty(definition);
        }

        private static void ApplyMetadata(GameObject root, DesignerMetadataAsset metadata, DesignerSaveReport report)
        {
            var prefabIndex = BuildPrefabIndex(root, report);
            var usedObjects = new HashSet<GameObject>();
            var metadataStableIds = new HashSet<string>();
            var invalidStableIds = new HashSet<string>();
            var metadataElementIds = new HashSet<string>();
            var invalidElementIds = new HashSet<string>();
            foreach (var element in metadata.elements)
            {
                if (element == null || string.IsNullOrEmpty(element.elementId)) continue;
                if (!metadataElementIds.Add(element.elementId)) invalidElementIds.Add(element.elementId);
                if (string.IsNullOrEmpty(element.stableId))
                    report.Error($"Element '{element.elementId}' has no stableId; run metadata migration before saving.");
                else if (!metadataStableIds.Add(element.stableId))
                    invalidStableIds.Add(element.stableId);
            }
            foreach (var id in invalidStableIds)
                report.Error($"Duplicate metadata stableId '{id}'. No element with that identity was applied.");
            foreach (var id in invalidElementIds)
                report.Error($"Duplicate metadata elementId '{id}'. No element with that public id was applied.");
            if (report.HasErrors)
            {
                ReportOrphans(root, usedObjects, report);
                return;
            }

            // Pass 1: ensure every element exists so parents resolve regardless of order.
            var objects = new Dictionary<string, GameObject>();
            foreach (var element in metadata.elements)
            {
                if (element == null || string.IsNullOrEmpty(element.elementId)) continue;
                if (string.IsNullOrEmpty(element.stableId) || invalidStableIds.Contains(element.stableId) ||
                    invalidElementIds.Contains(element.elementId)) continue;
                if (prefabIndex.AmbiguousStableIds.Contains(element.stableId))
                {
                    report.Error($"Element '{element.elementId}' cannot be matched because stableId '{element.stableId}' is duplicated in the prefab.");
                    continue;
                }

                var go = ResolveExisting(prefabIndex, element);
                var created = false;
                if (go != null && usedObjects.Contains(go))
                {
                    report.Error($"Prefab object '{go.name}' matched more than one metadata element; no additional changes were applied.");
                    continue;
                }
                if (go == null)
                {
                    go = UGUIControlFactory.Create(element);
                    go.transform.SetParent(root.transform, false);
                    UGUIControlFactory.MatchLayer(go, root);
                    created = true;
                    report.MarkCreated("Prefab element", $"Created element '{element.elementId}'", element.elementId);
                }

                var tag = go.GetComponent<NxUGuiBindingTag>();
                if (tag == null)
                {
                    tag = go.AddComponent<NxUGuiBindingTag>();
                    tag.ownership = created ? NexUIElementOwnership.DesignerOwned : NexUIElementOwnership.UserOwned;
                    report.MarkChanged($"Added stable identity tag to '{element.elementId}'");
                }
                else if (!string.IsNullOrEmpty(tag.stableId) && tag.stableId != element.stableId)
                {
                    report.Error($"'{go.name}' is already bound to stableId '{tag.stableId}', so '{element.elementId}' was not applied.");
                    continue;
                }

                tag.stableId = element.stableId;
                tag.elementId = element.elementId;
                if (tag.ownership == NexUIElementOwnership.Unknown)
                    tag.ownership = created ? NexUIElementOwnership.DesignerOwned : NexUIElementOwnership.UserOwned;
                if (tag.ownership == NexUIElementOwnership.DesignerOwned && go.name != element.elementId)
                {
                    go.name = element.elementId;
                    report.MarkChanged($"Renamed Designer-owned object to '{element.elementId}'");
                }
                objects[element.elementId] = go;
                usedObjects.Add(go);
            }

            // Pass 2: parent + apply properties.
            foreach (var element in metadata.elements)
            {
                if (element == null || string.IsNullOrEmpty(element.elementId)) continue;
                if (!objects.TryGetValue(element.elementId, out var go) || go == null) continue;

                if (!string.IsNullOrEmpty(element.parentId) &&
                    objects.TryGetValue(element.parentId, out var parent) && parent != null &&
                    go.transform.parent != parent.transform)
                {
                    go.transform.SetParent(UGUIControlFactory.ContentParent(parent,
                        metadata.Find(element.parentId)?.elementType), false);
                }

                // Element rects are stored in absolute canvas space; convert to the parent-relative
                // local position so a child's anchoredPosition is correct once re-parented (and so
                // moving a parent carries its children, matching the Designer canvas).
                var local = DesignerCoordinateUtility.GetLocalPosition(metadata, element);
                ApplyRect(go, element, local);
                ApplyTypedLayout(go, element, report);
                ApplyAutoLayout(go, element, report);
                go.SetActive(element.runtimeVisible);
                ApplyVisualAndText(go, element, report);
                UGUIControlFactory.EnsureAndApply(go, element, report);
                ApplyAttachedComponents(go, element, report);
            }

            // Pass 3: reflect Designer sibling order onto the transform (SetSiblingIndex), so the
            // saved prefab's child order matches the hierarchy panel / draw order.
            foreach (var element in metadata.elements)
            {
                if (element == null || string.IsNullOrEmpty(element.elementId)) continue;
                if (!objects.TryGetValue(element.elementId, out var go) || go == null) continue;
                var ordered = DesignerHierarchyUtility.GetOrderedChildren(metadata, element.parentId);
                var index = ordered.IndexOf(element);
                if (index >= 0 && index < go.transform.parent.childCount)
                    go.transform.SetSiblingIndex(index);
            }

            ReportOrphans(root, usedObjects, report);
        }

        private static void ReportOrphans(GameObject root, HashSet<GameObject> usedObjects, DesignerSaveReport report)
        {
            foreach (var tag in root.GetComponentsInChildren<NxUGuiBindingTag>(includeInactive: true))
                if (tag.ownership == NexUIElementOwnership.DesignerOwned && !usedObjects.Contains(tag.gameObject))
                    report.MarkOrphan("Prefab element", $"Orphaned Designer-owned object '{tag.name}' was preserved. Remove it manually after confirming it is unused.", tag.elementId);
        }

        private sealed class PrefabIndex
        {
            public readonly Dictionary<string, GameObject> StableIds = new Dictionary<string, GameObject>();
            public readonly HashSet<string> AmbiguousStableIds = new HashSet<string>();
            public readonly Dictionary<string, GameObject> ElementIds = new Dictionary<string, GameObject>();
            public readonly HashSet<string> AmbiguousElementIds = new HashSet<string>();
            public readonly Dictionary<string, GameObject> Names = new Dictionary<string, GameObject>();
            public readonly HashSet<string> AmbiguousNames = new HashSet<string>();
        }

        private static PrefabIndex BuildPrefabIndex(GameObject root, DesignerSaveReport report)
        {
            var index = new PrefabIndex();
            var stack = new Stack<Transform>();
            stack.Push(root.transform);
            while (stack.Count > 0)
            {
                var transform = stack.Pop();
                AddUnique(index.Names, index.AmbiguousNames, transform.name, transform.gameObject);
                var tag = transform.GetComponent<NxUGuiBindingTag>();
                if (tag != null)
                {
                    AddUnique(index.StableIds, index.AmbiguousStableIds, tag.stableId, transform.gameObject);
                    AddUnique(index.ElementIds, index.AmbiguousElementIds, tag.elementId, transform.gameObject);
                }
                for (var i = 0; i < transform.childCount; i++) stack.Push(transform.GetChild(i));
            }

            foreach (var id in index.AmbiguousStableIds)
                report.Error($"Duplicate prefab stableId '{id}'. Resolve the duplicate tags before saving.");
            foreach (var id in index.AmbiguousElementIds)
                report.Warn($"Duplicate prefab elementId tag '{id}'; fallback matching for that id is disabled.");
            foreach (var name in index.AmbiguousNames)
                report.Warn($"Duplicate GameObject name '{name}'; name fallback matching for that name is disabled.");
            return index;
        }

        private static void AddUnique(Dictionary<string, GameObject> values, HashSet<string> ambiguous,
            string key, GameObject value)
        {
            if (string.IsNullOrEmpty(key) || ambiguous.Contains(key)) return;
            if (values.ContainsKey(key))
            {
                values.Remove(key);
                ambiguous.Add(key);
                return;
            }
            values.Add(key, value);
        }

        private static GameObject ResolveExisting(PrefabIndex index, DesignerElementMetadata element)
        {
            if (index.StableIds.TryGetValue(element.stableId, out var stableMatch)) return stableMatch;
            if (!index.AmbiguousElementIds.Contains(element.elementId) &&
                index.ElementIds.TryGetValue(element.elementId, out var tagMatch)) return tagMatch;
            if (!index.AmbiguousNames.Contains(element.elementId) &&
                index.Names.TryGetValue(element.elementId, out var nameMatch)) return nameMatch;
            return null;
        }

        private static void ApplyRect(GameObject go, DesignerElementMetadata element, Vector2 localPosition)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            // Establish the designer-space placement first (top-left origin, y growing
            // downward), then re-anchor to the element's chosen preset. Reusing
            // UGUIAnchorUtility keeps the saved prefab identical to what the live preview
            // shows via UGUIDesignerBackend.SetAnchor. For the default TopLeft preset this
            // is a no-op re-application, so existing metadata saves exactly as before.
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(element.rect.width, element.rect.height);
            rt.anchoredPosition = new Vector2(localPosition.x, -localPosition.y);
            UGUIAnchorUtility.Apply(rt, element.anchorPreset);
            var layout = DesignerPropertyAdapter.Layout(element);
            if (layout.hasOverrides)
            {
                rt.pivot = layout.pivot;
                rt.localEulerAngles = new Vector3(0f, 0f, layout.rotation);
                rt.localScale = new Vector3(layout.scale.x, layout.scale.y, 1f);
            }
        }

        private static void ApplyTypedLayout(GameObject go, DesignerElementMetadata element, DesignerSaveReport report)
        {
            var layout = DesignerPropertyAdapter.Layout(element);
            if (!layout.hasOverrides) return;
            var layoutElement = go.GetComponent<LayoutElement>();
            if (layout.minSize != Vector2.zero || layout.maxSize != Vector2.zero)
                layoutElement = layoutElement ?? go.AddComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.minWidth = layout.minSize.x > 0f ? layout.minSize.x : -1f;
                layoutElement.minHeight = layout.minSize.y > 0f ? layout.minSize.y : -1f;
            }
            if (layout.maxSize != Vector2.zero)
                report.MarkUnsupported("Maximum size", $"'{element.elementId}' max size has no native uGUI LayoutElement equivalent; metadata was preserved.", element.elementId);

            var aspect = go.GetComponent<AspectRatioFitter>();
            if (layout.aspectRatio > 0f)
            {
                aspect = aspect ?? go.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                aspect.aspectRatio = layout.aspectRatio;
            }
            else if (aspect != null)
                Object.DestroyImmediate(aspect);

            if (DesignerPropertyAdapter.Clip(element))
            {
                if (go.GetComponent<RectMask2D>() == null) go.AddComponent<RectMask2D>();
            }
            else
            {
                var mask = go.GetComponent<RectMask2D>();
                if (mask != null) Object.DestroyImmediate(mask);
            }

            if (layout.marginLeft != 0f || layout.marginTop != 0f || layout.marginRight != 0f || layout.marginBottom != 0f)
                report.MarkSkipped($"'{element.elementId}' per-element margin is preserved but uGUI LayoutGroup has no native child margin.");
        }

        private static void ApplyVisualAndText(GameObject go, DesignerElementMetadata element, DesignerSaveReport report)
        {
            var type = element.elementType ?? "Panel";
            var control = DesignerComponentRegistry.Get(type).UGUIControl;
            bool isButton = Is(type, "Button") || Is(type, "IconButton") || control == "Button" || control == "ButtonTMP";
            bool isText = Is(type, "Label") || Is(type, "Toast") || Is(type, "Tooltip") || control == "Text" || control == "TextTMP";
            bool isImage = Is(type, "Image") || control == "Image";
            bool isValueFill = Is(type, "ProgressBar") || Is(type, "StatBar") || Is(type, "RadialFill");

            // Value components map to an Image with a fill method + fillAmount (uGUI: Partial - the
            // Track/Label virtual parts and animation are preview-only).
            if (isValueFill)
            {
                ApplyValueFill(go, element, report);
                report.MarkPreviewOnly("ProgressBar", $"'{element.elementId}' Track/Label/animation are preview-only (uGUI ProgressBar is Partial).", element.elementId);
            }

            // Honest per-element backend-support note: never claim to have written preview-only /
            // unsupported descriptor values.
            var support = DesignerComponentRegistry.Get(type).UGUISupport;
            if (support == DesignerBackendSupport.PreviewOnly)
                report.MarkPreviewOnly(type, $"'{element.elementId}' ({type}) is Preview-only on uGUI; only its rect/tint were written.", element.elementId);
            else if (support == DesignerBackendSupport.Unsupported)
                report.MarkUnsupported(type, $"'{element.elementId}' ({type}) is not supported on uGUI; only a placeholder GameObject was written.", element.elementId);

            // Tint on any Graphic; add an Image for Image/Button backgrounds when missing.
            var graphic = go.GetComponent<Graphic>();
            if (graphic == null && (isImage || isButton))
            {
                graphic = go.AddComponent<Image>();
                report.MarkChanged($"Added Image to '{element.elementId}'");
            }
            if (graphic != null)
                graphic.color = DesignerPropertyAdapter.BackgroundColor(element);

            ApplyVisualStyle(go, element, graphic, report);

            // The image selected in Designer is the real backend sprite, not merely a canvas
            // thumbnail. Preserve its aspect ratio by default so wide/tall source art does not
            // become stretched when the Designer rect has a different ratio.
            if (isImage && graphic is Image image)
            {
                image.sprite = element.previewImage;
                var visual = DesignerPropertyAdapter.Visual(element);
                image.preserveAspect = element.previewImage != null && visual.imageFit != DesignerImageFit.Stretch;
                image.type = visual.imageSlice ? Image.Type.Sliced : Image.Type.Simple;
                report.MarkChanged(element.previewImage != null
                    ? $"Applied sprite to '{element.elementId}'"
                    : $"Cleared sprite on '{element.elementId}'");
            }

            // Button component when the element is a button and lacks one.
            if (isButton && go.GetComponent<Button>() == null)
            {
                var button = go.AddComponent<Button>();
                if (graphic != null) button.targetGraphic = graphic;
                report.MarkChanged($"Added Button to '{element.elementId}'");
            }

            // Text: set on an existing text component, else create one for text-y elements.
            if (!string.IsNullOrEmpty(element.text) || isText || isButton)
                ApplyText(go, element, isButton, report);
        }

        private static void ApplyVisualStyle(GameObject go, DesignerElementMetadata element, Graphic graphic, DesignerSaveReport report)
        {
            var visual = DesignerPropertyAdapter.Visual(element);
            if (!visual.hasOverrides) return;

            var group = go.GetComponent<CanvasGroup>();
            if (visual.opacity < 0.999f || group != null)
            {
                group = group ?? go.AddComponent<CanvasGroup>();
                group.alpha = Mathf.Clamp01(visual.opacity);
            }
            if (graphic != null && visual.material != null) graphic.material = visual.material;

            var outline = go.GetComponent<UnityEngine.UI.Outline>();
            if (visual.borderWidth > 0f || visual.outlineWidth > 0f)
            {
                outline = outline ?? go.AddComponent<UnityEngine.UI.Outline>();
                var width = visual.outlineWidth > 0f ? visual.outlineWidth : visual.borderWidth;
                outline.effectDistance = new Vector2(width, -width);
                outline.effectColor = visual.outlineWidth > 0f ? visual.outlineColor : visual.borderColor;
                if (visual.borderWidth > 0f)
                    report.MarkSkipped($"'{element.elementId}' border uses uGUI Outline fallback (outside edge, not inset border).");
            }
            else if (outline != null) Object.DestroyImmediate(outline);

            UnityEngine.UI.Shadow shadow = null;
            foreach (var candidate in go.GetComponents<UnityEngine.UI.Shadow>())
                if (!(candidate is UnityEngine.UI.Outline)) { shadow = candidate; break; }
            if (visual.dropShadow)
            {
                shadow = shadow ?? go.AddComponent<UnityEngine.UI.Shadow>();
                shadow.effectColor = visual.shadowColor;
                shadow.effectDistance = visual.shadowOffset;
            }
            else if (shadow != null) Object.DestroyImmediate(shadow);

            if (visual.cornerRadius > 0f && (!(graphic is Image image) || image.sprite == null || !visual.imageSlice))
                report.MarkSkipped($"'{element.elementId}' numeric corner radius requires a rounded sliced Sprite on uGUI.");
            if (visual.innerShadow) report.MarkUnsupported("Inner shadow", $"'{element.elementId}' inner shadow is unsupported on stock uGUI.", element.elementId);
            if (visual.blur > 0f) report.MarkUnsupported("Blur", $"'{element.elementId}' blur is unsupported on stock uGUI.", element.elementId);
            if (visual.gradient != null) report.MarkUnsupported("Gradient", $"'{element.elementId}' gradient requires a custom uGUI material.", element.elementId);
        }

        private static void ApplyAutoLayout(GameObject go, DesignerElementMetadata element, DesignerSaveReport report)
        {
            var layout = element.autoLayout;
            if (layout == null) return;
            var layoutElement = go.GetComponent<LayoutElement>();
            if (layout.widthSizing == DesignerAutoLayoutSizing.Fill || layout.heightSizing == DesignerAutoLayoutSizing.Fill)
            {
                if (layoutElement == null) layoutElement = go.AddComponent<LayoutElement>();
                layoutElement.flexibleWidth = layout.widthSizing == DesignerAutoLayoutSizing.Fill ? 1f : 0f;
                layoutElement.flexibleHeight = layout.heightSizing == DesignerAutoLayoutSizing.Fill ? 1f : 0f;
            }

            if (!layout.enabled) return;
            var horizontal = go.GetComponent<HorizontalLayoutGroup>();
            var vertical = go.GetComponent<VerticalLayoutGroup>();
            var grid = go.GetComponent<GridLayoutGroup>();
            if (layout.direction != DesignerAutoLayoutDirection.Row && horizontal != null) Object.DestroyImmediate(horizontal);
            if (layout.direction != DesignerAutoLayoutDirection.Column && vertical != null) Object.DestroyImmediate(vertical);
            if (layout.direction != DesignerAutoLayoutDirection.Grid && grid != null) Object.DestroyImmediate(grid);

            if (layout.direction == DesignerAutoLayoutDirection.Grid)
            {
                grid = go.GetComponent<GridLayoutGroup>() ?? go.AddComponent<GridLayoutGroup>();
                grid.padding = Padding(layout);
                grid.spacing = new Vector2(layout.spacing, layout.spacing);
                grid.cellSize = new Vector2(Mathf.Max(1f, layout.gridCellWidth), Mathf.Max(1f, layout.gridCellHeight));
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = Mathf.Max(1, layout.gridColumns);
            }
            else
            {
                HorizontalOrVerticalLayoutGroup group = layout.direction == DesignerAutoLayoutDirection.Row
                    ? (HorizontalOrVerticalLayoutGroup)(go.GetComponent<HorizontalLayoutGroup>() ?? go.AddComponent<HorizontalLayoutGroup>())
                    : go.GetComponent<VerticalLayoutGroup>() ?? go.AddComponent<VerticalLayoutGroup>();
                group.padding = Padding(layout);
                group.spacing = layout.spacing;
                group.childControlWidth = false;
                group.childControlHeight = false;
                group.childForceExpandWidth = false;
                group.childForceExpandHeight = false;
                group.childAlignment = TextAnchorFor(DesignerPropertyAdapter.Layout(element).align,
                    DesignerPropertyAdapter.Layout(element).justify);
            }
            report.MarkChanged($"Applied {layout.direction} layout to '{element.elementId}'");
        }

        private static RectOffset Padding(DesignerAutoLayoutMetadata layout)
            => new RectOffset(Mathf.RoundToInt(layout.paddingLeft), Mathf.RoundToInt(layout.paddingRight),
                Mathf.RoundToInt(layout.paddingTop), Mathf.RoundToInt(layout.paddingBottom));

        /// <summary>
        /// Maps a value component to a filled <see cref="Image"/>: fill method from the fill
        /// direction (Radial for RadialFill), fillAmount from the normalized preview value, and
        /// fill origin/clockwise to match the Designer preview.
        /// </summary>
        private static void ApplyValueFill(GameObject go, DesignerElementMetadata element, DesignerSaveReport report)
        {
            var img = go.GetComponent<Image>();
            if (img == null)
            {
                img = go.AddComponent<Image>();
                report.MarkChanged($"Added filled Image to '{element.elementId}'");
            }
            img.color = element.tint;
            img.type = Image.Type.Filled;

            var normalized = Mathf.Clamp01(Mathf.InverseLerp(element.fill.minValue, element.fill.maxValue, element.previewValue));
            img.fillAmount = normalized;

            if (Is(element.elementType ?? "", "RadialFill"))
            {
                img.fillMethod = Image.FillMethod.Radial360;
                img.fillOrigin = (int)Image.Origin360.Bottom;
                img.fillClockwise = element.fill.clockwise;
            }
            else
            {
                switch (element.fill.direction)
                {
                    case DesignerFillDirection.LeftToRight:
                        img.fillMethod = Image.FillMethod.Horizontal; img.fillOrigin = (int)Image.OriginHorizontal.Left; break;
                    case DesignerFillDirection.RightToLeft:
                        img.fillMethod = Image.FillMethod.Horizontal; img.fillOrigin = (int)Image.OriginHorizontal.Right; break;
                    case DesignerFillDirection.BottomToTop:
                        img.fillMethod = Image.FillMethod.Vertical; img.fillOrigin = (int)Image.OriginVertical.Bottom; break;
                    case DesignerFillDirection.TopToBottom:
                        img.fillMethod = Image.FillMethod.Vertical; img.fillOrigin = (int)Image.OriginVertical.Top; break;
                }
            }
            report.MarkChanged($"Set fillAmount {normalized:0.##} on '{element.elementId}'");
        }

        private static void ApplyText(GameObject go, DesignerElementMetadata element, bool isButton, DesignerSaveReport report)
        {
            var tmp = go.GetComponentInChildren<TMP_Text>(true);
            var uiText = tmp == null ? go.GetComponentInChildren<Text>(true) : null;

            if (tmp != null) { if (element.text != null) tmp.text = element.text; ApplyTextStyle(tmp, element); return; }
            if (uiText != null) { if (element.text != null) uiText.text = element.text; ApplyTextStyle(uiText, element); return; }

            if (string.IsNullOrEmpty(element.text)) return;

            // No text component: create a TMP text. For buttons, place it on a child so the
            // button's own graphic (background) is preserved.
            var host = go;
            if (isButton)
            {
                var child = new GameObject(element.elementId + "_Text", typeof(RectTransform));
                child.transform.SetParent(go.transform, false);
                var crt = child.GetComponent<RectTransform>();
                crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
                crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
                host = child;
            }
            var newText = host.AddComponent<TextMeshProUGUI>();
            newText.text = element.text;
            newText.alignment = TextAlignmentOptions.Center;
            ApplyTextStyle(newText, element);
            report.MarkChanged($"Added text to '{element.elementId}'");
        }

        private static void ApplyTextStyle(TMP_Text tmp, DesignerElementMetadata element)
        {
            var typography = DesignerPropertyAdapter.Typography(element);
            tmp.color = DesignerPropertyAdapter.TextColor(element);
            tmp.fontSize = DesignerPropertyAdapter.FontSize(element);
            if (!typography.hasOverrides) return;
            if (typography.fontAsset is TMP_FontAsset font) tmp.font = font;
            tmp.enableAutoSizing = typography.autoSize;
            tmp.fontSizeMin = typography.minFontSize;
            tmp.fontSizeMax = typography.maxFontSize;
            tmp.alignment = TmpAlignment(typography.alignment);
            tmp.textWrappingMode = typography.wrapping ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            tmp.overflowMode = TmpOverflow(typography);
            tmp.richText = typography.richText;
            tmp.isRightToLeftText = typography.rightToLeft;
            tmp.lineSpacing = (typography.lineHeight - 1f) * 100f;
            tmp.characterSpacing = typography.letterSpacing;
            tmp.paragraphSpacing = typography.paragraphSpacing;
            tmp.fontStyle = TmpFontStyle(typography);
            tmp.outlineWidth = typography.outlineWidth;
            tmp.outlineColor = typography.outlineColor;
            ApplyTextShadow(tmp, typography);
        }

        private static void ApplyTextStyle(Text text, DesignerElementMetadata element)
        {
            var typography = DesignerPropertyAdapter.Typography(element);
            text.color = DesignerPropertyAdapter.TextColor(element);
            text.fontSize = Mathf.RoundToInt(DesignerPropertyAdapter.FontSize(element));
            if (!typography.hasOverrides) return;
            if (typography.fontAsset is Font font) text.font = font;
            text.resizeTextForBestFit = typography.autoSize;
            text.resizeTextMinSize = Mathf.RoundToInt(typography.minFontSize);
            text.resizeTextMaxSize = Mathf.RoundToInt(typography.maxFontSize);
            text.alignment = (TextAnchor)typography.alignment;
            text.horizontalOverflow = typography.wrapping ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = typography.overflow == DesignerTextOverflow.Overflow ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;
            text.supportRichText = typography.richText;
            var bold = typography.fontWeight >= DesignerFontWeight.SemiBold || (typography.fontStyle & DesignerFontStyle.Bold) != 0;
            var italic = (typography.fontStyle & DesignerFontStyle.Italic) != 0;
            text.fontStyle = bold && italic ? FontStyle.BoldAndItalic : bold ? FontStyle.Bold : italic ? FontStyle.Italic : FontStyle.Normal;
            ApplyTextShadow(text, typography);
        }

        private static void ApplyTextShadow(Graphic graphic, DesignerTypographyMetadata typography)
        {
            UnityEngine.UI.Shadow shadow = null;
            foreach (var candidate in graphic.GetComponents<UnityEngine.UI.Shadow>())
                if (!(candidate is UnityEngine.UI.Outline)) { shadow = candidate; break; }
            if (typography.textShadow)
            {
                shadow = shadow ?? graphic.gameObject.AddComponent<UnityEngine.UI.Shadow>();
                shadow.effectColor = typography.shadowColor;
                shadow.effectDistance = typography.shadowOffset;
            }
            else if (shadow != null) Object.DestroyImmediate(shadow);
        }

        private static TextAnchor TextAnchorFor(DesignerLayoutAlignment align, DesignerJustifyContent justify)
        {
            var vertical = align == DesignerLayoutAlignment.Center ? 1 : align == DesignerLayoutAlignment.End ? 2 : 0;
            var horizontal = justify == DesignerJustifyContent.Center ? 1 : justify == DesignerJustifyContent.End ? 2 : 0;
            return (TextAnchor)(vertical * 3 + horizontal);
        }

        private static TextAlignmentOptions TmpAlignment(DesignerTextAlignment value)
        {
            switch (value)
            {
                case DesignerTextAlignment.UpperLeft: return TextAlignmentOptions.TopLeft;
                case DesignerTextAlignment.UpperCenter: return TextAlignmentOptions.Top;
                case DesignerTextAlignment.UpperRight: return TextAlignmentOptions.TopRight;
                case DesignerTextAlignment.MiddleLeft: return TextAlignmentOptions.Left;
                case DesignerTextAlignment.MiddleRight: return TextAlignmentOptions.Right;
                case DesignerTextAlignment.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case DesignerTextAlignment.LowerCenter: return TextAlignmentOptions.Bottom;
                case DesignerTextAlignment.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        private static TextOverflowModes TmpOverflow(DesignerTypographyMetadata typography)
        {
            if (typography.ellipsis || typography.overflow == DesignerTextOverflow.Ellipsis) return TextOverflowModes.Ellipsis;
            if (typography.overflow == DesignerTextOverflow.Clip) return TextOverflowModes.Masking;
            if (typography.overflow == DesignerTextOverflow.Truncate) return TextOverflowModes.Truncate;
            return TextOverflowModes.Overflow;
        }

        private static FontStyles TmpFontStyle(DesignerTypographyMetadata typography)
        {
            var style = FontStyles.Normal;
            if (typography.fontWeight >= DesignerFontWeight.SemiBold || (typography.fontStyle & DesignerFontStyle.Bold) != 0) style |= FontStyles.Bold;
            if ((typography.fontStyle & DesignerFontStyle.Italic) != 0) style |= FontStyles.Italic;
            if ((typography.fontStyle & DesignerFontStyle.Underline) != 0) style |= FontStyles.Underline;
            if ((typography.fontStyle & DesignerFontStyle.Strikethrough) != 0) style |= FontStyles.Strikethrough;
            return style;
        }

        private static bool Is(string type, string other)
            => string.Equals(type, other, System.StringComparison.OrdinalIgnoreCase);

        private static void ApplyAttachedComponents(GameObject go, DesignerElementMetadata element, DesignerSaveReport report)
        {
            var requested = element.attachedComponents ?? new List<DesignerAttachedComponentMetadata>();
            var desired = new Dictionary<System.Type, int>();
            foreach (var item in requested)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.typeName)) continue;
                var type = ResolveComponentType(item.typeName);
                if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters)
                {
                    report.MarkUnsupported("Attached component",
                        $"'{element.elementId}' references unavailable MonoBehaviour '{item.typeName}'. Metadata was preserved.", element.elementId);
                    continue;
                }
                desired.TryGetValue(type, out var count);
                desired[type] = count + 1;
            }

            var tracker = go.GetComponent<DesignerAttachedComponentTracker>();
            if (tracker != null)
            {
                tracker.managedComponents ??= new List<Component>();
                var trackedCounts = new Dictionary<System.Type, int>();
                foreach (var tracked in tracker.managedComponents)
                {
                    if (tracked == null || tracked.gameObject != go) continue;
                    trackedCounts.TryGetValue(tracked.GetType(), out var trackedCount);
                    trackedCounts[tracked.GetType()] = trackedCount + 1;
                }
                var allowedTrackedCounts = new Dictionary<System.Type, int>();
                foreach (var pair in trackedCounts)
                {
                    desired.TryGetValue(pair.Key, out var wanted);
                    var userOwnedCount = Mathf.Max(0, go.GetComponents(pair.Key).Length - pair.Value);
                    allowedTrackedCounts[pair.Key] = Mathf.Max(0, wanted - userOwnedCount);
                }
                for (var i = tracker.managedComponents.Count - 1; i >= 0; i--)
                {
                    var component = tracker.managedComponents[i];
                    if (component == null || component.gameObject != go)
                    {
                        tracker.managedComponents.RemoveAt(i);
                        continue;
                    }
                    allowedTrackedCounts.TryGetValue(component.GetType(), out var wanted);
                    var sameTypeBefore = 0;
                    for (var j = 0; j < i; j++)
                        if (tracker.managedComponents[j] != null && tracker.managedComponents[j].GetType() == component.GetType())
                            sameTypeBefore++;
                    if (sameTypeBefore >= wanted)
                    {
                        var componentName = component.GetType().Name;
                        tracker.managedComponents.RemoveAt(i);
                        Object.DestroyImmediate(component);
                        report.MarkChanged($"Removed Designer-managed {componentName} from '{element.elementId}'");
                    }
                }
            }

            foreach (var pair in desired)
            {
                var existing = go.GetComponents(pair.Key).Length;
                for (var i = existing; i < pair.Value; i++)
                {
                    try
                    {
                        var added = go.AddComponent(pair.Key);
                        tracker ??= go.AddComponent<DesignerAttachedComponentTracker>();
                        tracker.managedComponents ??= new List<Component>();
                        tracker.managedComponents.Add(added);
                        report.MarkChanged($"Added {pair.Key.Name} to '{element.elementId}'");
                    }
                    catch (System.Exception ex)
                    {
                        report.Warn($"Could not attach {pair.Key.FullName} to '{element.elementId}': {ex.Message}");
                        break;
                    }
                }
            }

            if (tracker != null && (tracker.managedComponents == null || tracker.managedComponents.Count == 0))
                Object.DestroyImmediate(tracker);
        }

        private static System.Type ResolveComponentType(string typeName)
        {
            var type = System.Type.GetType(typeName, false);
            if (type != null) return type;
            var comma = typeName.IndexOf(',');
            var fullName = comma >= 0 ? typeName.Substring(0, comma).Trim() : typeName.Trim();
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }
    }
}

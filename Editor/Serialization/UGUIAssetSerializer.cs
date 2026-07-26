using System.Collections.Generic;
using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Components;
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
                WarnOnDuplicateNames(root, report);
                ApplyMetadata(root, metadata, report);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                report.MarkChanged($"Prefab '{System.IO.Path.GetFileName(path)}'");
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
            // Pass 1: ensure every element exists so parents resolve regardless of order.
            var objects = new Dictionary<string, GameObject>();
            foreach (var element in metadata.elements)
            {
                if (element == null || string.IsNullOrEmpty(element.elementId)) continue;
                var go = FindDescendant(root.transform, element.elementId);
                if (go == null)
                {
                    go = new GameObject(element.elementId, typeof(RectTransform));
                    go.transform.SetParent(root.transform, false);
                    report.MarkChanged($"Created element '{element.elementId}'");
                }
                objects[element.elementId] = go;
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
                    go.transform.SetParent(parent.transform, false);
                }

                // Element rects are stored in absolute canvas space; convert to the parent-relative
                // local position so a child's anchoredPosition is correct once re-parented (and so
                // moving a parent carries its children, matching the Designer canvas).
                var local = DesignerCoordinateUtility.GetLocalPosition(metadata, element);
                ApplyRect(go, element, local);
                ApplyAutoLayout(go, element, report);
                go.SetActive(!element.hiddenInDesigner);
                ApplyVisualAndText(go, element, report);
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
        }

        private static void ApplyVisualAndText(GameObject go, DesignerElementMetadata element, DesignerSaveReport report)
        {
            var type = element.elementType ?? "Panel";
            bool isButton = Is(type, "Button") || Is(type, "IconButton");
            bool isText = Is(type, "Label") || Is(type, "Toast") || Is(type, "Tooltip");
            bool isImage = Is(type, "Image");
            bool isValueFill = Is(type, "ProgressBar") || Is(type, "StatBar") || Is(type, "RadialFill");

            // Value components map to an Image with a fill method + fillAmount (uGUI: Partial - the
            // Track/Label virtual parts and animation are preview-only).
            if (isValueFill)
            {
                ApplyValueFill(go, element, report);
                report.MarkSkipped($"'{element.elementId}' Track/Label/animation are preview-only (uGUI ProgressBar is Partial).");
            }

            // Honest per-element backend-support note: never claim to have written preview-only /
            // unsupported descriptor values.
            var support = DesignerComponentRegistry.Get(type).UGUISupport;
            if (support == DesignerBackendSupport.PreviewOnly)
                report.MarkSkipped($"'{element.elementId}' ({type}) is Preview-only on uGUI; only its rect/tint were written.");
            else if (support == DesignerBackendSupport.Unsupported)
                report.MarkSkipped($"'{element.elementId}' ({type}) is not supported on uGUI; only a placeholder GameObject was written.");

            // Tint on any Graphic; add an Image for Image/Button backgrounds when missing.
            var graphic = go.GetComponent<Graphic>();
            if (graphic == null && (isImage || isButton))
            {
                graphic = go.AddComponent<Image>();
                report.MarkChanged($"Added Image to '{element.elementId}'");
            }
            if (graphic != null)
                graphic.color = element.tint;

            // The image selected in Designer is the real backend sprite, not merely a canvas
            // thumbnail. Preserve its aspect ratio by default so wide/tall source art does not
            // become stretched when the Designer rect has a different ratio.
            if (isImage && graphic is Image image)
            {
                image.sprite = element.previewImage;
                image.preserveAspect = element.previewImage != null;
                image.type = Image.Type.Simple;
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
            if (uiText != null) { if (element.text != null) uiText.text = element.text; uiText.color = element.textColor; uiText.fontSize = element.fontSize; return; }

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
            tmp.color = element.textColor;
            tmp.fontSize = element.fontSize;
        }

        private static void WarnOnDuplicateNames(GameObject root, DesignerSaveReport report)
        {
            var seen = new HashSet<string>();
            var reported = new HashSet<string>();
            var stack = new Stack<Transform>();
            stack.Push(root.transform);
            while (stack.Count > 0)
            {
                var t = stack.Pop();
                if (!seen.Add(t.name) && reported.Add(t.name))
                    report.Warn($"Duplicate GameObject name '{t.name}' in prefab; name-based element matching may be unpredictable.");
                for (int i = 0; i < t.childCount; i++)
                    stack.Push(t.GetChild(i));
            }
        }

        private static GameObject FindDescendant(Transform root, string name)
        {
            if (root.name == name) return root.gameObject;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDescendant(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static bool Is(string type, string other)
            => string.Equals(type, other, System.StringComparison.OrdinalIgnoreCase);
    }
}

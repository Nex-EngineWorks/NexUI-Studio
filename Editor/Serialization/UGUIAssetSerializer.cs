using System.Collections.Generic;
using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Components.Serialization;
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
                DesignerMetadataUtility.MarkDirty(metadata);
            if (definition != null)
                DesignerMetadataUtility.MarkDirty(definition);

            var prefab = definition != null ? definition.backendAsset.asset as GameObject : null;
            if (prefab == null)
            {
                report.MarkSkipped("No uGUI prefab assigned to the screen backend asset (metadata saved only).");
                SaveDirtyAssets(metadata, definition);
                MarkMetadataWritten(metadata, report);
                return report;
            }

            var path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
            {
                report.Warn($"Backend asset '{prefab.name}' is not a prefab asset; prefab changes were skipped (metadata saved only).");
                SaveDirtyAssets(metadata, definition);
                MarkMetadataWritten(metadata, report);
                return report;
            }

            if (metadata == null || metadata.elements.Count == 0)
            {
                report.MarkSkipped("No metadata elements to apply to prefab.");
                SaveDirtyAssets(metadata, definition);
                MarkMetadataWritten(metadata, report);
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
                report.Error($"Failed to write prefab: {e}");
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }

            if (!report.HasErrors)
            {
                SaveDirtyAssets(metadata, definition);
                MarkMetadataWritten(metadata, report);
            }
            return report;
        }

        private static void MarkMetadataWritten(DesignerMetadataAsset metadata, DesignerSaveReport report)
        {
            if (metadata != null) report.MarkChanged("Designer metadata asset");
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
                    TrackFactoryComponents(go);
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
                // Authored components last: they are the element's real identity, so their values win
                // over anything the preset-derived write above assumed.
                UGUIComponentWriter.Apply(go, element, report);
                UGUIControlFactory.EnsureAndApply(go, element, report);
                StudioComponentWriter.EnsureComponents(go, element, report);
            }

            // Pass 2b: values and references, once every component on every element exists. A field
            // may point at any element on the screen, including one written later in pass 2.
            var byStableId = new Dictionary<string, GameObject>(System.StringComparer.Ordinal);
            foreach (var element in metadata.elements)
            {
                if (element == null || string.IsNullOrEmpty(element.stableId)) continue;
                if (objects.TryGetValue(element.elementId ?? string.Empty, out var go) && go != null)
                    byStableId[element.stableId] = go;
            }
            foreach (var element in metadata.elements)
            {
                if (element == null || string.IsNullOrEmpty(element.elementId)) continue;
                if (!objects.TryGetValue(element.elementId, out var go) || go == null) continue;
                StudioComponentWriter.ApplyValues(go, element, report,
                    stableId => byStableId.TryGetValue(stableId ?? string.Empty, out var target) ? target : null);
            }

            // Pass 3: reflect Designer sibling order onto the transform (SetSiblingIndex), so the
            // saved prefab's child order matches the hierarchy panel / draw order.
            foreach (var element in metadata.elements)
            {
                if (element == null || string.IsNullOrEmpty(element.elementId)) continue;
                if (!objects.TryGetValue(element.elementId, out var go) || go == null) continue;

                // A screen root has no parent, so there is no sibling list to order it within.
                // Reading parent.childCount on it threw, which failed the whole save - and because
                // the throw happened in pass 3, every change passes 1 and 2 had already made was
                // reported as written while the prefab was left unsaved.
                var parent = go.transform.parent;
                if (parent == null) continue;

                var ordered = DesignerHierarchyUtility.GetOrderedChildren(metadata, element.parentId);
                var index = ordered.IndexOf(element);
                if (index >= 0 && index < parent.childCount)
                    go.transform.SetSiblingIndex(index);
            }

            ReportOwnership(root, metadata, objects, report);
            ReportOrphans(root, usedObjects, report);
        }

        /// <summary>
        /// States, in the Validation / Save report, exactly what this save was allowed to overwrite.
        /// </summary>
        /// <remarks>
        /// Every other line in the report is a consequence of this boundary, but the boundary itself was
        /// never written down - so "Preserved user-authored HorizontalLayoutGroup" read as a failure
        /// rather than as the rule working. Naming the two halves turns the individual notes into
        /// something a user can act on: what the Studio rewrites on every save, and what only they can
        /// change.
        ///
        /// The counts come from the prefab that is about to be written, not from the metadata, so they
        /// describe the real object rather than the intent.
        /// </remarks>
        internal static void ReportOwnership(GameObject root, DesignerMetadataAsset metadata,
            Dictionary<string, GameObject> objects, DesignerSaveReport report)
        {
            var designerOwned = 0;
            var adopted = 0;
            var manualComponents = 0;
            var manualElements = new List<string>();

            foreach (var pair in objects)
            {
                var go = pair.Value;
                if (go == null) continue;

                var tag = go.GetComponent<NxUGuiBindingTag>();
                if (tag != null && tag.ownership == NexUIElementOwnership.DesignerOwned) designerOwned++;
                else adopted++;

                var manual = ManualComponentNames(go, metadata?.Find(pair.Key));
                if (manual.Count == 0) continue;
                manualComponents += manual.Count;
                if (manualElements.Count < 8)
                    manualElements.Add($"{pair.Key} ({string.Join(", ", manual)})");
            }

            // Only objects that sit outside every Studio element are counted. An object *inside* one -
            // a Button's caption child, say - may well have been created by this serializer, and
            // claiming it as the user's would make the boundary statement wrong in the common case.
            var outside = 0;
            foreach (var transform in root.GetComponentsInChildren<Transform>(includeInactive: true))
                if (transform != root.transform && !HasTaggedAncestor(transform, root.transform)) outside++;

            report.MarkOwnership("Studio-owned",
                (report.IsPreview
                    ? "Will be rewritten: rect, anchors, active state and the authored component stack of "
                    : "Rewritten on every save: rect, anchors, active state and the authored component stack of ") +
                $"{objects.Count} element(s) - {designerOwned} created by the Studio, {adopted} adopted from the prefab.");

            if (manualComponents > 0)
                report.MarkOwnership("Manual",
                    $"Left untouched: {manualComponents} component(s) you added in the Prefab. " +
                    $"Add them to the element's stack in the Studio if you want their values saved. " +
                    (manualElements.Count > 0 ? string.Join("; ", manualElements) : string.Empty));

            if (outside > 0)
                report.MarkOwnership("Manual",
                    $"Left untouched: {outside} object(s) outside every Studio element. " +
                    "Import the prefab to bring them into the screen.");

            // Only meaningful after a real write: in a dry run the missing entries are the elements the
            // save is about to create, not elements it failed to apply.
            if (!report.IsPreview && metadata != null && objects.Count < CountedElements(metadata))
                report.MarkOwnership("Studio-owned",
                    $"{CountedElements(metadata) - objects.Count} metadata element(s) were not applied; see the errors above.");
        }

        /// <summary>
        /// Records the components <see cref="UGUIControlFactory"/> put on a freshly created object.
        /// </summary>
        /// <remarks>
        /// A stock control arrives with its parts already attached - <c>CreatePanel</c> brings an
        /// <c>Image</c>, <c>CreateSlider</c> a <c>Slider</c> - and none of them went through
        /// <see cref="AddManagedComponent{T}"/>, so nothing recorded that the Studio had made them.
        /// The overwrite-scope report would then have called the Studio's own Image a component the
        /// user added by hand, which is the one thing that statement must never get wrong.
        ///
        /// They go in the legacy <c>managedComponents</c> list rather than the generated one: that list
        /// is read for adoption but never for removal, so recording a part here can pair it with a
        /// stack entry and can never delete it behind the user's back.
        /// </remarks>
        private static void TrackFactoryComponents(GameObject go)
        {
            var components = go.GetComponents<Component>();
            if (components.Length <= 1) return;

            var tracker = go.GetComponent<DesignerAttachedComponentTracker>() ??
                          go.AddComponent<DesignerAttachedComponentTracker>();
            tracker.managedComponents ??= new List<Component>();
            foreach (var component in components)
            {
                if (component == null || component is Transform ||
                    component is CanvasRenderer || component is DesignerAttachedComponentTracker) continue;
                if (!tracker.managedComponents.Contains(component)) tracker.managedComponents.Add(component);
            }
        }

        /// <summary>Whether <paramref name="transform"/> or any ancestor up to <paramref name="root"/> is a Studio element.</summary>
        private static bool HasTaggedAncestor(Transform transform, Transform root)
        {
            for (var current = transform; current != null && current != root.parent; current = current.parent)
                if (current.GetComponent<NxUGuiBindingTag>() != null) return true;
            return false;
        }

        private static int CountedElements(DesignerMetadataAsset metadata)
        {
            var count = 0;
            foreach (var element in metadata.elements)
                if (element != null && !string.IsNullOrEmpty(element.elementId)) count++;
            return count;
        }

        /// <summary>
        /// Components on <paramref name="go"/> that neither writer owns - the ones a user added in the
        /// Prefab and that a save must therefore neither rewrite nor remove.
        /// </summary>
        /// <remarks>
        /// Two signals, because neither alone is complete. The tracker knows what the Studio created,
        /// but the registry writer adopts an existing component without recording anything, so the
        /// element's own stack is consulted too: if the element declares an <c>Image</c>, whichever
        /// Image is on the object is written by this save and is not the user's to keep.
        /// </remarks>
        private static List<string> ManualComponentNames(GameObject go, DesignerElementMetadata element)
        {
            var declared = new HashSet<System.Type>();
            foreach (var entry in element?.components ?? new List<DesignerElementComponent>())
            {
                var type = StudioReferenceUtility.ResolveComponentType(entry);
                if (type != null) declared.Add(type);
            }

            var names = new List<string>();
            var tracker = go.GetComponent<DesignerAttachedComponentTracker>();
            foreach (var component in go.GetComponents<Component>())
            {
                if (component == null) continue;
                if (component is Transform || component is NxUGuiBindingTag ||
                    component is DesignerAttachedComponentTracker || component is CanvasRenderer) continue;
                if (tracker != null && tracker.Owns(component)) continue;
                if (declared.Contains(component.GetType())) continue;
                names.Add(component.GetType().Name);
            }
            return names;
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
                layoutElement = layoutElement ?? AddManagedComponent<LayoutElement>(go);
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
                aspect = aspect ?? AddManagedComponent<AspectRatioFitter>(go);
                aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                aspect.aspectRatio = layout.aspectRatio;
            }
            else if (aspect != null)
                RemoveManagedComponents<AspectRatioFitter>(go, report, element.elementId);

            if (DesignerPropertyAdapter.Clip(element))
            {
                if (go.GetComponent<RectMask2D>() == null) AddManagedComponent<RectMask2D>(go);
            }
            else
            {
                if (go.GetComponent<RectMask2D>() != null)
                    RemoveManagedComponents<RectMask2D>(go, report, element.elementId);
            }

            if (layout.marginLeft != 0f || layout.marginTop != 0f || layout.marginRight != 0f || layout.marginBottom != 0f)
                report.MarkSkipped($"'{element.elementId}' per-element margin is preserved but uGUI LayoutGroup has no native child margin.");
        }

        /// <summary>
        /// Writes the element's drawn path to the prefab, or clears one it no longer has.
        /// </summary>
        /// <remarks>
        /// The path is stored fitted to the element's rect, and
        /// <see cref="Integrations.UGUI.NXVectorGraphic"/> re-fits it to whatever rect the layout
        /// ends up giving the object - so the shape is handed over as authored, with no conversion
        /// here to drift out of step with the renderer.
        /// </remarks>
        /// <returns>Whether the element draws a path, and so has no rect fill of its own.</returns>
        private static bool ApplyVectorShape(GameObject go, DesignerElementMetadata element, DesignerSaveReport report)
        {
            var draws = element.hasShape && element.vectorShape != null && !element.vectorShape.IsEmpty;

            if (!draws)
            {
                if (Integrations.UGUI.NexUGuiShapeApplier.Remove(go))
                    report.MarkChanged($"Removed vector shape from '{element.elementId}'");
                return false;
            }

            // Cloned: the prefab keeps its own copy, so a later pen edit does not reach into an
            // already-saved asset and change it without a save.
            Integrations.UGUI.NexUGuiShapeApplier.Apply(go, element.vectorShape.Clone());
            report.MarkChanged($"Applied vector shape to '{element.elementId}'");
            return true;
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

            // A drawn path replaces the element's rect fill, so it is settled before any background
            // Graphic is created below - otherwise this would add an Image and then immediately
            // destroy it, reporting both. Applied through the same applier the compiled builder
            // uses so a saved prefab and a compiled screen draw the same thing.
            //
            // Only the *background* is replaced: a button with a custom silhouette still needs its
            // label, so everything below that is not the rect fill still runs.
            var hasVector = ApplyVectorShape(go, element, report);

            var visualStyle = DesignerPropertyAdapter.Visual(element);

            // Tint on the element's background Graphic. Sprite-less rounded surfaces use NexUI's
            // mesh graphic instead of requiring a sliced texture; real images keep the stock Image.
            //
            // A text component is a Graphic too, and it is the element's *text*, not its
            // background. Writing the tint into it overwrote the font colour - and only on the
            // second save, because the first one had not created the text component yet when this
            // ran. That is what made saving twice produce a different prefab.
            var graphic = BackgroundGraphicOf(go);
            if (graphic == null && !hasVector && (isImage || isButton || visualStyle.hasOverrides))
            {
                if (element.previewImage == null && visualStyle.cornerRadius > 0f)
                {
                    graphic = AddManagedComponent<NXRoundedRect>(go);
                    report.MarkChanged($"Added rounded surface to '{element.elementId}'");
                }
                else
                {
                    graphic = AddManagedComponent<Image>(go);
                    report.MarkChanged($"Added Image to '{element.elementId}'");
                }
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
                // Use Unity's overloaded null check. A destroyed native component is non-null to
                // C#'s ?? operator and throws MissingComponentException on the next property access.
                if (group == null) group = go.AddComponent<CanvasGroup>();
                if (group != null) group.alpha = Mathf.Clamp01(visual.opacity);
            }
            if (graphic != null && visual.material != null) graphic.material = visual.material;

            if (graphic is NXRoundedRect rounded)
            {
                rounded.Radius = Mathf.Max(0f, visual.cornerRadius);
                rounded.BorderWidth = Mathf.Max(0f, visual.borderWidth);
                rounded.BorderColor = visual.borderColor;
            }

            var outline = go.GetComponent<UnityEngine.UI.Outline>();
            if (visual.borderWidth > 0f || visual.outlineWidth > 0f)
            {
                outline = outline ?? AddManagedComponent<UnityEngine.UI.Outline>(go);
                var width = visual.outlineWidth > 0f ? visual.outlineWidth : visual.borderWidth;
                outline.effectDistance = new Vector2(width, -width);
                outline.effectColor = visual.outlineWidth > 0f ? visual.outlineColor : visual.borderColor;
                if (visual.borderWidth > 0f)
                    report.MarkSkipped($"'{element.elementId}' border uses uGUI Outline fallback (outside edge, not inset border).");
            }
            else if (outline != null)
                RemoveManagedComponents<UnityEngine.UI.Outline>(go, report, element.elementId);

            UnityEngine.UI.Shadow shadow = null;
            foreach (var candidate in go.GetComponents<UnityEngine.UI.Shadow>())
                if (!(candidate is UnityEngine.UI.Outline)) { shadow = candidate; break; }
            var softShadow = go.GetComponent<NXSoftShadow>();
            if (visual.dropShadow && visual.shadowBlur > 0f && graphic != null)
            {
                if (softShadow == null) softShadow = AddManagedComponent<NXSoftShadow>(go);
                softShadow.ShadowColor = visual.shadowColor;
                softShadow.Offset = visual.shadowOffset;
                softShadow.Spread = visual.shadowBlur;
                if (shadow != null)
                    RemoveManagedComponents<UnityEngine.UI.Shadow>(go, report, element.elementId,
                        candidate => !(candidate is UnityEngine.UI.Outline));
            }
            else if (visual.dropShadow)
            {
                shadow = shadow ?? AddManagedComponent<UnityEngine.UI.Shadow>(go);
                shadow.effectColor = visual.shadowColor;
                shadow.effectDistance = visual.shadowOffset;
            }
            else
            {
                if (softShadow != null) RemoveManagedComponents<NXSoftShadow>(go, report, element.elementId);
                if (shadow != null)
                RemoveManagedComponents<UnityEngine.UI.Shadow>(go, report, element.elementId,
                    candidate => !(candidate is UnityEngine.UI.Outline));
            }

            if (visual.cornerRadius > 0f && graphic is Image image && (image.sprite == null || !visual.imageSlice))
                report.MarkSkipped($"'{element.elementId}' uses a stock Image; assign a sliced Sprite or use NX Rounded Rect for numeric radius.");
            if (visual.innerShadow) report.MarkUnsupported("Inner shadow", $"'{element.elementId}' inner shadow is unsupported on stock uGUI.", element.elementId);
            if (visual.blur > 0f) report.MarkUnsupported("Blur", $"'{element.elementId}' blur is unsupported on stock uGUI.", element.elementId);
            var gradient = go.GetComponent<NXGradient>();
            if (visual.gradient != null && graphic != null)
            {
                if (gradient == null) gradient = AddManagedComponent<NXGradient>(go);
                gradient.StartColor = visual.gradient.Evaluate(0f);
                gradient.EndColor = visual.gradient.Evaluate(1f);
                gradient.Angle = 90f;
            }
            else if (gradient != null)
                RemoveManagedComponents<NXGradient>(go, report, element.elementId);
        }

        private static void ApplyAutoLayout(GameObject go, DesignerElementMetadata element, DesignerSaveReport report)
        {
            var layout = element.autoLayout;
            if (layout == null) return;
            var layoutElement = go.GetComponent<LayoutElement>();
            if (layout.widthSizing == DesignerAutoLayoutSizing.Fill || layout.heightSizing == DesignerAutoLayoutSizing.Fill)
            {
                if (layoutElement == null) layoutElement = AddManagedComponent<LayoutElement>(go);
                layoutElement.flexibleWidth = layout.widthSizing == DesignerAutoLayoutSizing.Fill ? 1f : 0f;
                layoutElement.flexibleHeight = layout.heightSizing == DesignerAutoLayoutSizing.Fill ? 1f : 0f;
            }

            if (!layout.enabled) return;
            var horizontal = go.GetComponent<HorizontalLayoutGroup>();
            var vertical = go.GetComponent<VerticalLayoutGroup>();
            var grid = go.GetComponent<GridLayoutGroup>();
            if (layout.direction != DesignerAutoLayoutDirection.Row && horizontal != null)
                RemoveManagedComponents<HorizontalLayoutGroup>(go, report, element.elementId);
            if (layout.direction != DesignerAutoLayoutDirection.Column && vertical != null)
                RemoveManagedComponents<VerticalLayoutGroup>(go, report, element.elementId);
            if (layout.direction != DesignerAutoLayoutDirection.Grid && grid != null)
                RemoveManagedComponents<GridLayoutGroup>(go, report, element.elementId);

            // Unity allows only one LayoutGroup subtype per GameObject. Removal above only removes
            // serializer-owned helpers, so an incompatible user-authored group may intentionally
            // remain. Do not call AddComponent in that case: Unity would emit an error and return
            // null, and the save would both fail CI and risk hiding the ownership conflict.
            var remainingLayout = go.GetComponent<LayoutGroup>();
            var compatible = layout.direction switch
            {
                DesignerAutoLayoutDirection.Row => remainingLayout == null || remainingLayout is HorizontalLayoutGroup,
                DesignerAutoLayoutDirection.Column => remainingLayout == null || remainingLayout is VerticalLayoutGroup,
                DesignerAutoLayoutDirection.Grid => remainingLayout == null || remainingLayout is GridLayoutGroup,
                _ => true
            };
            if (!compatible)
            {
                report.MarkUserImpact("LayoutGroup",
                    $"Preserved user-authored {remainingLayout.GetType().Name} on '{element.elementId}'; the requested {layout.direction} layout was not added.",
                    element.elementId);
                return;
            }

            if (layout.direction == DesignerAutoLayoutDirection.Grid)
            {
                grid = go.GetComponent<GridLayoutGroup>() ?? AddManagedComponent<GridLayoutGroup>(go);
                if (grid == null)
                {
                    report.MarkUserImpact("LayoutGroup",
                        $"Preserved an incompatible user-authored LayoutGroup on '{element.elementId}'; the requested Grid layout was not added.",
                        element.elementId);
                    return;
                }
                grid.padding = Padding(layout);
                grid.spacing = new Vector2(layout.spacing, layout.spacing);
                grid.cellSize = new Vector2(Mathf.Max(1f, layout.gridCellWidth), Mathf.Max(1f, layout.gridCellHeight));
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = Mathf.Max(1, layout.gridColumns);
            }
            else
            {
                HorizontalOrVerticalLayoutGroup group = layout.direction == DesignerAutoLayoutDirection.Row
                    ? (HorizontalOrVerticalLayoutGroup)(go.GetComponent<HorizontalLayoutGroup>() ?? AddManagedComponent<HorizontalLayoutGroup>(go))
                    : go.GetComponent<VerticalLayoutGroup>() ?? AddManagedComponent<VerticalLayoutGroup>(go);
                if (group == null)
                {
                    report.MarkUserImpact("LayoutGroup",
                        $"Preserved an incompatible user-authored LayoutGroup on '{element.elementId}'; the requested {layout.direction} layout was not added.",
                        element.elementId);
                    return;
                }
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

            var minimum = DesignerComponentPropertyAccess.GetFloat(element, "value.min", element.fill.minValue);
            var maximum = DesignerComponentPropertyAccess.GetFloat(element, "value.max", element.fill.maxValue);
            if (maximum <= minimum) maximum = minimum + 1f;
            var previewValue = DesignerComponentPropertyAccess.GetBool(element, "value.wholeNumbers")
                ? Mathf.Round(element.previewValue)
                : element.previewValue;
            var normalized = Mathf.Clamp01(Mathf.InverseLerp(minimum, maximum, previewValue));
            img.fillAmount = normalized;

            if (Is(element.elementType ?? "", "RadialFill"))
            {
                img.fillMethod = Image.FillMethod.Radial360;
                img.fillOrigin = (int)Image.Origin360.Bottom;
                img.fillClockwise = element.fill.clockwise;
            }
            else
            {
                var direction = DesignerComponentPropertyAccess.GetEnum(element, "value.direction") switch
                {
                    "RightToLeft" => DesignerFillDirection.RightToLeft,
                    "BottomToTop" => DesignerFillDirection.BottomToTop,
                    "TopToBottom" => DesignerFillDirection.TopToBottom,
                    _ => element.fill.direction
                };
                switch (direction)
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

            if (tmp != null)
            {
                if (tmp.font == null)
                    tmp.font = element.typography?.fontAsset as TMP_FontAsset ?? DefaultTmpFont();
                if (tmp.font != null)
                {
                    if (element.text != null) tmp.text = element.text;
                    ApplyTextStyle(tmp, element);
                    return;
                }

                // TMP stock controls can exist before TMP Essential Resources are imported. A
                // null-font TMP component is invisible and material properties throw, so replace
                // text-only/button captions with a working built-in Text fallback.
                var fallbackHost = tmp.gameObject;
                Object.DestroyImmediate(tmp);
                uiText = fallbackHost.GetComponent<Text>() ?? fallbackHost.AddComponent<Text>();
                uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                uiText.alignment = TextAnchor.MiddleCenter;
                report.MarkUserImpact("Typography fallback",
                    $"'{element.elementId}' used legacy uGUI Text because TMP Essential Resources are not installed.",
                    element.elementId);
            }
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
            var defaultTmpFont = DefaultTmpFont();
            if (defaultTmpFont != null)
            {
                var newText = host.AddComponent<TextMeshProUGUI>();
                newText.font = defaultTmpFont;
                newText.text = element.text;
                newText.alignment = TextAlignmentOptions.Center;
                ApplyTextStyle(newText, element);
            }
            else
            {
                // Clean projects do not necessarily have TMP Essential Resources imported. Saving
                // must still succeed; use Unity's built-in legacy font and report the fallback.
                var legacyText = host.AddComponent<Text>();
                legacyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                legacyText.text = element.text;
                legacyText.alignment = TextAnchor.MiddleCenter;
                ApplyTextStyle(legacyText, element);
                report.MarkUserImpact("Typography fallback",
                    $"'{element.elementId}' used legacy uGUI Text because TMP Essential Resources are not installed.",
                    element.elementId);
            }
            report.MarkChanged($"Added text to '{element.elementId}'");
        }

        private static TMP_FontAsset DefaultTmpFont()
        {
            try
            {
                // TMP_Settings.defaultFontAsset throws NullReferenceException when a clean project
                // has not imported TMP Essential Resources yet.
                return TMP_Settings.defaultFontAsset;
            }
            catch (System.NullReferenceException)
            {
                return null;
            }
        }

        /// <summary>
        /// The Graphic that draws the element's background, ignoring one that draws its text.
        /// </summary>
        /// <remarks>
        /// <c>GetComponent&lt;Graphic&gt;()</c> cannot tell them apart: TMP_Text and Text are both
        /// Graphics, so on a label it returns the text itself. Every caller here means "the surface
        /// behind the content", and text colour is owned by <see cref="ApplyTextStyle"/>.
        /// </remarks>
        private static Graphic BackgroundGraphicOf(GameObject go)
        {
            var graphics = go.GetComponents<Graphic>();
            for (int i = 0; i < graphics.Length; i++)
            {
                var graphic = graphics[i];
                if (graphic is TMP_Text || graphic is Text) continue;
                return graphic;
            }
            return null;
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
            if (tmp.fontSharedMaterial != null)
            {
                tmp.outlineWidth = typography.outlineWidth;
                tmp.outlineColor = typography.outlineColor;
            }
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
                shadow = shadow ?? AddManagedComponent<UnityEngine.UI.Shadow>(graphic.gameObject);
                shadow.effectColor = typography.shadowColor;
                shadow.effectDistance = typography.shadowOffset;
            }
            else if (shadow != null)
                RemoveManagedComponents<UnityEngine.UI.Shadow>(graphic.gameObject, null, null,
                    candidate => !(candidate is UnityEngine.UI.Outline));
        }

        private static T AddManagedComponent<T>(GameObject go) where T : Component
        {
            var component = go.AddComponent<T>();
            if (component == null) return null;
            var tracker = go.GetComponent<DesignerAttachedComponentTracker>() ?? go.AddComponent<DesignerAttachedComponentTracker>();
            tracker.managedGeneratedComponents ??= new List<Component>();
            tracker.managedGeneratedComponents.Add(component);
            return component;
        }

        private static void RemoveManagedComponents<T>(GameObject go, DesignerSaveReport report, string elementId,
            System.Predicate<T> predicate = null) where T : Component
        {
            var tracker = go.GetComponent<DesignerAttachedComponentTracker>();
            var removed = false;
            if (tracker?.managedGeneratedComponents != null)
                for (var i = tracker.managedGeneratedComponents.Count - 1; i >= 0; i--)
                {
                    if (!(tracker.managedGeneratedComponents[i] is T component) || component.gameObject != go ||
                        (predicate != null && !predicate(component))) continue;
                    tracker.managedGeneratedComponents.RemoveAt(i);
                    Object.DestroyImmediate(component);
                    removed = true;
                }

            if (!removed && go.GetComponent<T>() != null && report != null)
                report.MarkUserImpact(typeof(T).Name,
                    $"Preserved user-authored {typeof(T).Name} on '{elementId}'.", elementId);
            CleanupTracker(tracker);
        }

        private static void CleanupTracker(DesignerAttachedComponentTracker tracker)
        {
            if (tracker == null) return;
            tracker.Prune();
            // IsEmpty covers managedByInstance too: dropping the tracker while it still owns a
            // component would make that component look user-authored on the next save.
            if (tracker.IsEmpty) Object.DestroyImmediate(tracker);
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

    }
}

using System;
using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Components.Serialization;
using emiteat.NexUI.Integrations.UGUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Designer.Editor.Serialization
{
    /// <summary>
    /// Reads an existing uGUI prefab into Studio metadata: the other half of the round trip.
    /// </summary>
    /// <remarks>
    /// Import is deliberately <b>read-only on the prefab</b>. Stamping identity tags at import time
    /// would modify an asset the user only asked to look at; instead the generated stable ids live in
    /// metadata, and the first Save writes the tags through the existing ownership-preserving path -
    /// where every object that already existed is marked UserOwned and can never be auto-deleted.
    ///
    /// Every component is imported by <b>property path</b>, not through the curated palette schema.
    /// The schema covers the fields a designer usually touches; a prefab someone actually shipped
    /// contains far more than that, and anything the schema does not name would be lost on the way
    /// back out.
    /// </remarks>
    public static class StudioPrefabImporter
    {
        /// <summary>What a call to <see cref="Import"/> produced.</summary>
        public sealed class Result
        {
            public DesignerSaveReport Report = new DesignerSaveReport();
            public List<DesignerElementMetadata> Elements = new List<DesignerElementMetadata>();
            public int ComponentCount;
            public int ValueCount;
        }

        /// <summary>Components the Studio owns as bookkeeping; they describe the mapping, not the UI.</summary>
        private static readonly HashSet<Type> Bookkeeping = new HashSet<Type>
        {
            typeof(NxUGuiBindingTag), typeof(DesignerAttachedComponentTracker)
        };

        /// <summary>
        /// Builds elements for every descendant of <paramref name="root"/>. The root itself maps to
        /// the screen, so it does not become an element.
        /// </summary>
        /// <param name="root">
        /// The prefab's root. Pass the loaded prefab contents or the asset itself - the importer only
        /// reads.
        /// </param>
        public static Result Import(GameObject root)
        {
            var result = new Result();
            if (root == null)
            {
                result.Report.Error("No prefab was given to import.");
                return result;
            }

            // Pass 1: identity for every object, so a reference found in pass 2 can name its target
            // no matter where in the hierarchy it lives.
            var identities = new Dictionary<GameObject, DesignerElementMetadata>();
            var ordered = new List<(GameObject Object, DesignerElementMetadata Element)>();
            var usedIds = new HashSet<string>(StringComparer.Ordinal);
            Walk(root.transform, null, identities, ordered, usedIds, result);

            // Pass 2: geometry and components, parents first. A child's canvas rect is expressed
            // relative to its parent's, so the parent's has to be resolved before the child's.
            foreach (var (go, element) in ordered)
                ImportObject(go, element, identities, result);

            result.Report.MarkCreated("Prefab import",
                $"Imported {result.Elements.Count} element(s), {result.ComponentCount} component(s) and {result.ValueCount} authored value(s) from '{root.name}'.");
            return result;
        }

        /// <summary>
        /// Imports <paramref name="root"/> into an existing metadata asset, merging by stable id.
        /// </summary>
        /// <remarks>
        /// Merging rather than replacing is what makes re-import safe. Everything the prefab is
        /// authoritative for - hierarchy, geometry, the component stack - is overwritten; everything
        /// that only exists in the Studio - bindings, motion, theme, focus links, classes - is left
        /// exactly as the user authored it, because the prefab has no opinion about any of it.
        ///
        /// Elements with no counterpart in the prefab are reported, never deleted: the object may
        /// simply not have been created yet, and deleting the element would throw away its bindings.
        /// </remarks>
        public static Result ImportInto(DesignerMetadataAsset metadata, GameObject root)
        {
            var result = Import(root);
            if (metadata == null || result.Report.HasErrors) return result;

            var byStableId = new Dictionary<string, DesignerElementMetadata>(StringComparer.Ordinal);
            foreach (var existing in metadata.elements)
                if (existing != null && !string.IsNullOrEmpty(existing.stableId))
                    byStableId[existing.stableId] = existing;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var imported in result.Elements)
            {
                seen.Add(imported.stableId);
                if (!byStableId.TryGetValue(imported.stableId, out var target))
                {
                    metadata.elements.Add(imported);
                    result.Report.MarkCreated("Element", $"Added element '{imported.elementId}'.", imported.elementId);
                    continue;
                }

                target.elementId = imported.elementId;
                target.displayName = imported.displayName;
                target.parentId = imported.parentId;
                target.siblingIndex = imported.siblingIndex;
                target.rect = imported.rect;
                target.anchorPreset = imported.anchorPreset;
                target.runtimeVisible = imported.runtimeVisible;
                target.elementType = imported.elementType;
                target.components = imported.components;
                result.Report.MarkModified("Element",
                    $"Updated element '{imported.elementId}' from the prefab.", imported.elementId);
            }

            foreach (var existing in metadata.elements)
            {
                if (existing == null || string.IsNullOrEmpty(existing.stableId)) continue;
                if (seen.Contains(existing.stableId)) continue;
                result.Report.MarkOrphan("Element",
                    $"'{existing.elementId}' has no object in the prefab. It was kept - saving the screen will create it.",
                    existing.elementId);
            }

            DesignerHierarchyUtility.NormalizeSiblingIndices(metadata);
            return result;
        }

        // ---- Identity -------------------------------------------------------------------------

        private static void Walk(Transform parent, DesignerElementMetadata parentElement,
            Dictionary<GameObject, DesignerElementMetadata> identities,
            List<(GameObject, DesignerElementMetadata)> ordered, HashSet<string> usedIds, Result result)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                var tag = child.GetComponent<NxUGuiBindingTag>();

                var element = new DesignerElementMetadata
                {
                    // An object the Studio has seen before keeps the identity it already has, so
                    // re-importing a prefab updates the same elements instead of duplicating them.
                    stableId = tag != null && !string.IsNullOrEmpty(tag.stableId)
                        ? tag.stableId
                        : Guid.NewGuid().ToString("N"),
                    elementId = UniqueId(tag != null && !string.IsNullOrEmpty(tag.elementId)
                        ? tag.elementId
                        : child.name, usedIds),
                    displayName = child.name,
                    parentId = parentElement?.elementId,
                    siblingIndex = i,
                    runtimeVisible = child.gameObject.activeSelf
                };

                identities[child.gameObject] = element;
                ordered.Add((child.gameObject, element));
                result.Elements.Add(element);
                Walk(child, element, identities, ordered, usedIds, result);
            }
        }

        /// <summary>
        /// Element ids have to be unique across a screen, but prefab names do not have to be unique
        /// across siblings. Renaming the GameObject to fix that would edit the user's prefab, so the
        /// suffix goes on the element id and the original name is kept as the display name.
        /// </summary>
        private static string UniqueId(string preferred, HashSet<string> used)
        {
            var baseId = string.IsNullOrWhiteSpace(preferred) ? "element" : preferred.Trim();
            var candidate = baseId;
            for (var suffix = 2; !used.Add(candidate); suffix++) candidate = baseId + suffix;
            return candidate;
        }

        // ---- Geometry and components ------------------------------------------------------------

        private static void ImportObject(GameObject go, DesignerElementMetadata element,
            Dictionary<GameObject, DesignerElementMetadata> identities, Result result)
        {
            ImportRect(go, element, identities, result.Report);
            element.elementType = InferElementType(go);

            foreach (var component in go.GetComponents<Component>())
            {
                if (component == null)
                {
                    // A missing script leaves a null slot. Reporting it is the only honest option:
                    // there is no type name to preserve, so re-saving cannot recreate it.
                    result.Report.MarkUnsupported("Missing script",
                        $"'{element.elementId}' has a component whose script is missing. It cannot be imported and will not be recreated on save.",
                        element.elementId);
                    continue;
                }
                if (component is Transform) continue;
                if (Bookkeeping.Contains(component.GetType())) continue;

                var entry = ToMetadata(component, element, identities, result);
                element.components.Add(entry);
                result.ComponentCount++;
                result.ValueCount += entry.properties.Count;
            }
        }

        private static DesignerElementComponent ToMetadata(Component component,
            DesignerElementMetadata element, Dictionary<GameObject, DesignerElementMetadata> identities,
            Result result)
        {
            var type = component.GetType();
            var qualifiedName = StudioComponentTypeIndex.Identity(type);
            var entry = new DesignerElementComponent
            {
                typeId = RegistryIdFor(type) ?? DesignerProjectComponentIds.FromQualifiedName(qualifiedName),
                source = SourceOf(type),
                assemblyQualifiedTypeName = qualifiedName,
                enabled = !(component is Behaviour behaviour) || behaviour.enabled,
                valueFormat = DesignerComponentValueFormat.PropertyPath,
                adoptExistingComponent = true
            };

            var unsupported = new List<string>();
            StudioSerializedComponentBridge.CaptureFrom(component, entry,
                target => Resolve(target, identities), unsupported);

            foreach (var key in unsupported)
                result.Report.MarkUnsupported(type.Name,
                    $"'{element.elementId}' {type.Name}.{key} uses a value shape this build cannot represent. " +
                    "It is left on the prefab untouched and is not tracked by the Studio.",
                    element.elementId);

            return entry;
        }

        /// <summary>The stable id of the element being created for <paramref name="target"/>, if any.</summary>
        private static string Resolve(UnityEngine.Object target,
            Dictionary<GameObject, DesignerElementMetadata> identities)
        {
            var go = target as GameObject ?? (target as Component)?.gameObject;
            if (go == null) return null;
            return identities.TryGetValue(go, out var element) ? element.stableId : null;
        }

        private static void ImportRect(GameObject go, DesignerElementMetadata element,
            Dictionary<GameObject, DesignerElementMetadata> identities, DesignerSaveReport report)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null)
            {
                report.MarkUnsupported("RectTransform",
                    $"'{element.elementId}' has no RectTransform, so it has no layout the Studio canvas can show.",
                    element.elementId);
                return;
            }

            var parent = rt.parent as RectTransform;
            var size = rt.rect.size;
            var local = Vector2.zero;

            if (parent != null)
            {
                // Corner-to-corner in the parent's own space, then flipped to the Studio's top-left
                // origin. Going through the corners rather than anchoredPosition is what makes an
                // arbitrarily anchored or stretched rect import at the position it visually occupies.
                var min = (Vector2)parent.InverseTransformPoint(rt.TransformPoint(rt.rect.min));
                var max = (Vector2)parent.InverseTransformPoint(rt.TransformPoint(rt.rect.max));
                size = max - min;
                local = new Vector2(min.x - parent.rect.xMin, parent.rect.yMax - max.y);
            }
            else
            {
                local = new Vector2(rt.anchoredPosition.x, -rt.anchoredPosition.y);
            }

            var parentElement = identities.TryGetValue(
                go.transform.parent != null ? go.transform.parent.gameObject : go, out var found) ? found : null;
            var origin = parentElement != null ? parentElement.rect.position : Vector2.zero;

            element.rect = new Rect(origin + local, size);
            element.anchorPreset = UGUIAnchorUtility.Detect(rt, out var exact);
            if (!exact)
                report.MarkUnsupported("Anchoring",
                    $"'{element.elementId}' uses custom anchors that do not match any Studio preset. " +
                    "Its position and size were imported exactly; the anchor preset shown is an approximation.",
                    element.elementId);
        }

        // ---- Classification ---------------------------------------------------------------------

        /// <summary>
        /// The registry id for a type the palette already knows, so an imported Image is recognised as
        /// the same kind of thing a palette Image is. Values still travel by property path - the id is
        /// for display, conflicts and Add Component rules, not for the writer.
        /// </summary>
        private static string RegistryIdFor(Type type)
        {
            foreach (var candidate in DesignerUIComponentRegistry.All)
                if (candidate.BackingType == type) return candidate.TypeId;
            return null;
        }

        private static DesignerComponentSource SourceOf(Type type)
            => StudioComponentTypeIndex.OriginOf(type) switch
            {
                StudioComponentOrigin.NexUI => DesignerComponentSource.NexUI,
                StudioComponentOrigin.UGUI => DesignerComponentSource.UGUI,
                StudioComponentOrigin.Unity => DesignerComponentSource.Unity,
                _ => DesignerComponentSource.Project
            };

        /// <summary>
        /// A coarse label for the canvas preview and the hierarchy icon. It is not authoritative -
        /// the component stack is - so guessing wrong costs a preview icon, not data.
        /// </summary>
        private static string InferElementType(GameObject go)
        {
            if (go.GetComponent<Button>() != null) return "Button";
            if (go.GetComponent<Toggle>() != null) return "Toggle";
            if (go.GetComponent<Slider>() != null) return "Slider";
            if (go.GetComponent<ScrollRect>() != null) return "ScrollView";
            if (go.GetComponent<TMP_Text>() != null || go.GetComponent<Text>() != null) return "Text";
            if (go.GetComponent<Image>() != null || go.GetComponent<RawImage>() != null) return "Image";
            return "Panel";
        }
    }
}

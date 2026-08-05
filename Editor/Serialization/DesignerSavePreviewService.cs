using System;
using System.Collections.Generic;
using System.IO;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor.Components.Definitions;
using emiteat.NexUI.Designer.Editor.Properties;
using emiteat.NexUI.Integrations.UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Serialization
{
    /// <summary>Builds a read-only, categorized plan of the next save.</summary>
    public static class DesignerSavePreviewService
    {
        /// <param name="variantContext">
        /// Canvas resolution / input mode, so the plan reflects the same variant rules the canvas is
        /// showing and the save will write.
        /// </param>
        public static DesignerSaveReport Preview(UIScreenDefinition definition, DesignerMetadataAsset metadata,
            Components.Definitions.DesignerComponentVariantContext variantContext = default)
        {
            var report = new DesignerSaveReport { IsPreview = true };
            if (definition == null)
            {
                report.MarkConflict("Screen", "No screen is open; save cannot be planned.");
                return report;
            }

            if (metadata == null)
            {
                report.MarkPreviewOnly("Metadata", "No Designer metadata is linked; only the screen definition can be saved.");
                return report;
            }

            report.MarkModified("Metadata", "Designer metadata and companion JSON will be synchronized.", path: AssetDatabase.GetAssetPath(metadata));

            // The plan must describe what the backend actually receives, which for a screen with
            // component instances is the flattened tree - otherwise the dry run would under-report
            // every object a component contributes.
            var expansion = DesignerComponentExpander.Expand(metadata, DesignerComponentLibrary.Resolver, variantContext);
            try
            {
                foreach (var issue in expansion.Issues)
                    report.MarkConflict("Component", $"{issue.InstanceElementId}: {issue.Message}");

                var effective = expansion.Expanded ?? metadata;
                if (definition.backendAsset.backend == UIRenderBackend.UGUI)
                    PreviewUgui(definition, effective, report);
                else
                    PreviewUiToolkit(definition, effective, report);
            }
            finally
            {
                expansion.Dispose();
            }
            return report;
        }

        private static void PreviewUgui(UIScreenDefinition definition, DesignerMetadataAsset metadata, DesignerSaveReport report)
        {
            var prefab = definition.backendAsset.asset as GameObject;
            var path = AssetDatabase.GetAssetPath(prefab);
            if (prefab == null || string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                report.MarkSkipped("No valid uGUI prefab is assigned; backend changes will not be written.");
                return;
            }

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                var tags = root.GetComponentsInChildren<NxUGuiBindingTag>(true);
                var byStableId = new Dictionary<string, NxUGuiBindingTag>(StringComparer.Ordinal);
                var byElementId = new Dictionary<string, NxUGuiBindingTag>(StringComparer.Ordinal);
                var byName = new Dictionary<string, GameObject>(StringComparer.Ordinal);
                var ambiguousStableIds = new HashSet<string>(StringComparer.Ordinal);
                var ambiguousElementIds = new HashSet<string>(StringComparer.Ordinal);
                var ambiguousNames = new HashSet<string>(StringComparer.Ordinal);
                var referenced = new HashSet<NxUGuiBindingTag>();
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    AddUnique(byName, ambiguousNames, transform.name, transform.gameObject, report, "GameObject name");
                foreach (var tag in tags)
                {
                    AddUnique(byStableId, ambiguousStableIds, tag.stableId, tag, report, "stableId");
                    AddUnique(byElementId, ambiguousElementIds, tag.elementId, tag, report, "elementId");
                }

                var elementIds = new HashSet<string>(StringComparer.Ordinal);
                var stableIds = new HashSet<string>(StringComparer.Ordinal);
                // Objects that already exist. The dry run reports its overwrite scope against these:
                // an element it is about to create has nothing on it yet that could be the user's.
                var matched = new Dictionary<string, GameObject>(StringComparer.Ordinal);
                foreach (var element in metadata.elements)
                {
                    if (element == null || string.IsNullOrWhiteSpace(element.elementId)) continue;
                    if (!elementIds.Add(element.elementId))
                    {
                        report.MarkConflict("Identity", $"Duplicate metadata elementId '{element.elementId}'.", element.elementId, path);
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(element.stableId) || !stableIds.Add(element.stableId))
                    {
                        report.MarkConflict("Identity", $"Element '{element.elementId}' has a missing or duplicate stableId.", element.elementId, path);
                        continue;
                    }

                    GameObject matchObject = null;
                    NxUGuiBindingTag matchTag = null;
                    if (byStableId.TryGetValue(element.stableId, out matchTag) ||
                        byElementId.TryGetValue(element.elementId, out matchTag))
                        matchObject = matchTag.gameObject;
                    else if (!ambiguousNames.Contains(element.elementId))
                        byName.TryGetValue(element.elementId, out matchObject);
                    if (matchObject == null)
                        report.MarkCreated("Prefab element", $"Create '{element.elementId}' and apply Designer-owned layout/style.", element.elementId, path);
                    else
                    {
                        if (matchTag != null) referenced.Add(matchTag);
                        matched[element.elementId] = matchObject;
                        report.MarkModified("Prefab element", $"Update '{element.elementId}' layout, visual, typography, hierarchy and visibility.", element.elementId, path);
                        if (matchTag == null || matchTag.ownership != NexUIElementOwnership.DesignerOwned)
                            report.MarkUserImpact("User-owned object", $"Designer properties will be applied to user-owned '{matchObject.name}', while unrelated components remain intact. A stable identity tag will be added when needed.", element.elementId);
                    }
                    ReportPropertyParity(element, UIRenderBackend.UGUI, report);
                }

                foreach (var tag in tags)
                    if (tag.ownership == NexUIElementOwnership.DesignerOwned && !referenced.Contains(tag))
                        report.MarkOrphan("Prefab element", $"Orphaned Designer-owned '{tag.name}' is preserved and requires manual removal.", tag.elementId);

                // The same statement the real save makes, so the dry run and the result agree on where
                // the overwrite boundary is rather than only on what crosses it.
                UGUIAssetSerializer.ReportOwnership(root, metadata, matched, report);
            }
            catch (Exception ex)
            {
                report.MarkConflict("Prefab", $"Prefab could not be inspected: {ex.Message}", path: path);
            }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AddUnique(Dictionary<string, NxUGuiBindingTag> index, HashSet<string> ambiguous,
            string key, NxUGuiBindingTag value, DesignerSaveReport report, string identityKind)
        {
            if (string.IsNullOrEmpty(key) || ambiguous.Contains(key)) return;
            if (index.ContainsKey(key))
            {
                index.Remove(key);
                ambiguous.Add(key);
                report.MarkConflict("Prefab identity", $"Duplicate prefab {identityKind} '{key}' prevents deterministic matching.");
                return;
            }
            index.Add(key, value);
        }

        private static void AddUnique(Dictionary<string, GameObject> index, HashSet<string> ambiguous,
            string key, GameObject value, DesignerSaveReport report, string identityKind)
        {
            if (string.IsNullOrEmpty(key) || ambiguous.Contains(key)) return;
            if (index.ContainsKey(key))
            {
                index.Remove(key);
                ambiguous.Add(key);
                report.Warn($"Duplicate prefab {identityKind} '{key}'; name fallback matching is disabled for it.");
                return;
            }
            index.Add(key, value);
        }

        private static void PreviewUiToolkit(UIScreenDefinition definition, DesignerMetadataAsset metadata, DesignerSaveReport report)
        {
            var vta = definition.backendAsset.asset as VisualTreeAsset;
            if (vta == null)
            {
                report.MarkSkipped("No UXML is assigned; metadata remains the only save target.");
                return;
            }

            var path = AssetDatabase.GetAssetPath(vta);
            var generated = !string.IsNullOrEmpty(path) && File.Exists(path) &&
                            File.ReadAllText(path).Contains(GeneratedAssetWriter.GeneratedMarker);
            if (generated)
            {
                var ussPath = Path.ChangeExtension(path, ".uss").Replace('\\', '/');
                var result = new GeneratedAssetWriter().Write(new[]
                {
                    new GeneratedAssetFile(path, UIToolkitCodeGenerator.GenerateUxml(metadata, Path.GetFileName(ussPath))),
                    new GeneratedAssetFile(ussPath, UIToolkitCodeGenerator.GenerateUss(metadata))
                }, dryRun: true);
                foreach (var changed in result.ChangedPaths)
                {
                    if (File.Exists(changed)) report.MarkModified("Generated asset", $"Regenerate '{changed}'.", path: changed);
                    else report.MarkCreated("Generated asset", $"Create '{changed}'.", path: changed);
                }
                foreach (var unchanged in result.UnchangedPaths) report.MarkSkipped($"Generated '{unchanged}' is unchanged.");
                foreach (var error in result.Errors) report.MarkConflict("Generated asset", error, path: path);
            }
            else
            {
                report.MarkPreviewOnly("Hand-authored UXML", "UXML structure is preserved; Designer changes remain metadata/preview-only until applied in UI Builder.");
                var names = UIToolkitAssetSerializer.CollectElementNames(vta);
                foreach (var element in metadata.elements)
                    if (element != null && !string.IsNullOrEmpty(element.elementId) && !names.Contains(element.elementId))
                        report.MarkPreviewOnly("Unmatched element", $"'{element.elementId}' has no named VisualElement and will not appear in the hand-authored UXML.", element.elementId);
            }

            foreach (var element in metadata.elements)
                if (element != null) ReportPropertyParity(element, UIRenderBackend.UIToolkit, report);
        }

        private static void ReportPropertyParity(DesignerElementMetadata element, UIRenderBackend backend, DesignerSaveReport report)
        {
            var component = Components.DesignerComponentRegistry.Get(element.elementType);
            var componentSupport = backend == UIRenderBackend.UGUI ? component.UGUISupport : component.UIToolkitSupport;
            if (componentSupport == Components.DesignerBackendSupport.PreviewOnly)
                report.MarkPreviewOnly(component.DisplayName,
                    $"'{element.elementId}' ({component.TypeId}) is preview-only on {backend}.", element.elementId);
            else if (componentSupport == Components.DesignerBackendSupport.Unsupported)
                report.MarkUnsupported(component.DisplayName,
                    $"'{element.elementId}' ({component.TypeId}) is unsupported on {backend}.", element.elementId);

            // Scripts live in element.components now, so the dry run counts the same entries the
            // writer will act on rather than the legacy list, which is read-only from v6 onwards.
            var scripts = 0;
            var values = 0;
            foreach (var entry in element.components ?? new List<DesignerElementComponent>())
            {
                if (entry == null || string.IsNullOrEmpty(entry.typeId)) continue;
                if (Components.DesignerUIComponentRegistry.IsRegistered(entry.typeId)) continue;
                scripts++;
                values += entry.properties?.Count ?? 0;
            }
            if (scripts > 0)
            {
                if (backend == UIRenderBackend.UGUI)
                    report.MarkModified("Components",
                        $"Synchronize {scripts} MonoBehaviour(s) and {values} authored value(s) on '{element.elementId}'.",
                        element.elementId);
                else
                    report.MarkPreviewOnly("Components",
                        $"'{element.elementId}' MonoBehaviour attachments are uGUI-only and remain in metadata.", element.elementId);
            }

            foreach (var entry in element.componentProperties ?? new List<DesignerComponentPropertyEntry>())
            {
                if (entry == null || string.IsNullOrEmpty(entry.key)) continue;
                var property = Components.DesignerComponentPropertyAccess.Find(element, entry.key);
                if (property == null)
                {
                    report.MarkUserImpact("Unknown component property",
                        $"'{element.elementId}' keeps newer/custom property '{entry.key}' in metadata, but this Designer cannot write it to {backend}.",
                        element.elementId);
                    continue;
                }

                var support = backend == UIRenderBackend.UGUI
                    ? Components.DesignerComponentPropertySupport.UGUI(component, property)
                    : Components.DesignerComponentPropertySupport.UIToolkit(component, property);
                if (support == Components.DesignerBackendSupport.Unsupported)
                    report.MarkUnsupported(property.DisplayName,
                        $"'{element.elementId}' property '{property.Key}' is unsupported on {backend}.", element.elementId);
                else if (support == Components.DesignerBackendSupport.PreviewOnly)
                    report.MarkPreviewOnly(property.DisplayName,
                        $"'{element.elementId}' property '{property.Key}' remains preview/metadata-only on {backend}.", element.elementId);
                else if (support == Components.DesignerBackendSupport.Partial)
                    report.MarkUserImpact(property.DisplayName,
                        $"'{element.elementId}' property '{property.Key}' needs NexUI runtime behavior or a backend-specific adapter on {backend}.",
                        element.elementId);
            }

            foreach (var property in ActiveLimitedProperties(element))
            {
                var descriptor = DesignerPropertyRegistry.Get(property);
                if (descriptor == null) continue;
                var support = backend == UIRenderBackend.UGUI ? descriptor.UGUI : descriptor.UIToolkit;
                var fallback = backend == UIRenderBackend.UGUI ? descriptor.UGUIFallback : descriptor.UIToolkitFallback;
                if (support == DesignerPropertyBackendSupport.Unsupported)
                    report.MarkUnsupported(descriptor.DisplayName,
                        $"'{element.elementId}' uses {descriptor.Path}, which {backend} cannot serialize.", element.elementId);
                else if (support == DesignerPropertyBackendSupport.PreviewOnly)
                    report.MarkPreviewOnly(descriptor.DisplayName,
                        $"'{element.elementId}' uses {descriptor.Path}, which remains preview-only on {backend}.", element.elementId);
                else if (support == DesignerPropertyBackendSupport.Fallback)
                    report.MarkUserImpact(descriptor.DisplayName,
                        $"'{element.elementId}' uses a {backend} fallback for {descriptor.Path}. {fallback}", element.elementId);
            }
        }

        private static IEnumerable<DesignerPropertyId> ActiveLimitedProperties(DesignerElementMetadata element)
        {
            var layout = element.layoutStyle;
            if (layout != null && layout.hasOverrides)
            {
                if (layout.maxSize.x > 0f) yield return DesignerPropertyId.MaxWidth;
                if (layout.maxSize.y > 0f) yield return DesignerPropertyId.MaxHeight;
                if (layout.wrap == DesignerLayoutWrap.Wrap) yield return DesignerPropertyId.Wrap;
                if (layout.justify == DesignerJustifyContent.SpaceAround || layout.justify == DesignerJustifyContent.SpaceBetween)
                    yield return DesignerPropertyId.Justify;
            }
            var visual = element.visualStyle;
            if (visual != null && visual.hasOverrides)
            {
                if (visual.gradient != null) yield return DesignerPropertyId.Gradient;
                if (visual.borderWidth > 0f) yield return DesignerPropertyId.BorderWidth;
                if (visual.cornerRadius > 0f) yield return DesignerPropertyId.CornerRadius;
                if (visual.dropShadow) yield return DesignerPropertyId.DropShadow;
                if (visual.innerShadow) yield return DesignerPropertyId.InnerShadow;
                if (visual.outlineWidth > 0f) yield return DesignerPropertyId.OutlineWidth;
                if (visual.blur > 0f) yield return DesignerPropertyId.Blur;
                if (visual.material != null) yield return DesignerPropertyId.Material;
            }
            var typography = element.typography;
            if (typography != null && typography.hasOverrides)
            {
                if (typography.fontAsset != null) yield return DesignerPropertyId.FontAsset;
                if (!string.IsNullOrEmpty(typography.fontFamily)) yield return DesignerPropertyId.FontFamily;
                if (typography.autoSize) yield return DesignerPropertyId.AutoFontSize;
                if (typography.ellipsis) yield return DesignerPropertyId.Ellipsis;
                if (Mathf.Abs(typography.lineHeight - 1.2f) > 0.001f) yield return DesignerPropertyId.LineHeight;
                if (Mathf.Abs(typography.paragraphSpacing) > 0.001f) yield return DesignerPropertyId.ParagraphSpacing;
                if (typography.fontFallback != null) yield return DesignerPropertyId.FontFallback;
                if (typography.rightToLeft) yield return DesignerPropertyId.RightToLeft;
                if (typography.outlineWidth > 0f) yield return DesignerPropertyId.TextOutline;
            }
        }
    }
}

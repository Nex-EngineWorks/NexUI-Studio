using System.Collections.Generic;
using emiteat.NexUI.Core;
using UnityEditor;
using UnityEngine.UIElements;
using System.IO;
using emiteat.NexUI.Designer.Editor.Components;

namespace emiteat.NexUI.Designer.Editor.Serialization
{
    /// <summary>
    /// Persists Designer metadata for a UI Toolkit screen.
    ///
    /// IMPORTANT: Unity exposes no public API to rewrite a UXML file from a VisualTreeAsset
    /// in editor code without risking loss of user-authored attributes / templates. Therefore
    /// this serializer runs in <b>companion-save mode</b>: Designer metadata is saved reliably,
    /// the UXML is treated as authored in UI Builder, and the serializer only <b>validates and
    /// reports</b> mismatches between metadata and the UXML tree. It never claims to have
    /// written the UXML. Structural UXML edits stay the responsibility of UI Builder.
    /// </summary>
    public sealed class UIToolkitAssetSerializer : IDesignerAssetSerializer
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

            var vta = definition != null ? definition.backendAsset.asset as VisualTreeAsset : null;
            if (vta == null)
            {
                report.MarkSkipped("No UXML (VisualTreeAsset) assigned to the screen backend asset (metadata saved only).");
                SaveDirtyAssets(metadata, definition);
                return report;
            }

            var uxmlPath = AssetDatabase.GetAssetPath(vta);
            var isGenerated = !string.IsNullOrEmpty(uxmlPath) && File.Exists(uxmlPath) &&
                              File.ReadAllText(uxmlPath).Contains(GeneratedAssetWriter.GeneratedMarker);
            if (isGenerated && metadata != null)
            {
                var ussPath = Path.ChangeExtension(uxmlPath, ".uss").Replace('\\', '/');
                var writer = new GeneratedAssetWriter();
                var write = writer.Write(new[]
                {
                    new GeneratedAssetFile(uxmlPath, UIToolkitCodeGenerator.GenerateUxml(metadata, Path.GetFileName(ussPath))),
                    new GeneratedAssetFile(ussPath, UIToolkitCodeGenerator.GenerateUss(metadata))
                });
                if (write.Success)
                {
                    foreach (var changed in write.ChangedPaths) report.MarkChanged($"Generated '{changed}'");
                    foreach (var unchanged in write.UnchangedPaths) report.MarkSkipped($"Generated '{unchanged}' is unchanged.");
                }
                else foreach (var error in write.Errors) report.Error(error);
            }
            else
            {
                report.MarkPreviewOnly("Hand-authored UXML", "Hand-authored UXML is preserved. Use UI Builder or generated .uxml/.uss output.");
            }

            if (metadata != null)
            {
                var names = CollectElementNames(vta);
                foreach (var element in metadata.elements)
                {
                    if (element == null || string.IsNullOrEmpty(element.elementId)) continue;
                    var support = DesignerComponentRegistry.Get(element.elementType).UIToolkitSupport;
                    if (support == DesignerBackendSupport.PreviewOnly)
                        report.MarkPreviewOnly(element.elementType,
                            $"'{element.elementId}' is a {element.elementType} control; generated UXML contains only its VisualElement placeholder.", element.elementId);
                    else if (support == DesignerBackendSupport.Unsupported)
                        report.MarkUnsupported(element.elementType,
                            $"'{element.elementId}' is unsupported by UI Toolkit.", element.elementId);
                    if (element.attachedComponents != null && element.attachedComponents.Count > 0)
                        report.MarkPreviewOnly("Attached components",
                            $"'{element.elementId}' has MonoBehaviour attachments; these are applied only to uGUI GameObjects.", element.elementId);
                    if (!names.Contains(element.elementId))
                        report.MarkPreviewOnly("Unmatched element", $"Metadata element '{element.elementId}' has no matching named VisualElement in UXML. " +
                                    "Add name=\"" + element.elementId + "\" in UI Builder, or use 'Sync Metadata From UXML'.", element.elementId);
                }
            }

            SaveDirtyAssets(metadata, definition);
            return report;
        }

        private static void SaveDirtyAssets(DesignerMetadataAsset metadata, UIScreenDefinition definition)
        {
            if (metadata != null) AssetDatabase.SaveAssetIfDirty(metadata);
            if (definition != null) AssetDatabase.SaveAssetIfDirty(definition);
        }

        // CloneTree()+walk is comparatively expensive and CollectElementNames is called on
        // every Save AND on every validation pass (validation runs on each canvas edit). Cache
        // the collected names per VisualTreeAsset, invalidated when the asset's dirty count
        // changes (in-editor edits) or when any UXML is reimported (UI Builder / external edits;
        // see UIToolkitUxmlCacheWatcher below). The cached set is read-only for callers.
        private static readonly Dictionary<VisualTreeAsset, CachedNames> NameCache = new Dictionary<VisualTreeAsset, CachedNames>();

        private struct CachedNames
        {
            public int DirtyCount;
            public HashSet<string> Names;
        }

        /// <summary>All non-empty VisualElement names found in the cloned UXML tree.</summary>
        public static HashSet<string> CollectElementNames(VisualTreeAsset vta)
        {
            if (vta == null) return new HashSet<string>();

            var dirty = EditorUtility.GetDirtyCount(vta);
            if (NameCache.TryGetValue(vta, out var cached) && cached.DirtyCount == dirty)
                return cached.Names;

            var names = ComputeElementNames(vta);
            NameCache[vta] = new CachedNames { DirtyCount = dirty, Names = names };
            return names;
        }

        /// <summary>Drops all cached UXML name sets. Called when UXML assets are reimported.</summary>
        internal static void InvalidateNameCache() => NameCache.Clear();

        private static HashSet<string> ComputeElementNames(VisualTreeAsset vta)
        {
            var names = new HashSet<string>();
            VisualElement root;
            try { root = vta.CloneTree(); }
            catch { return names; }
            Walk(root, ve =>
            {
                if (!string.IsNullOrEmpty(ve.name)) names.Add(ve.name);
            });
            return names;
        }

        /// <summary>
        /// Adds a metadata element for every named VisualElement in the UXML that has no
        /// metadata yet. Undo-aware. Returns the number of elements added.
        /// </summary>
        public static int SyncMetadataFromUxml(DesignerMetadataAsset metadata, VisualTreeAsset vta)
        {
            if (metadata == null || vta == null) return 0;
            var added = 0;
            Undo.RecordObject(metadata, "Sync Metadata From UXML");
            foreach (var name in CollectElementNames(vta))
            {
                if (metadata.Find(name) != null) continue;
                metadata.elements.Add(new DesignerElementMetadata
                {
                    elementId = name,
                    displayName = name,
                    elementType = "Custom"
                });
                added++;
            }
            if (added > 0) DesignerMetadataUtility.MarkDirty(metadata);
            return added;
        }

        private static void Walk(VisualElement element, System.Action<VisualElement> visit)
        {
            visit(element);
            foreach (var child in element.Children())
                Walk(child, visit);
        }
    }

    /// <summary>
    /// Clears the <see cref="UIToolkitAssetSerializer"/> name cache whenever a UXML asset is
    /// imported, deleted, or moved so name lookups pick up UI Builder / external edits that do
    /// not go through the in-editor dirty-count signal.
    /// </summary>
    internal sealed class UIToolkitUxmlCacheWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (TouchesUxml(importedAssets) || TouchesUxml(deletedAssets) ||
                TouchesUxml(movedAssets) || TouchesUxml(movedFromAssetPaths))
                UIToolkitAssetSerializer.InvalidateNameCache();
        }

        private static bool TouchesUxml(string[] paths)
        {
            if (paths == null) return false;
            for (int i = 0; i < paths.Length; i++)
                if (!string.IsNullOrEmpty(paths[i]) &&
                    paths[i].EndsWith(".uxml", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Components.Definitions
{
    /// <summary>
    /// Project-wide index of <see cref="DesignerComponentDefinitionAsset"/>s: discovery, lookup by
    /// GUID/componentId, search, categories, tags, favourites and "where is this used".
    ///
    /// The cache is rebuilt lazily and invalidated by asset post-processing, so creating or deleting
    /// a definition in the Project window is reflected without a manual refresh. Favourites are an
    /// editor preference, never written into the asset (so they don't pollute Git diffs).
    /// </summary>
    public static class DesignerComponentLibrary
    {
        private const string FavouritePrefKey = "NexUI.Designer.ComponentFavourites";
        public const string DefaultFolder = "Custom";

        private static readonly Dictionary<string, DesignerComponentDefinitionAsset> ByGuid = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> GuidByComponentId = new(StringComparer.Ordinal);
        private static readonly List<DesignerComponentDefinitionAsset> Ordered = new();
        private static HashSet<string> _favourites;
        private static bool _built;

        /// <summary>Raised after the index is invalidated so open windows can repaint.</summary>
        public static event Action Changed;

        public static IReadOnlyList<DesignerComponentDefinitionAsset> All
        {
            get { EnsureBuilt(); return Ordered; }
        }

        public static void Invalidate()
        {
            _built = false;
            ByGuid.Clear();
            GuidByComponentId.Clear();
            Ordered.Clear();
            Changed?.Invoke();
        }

        private static void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            ByGuid.Clear();
            GuidByComponentId.Clear();
            Ordered.Clear();

            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(DesignerComponentDefinitionAsset)))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<DesignerComponentDefinitionAsset>(path);
                if (asset == null) continue;
                ByGuid[guid] = asset;
                Ordered.Add(asset);
                if (!string.IsNullOrEmpty(asset.componentId) && !GuidByComponentId.ContainsKey(asset.componentId))
                    GuidByComponentId[asset.componentId] = guid;
            }
            Ordered.Sort((a, b) => string.Compare(a.EffectiveDisplayName, b.EffectiveDisplayName, StringComparison.OrdinalIgnoreCase));
        }

        public static string GuidOf(DesignerComponentDefinitionAsset definition)
        {
            if (definition == null) return null;
            var builtInGuid = DesignerBuiltInComponentCatalog.SyntheticGuid(definition);
            if (!string.IsNullOrEmpty(builtInGuid)) return builtInGuid;
            var path = AssetDatabase.GetAssetPath(definition);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
        }

        /// <summary>
        /// Resolves by GUID first; falls back to <c>componentId</c> so a definition that was moved
        /// between projects (new GUID, same identity) still resolves instead of silently vanishing.
        /// </summary>
        public static DesignerComponentDefinitionAsset Resolve(string definitionGuid, string definitionId)
        {
            var builtIn = DesignerBuiltInComponentCatalog.Resolve(definitionGuid, definitionId);
            if (builtIn != null) return builtIn;
            EnsureBuilt();
            if (!string.IsNullOrEmpty(definitionGuid) && ByGuid.TryGetValue(definitionGuid, out var byGuid))
                return byGuid;
            if (!string.IsNullOrEmpty(definitionId) && GuidByComponentId.TryGetValue(definitionId, out var guid) &&
                ByGuid.TryGetValue(guid, out var byId))
                return byId;
            return null;
        }

        /// <summary>Shared resolver instance for the expander.</summary>
        public static IDesignerComponentDefinitionResolver Resolver { get; } = new AssetDatabaseResolver();

        private sealed class AssetDatabaseResolver : IDesignerComponentDefinitionResolver
        {
            public DesignerComponentDefinitionAsset Resolve(string definitionGuid, string definitionId)
                => DesignerComponentLibrary.Resolve(definitionGuid, definitionId);
        }

        // ---- Browsing ---------------------------------------------------------------------

        public static IEnumerable<string> Categories()
        {
            EnsureBuilt();
            var seen = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { DefaultFolder };
            foreach (var folder in DesignerComponentFolderSettings.All)
                seen.Add(folder);
            foreach (var d in Ordered)
                seen.Add(EffectiveFolder(d));
            return seen;
        }

        public static string EffectiveFolder(DesignerComponentDefinitionAsset definition)
            => NormalizeFolder(definition?.category);

        /// <summary>Normalizes a user folder path without ever touching the file system.</summary>
        public static string NormalizeFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return DefaultFolder;
            var parts = path.Replace('\\', '/').Split('/');
            var clean = new List<string>(parts.Length);
            foreach (var part in parts)
            {
                var value = part.Trim();
                if (string.IsNullOrEmpty(value) || value == "." || value == "..") continue;
                foreach (var invalid in System.IO.Path.GetInvalidFileNameChars()) value = value.Replace(invalid.ToString(), string.Empty);
                if (!string.IsNullOrWhiteSpace(value)) clean.Add(value.Trim());
            }
            return clean.Count == 0 ? DefaultFolder : string.Join("/", clean);
        }

        public static bool CreateFolder(string path)
        {
            var changed = DesignerComponentFolderSettings.Add(path);
            if (changed) Changed?.Invoke();
            return changed;
        }

        public static void SetFolder(DesignerComponentDefinitionAsset definition, string folder)
        {
            if (definition == null) return;
            folder = NormalizeFolder(folder);
            DesignerComponentFolderSettings.Add(folder);
            if (string.Equals(EffectiveFolder(definition), folder, StringComparison.OrdinalIgnoreCase)) return;
            Undo.RecordObject(definition, "Move NexUI Component Folder");
            definition.category = folder;
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            Invalidate();
        }

        /// <summary>Renames a folder and every nested path, preserving component membership.</summary>
        public static int RenameFolder(string oldPath, string newPath)
        {
            oldPath = NormalizeFolder(oldPath);
            newPath = NormalizeFolder(newPath);
            if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)) return 0;
            EnsureBuilt();
            var changed = new List<DesignerComponentDefinitionAsset>();
            foreach (var definition in Ordered)
                if (IsInFolderTree(EffectiveFolder(definition), oldPath)) changed.Add(definition);

            if (changed.Count > 0) Undo.RecordObjects(changed.ToArray(), "Rename NexUI Component Folder");
            foreach (var definition in changed)
            {
                var current = EffectiveFolder(definition);
                definition.category = newPath + current.Substring(oldPath.Length);
                EditorUtility.SetDirty(definition);
            }
            DesignerComponentFolderSettings.RenameTree(oldPath, newPath);
            DesignerComponentFolderSettings.Add(newPath);
            if (changed.Count > 0) AssetDatabase.SaveAssets();
            Invalidate();
            return changed.Count;
        }

        /// <summary>
        /// Removes a folder node without deleting component assets. Components in the removed tree
        /// move to its parent (or Custom), which makes the operation reversible through Undo.
        /// </summary>
        public static int RemoveFolder(string path)
        {
            path = NormalizeFolder(path);
            if (string.Equals(path, DefaultFolder, StringComparison.OrdinalIgnoreCase)) return 0;
            var slash = path.LastIndexOf('/');
            var destination = slash > 0 ? path.Substring(0, slash) : DefaultFolder;
            EnsureBuilt();
            var changed = new List<DesignerComponentDefinitionAsset>();
            foreach (var definition in Ordered)
                if (IsInFolderTree(EffectiveFolder(definition), path)) changed.Add(definition);

            if (changed.Count > 0) Undo.RecordObjects(changed.ToArray(), "Remove NexUI Component Folder");
            foreach (var definition in changed)
            {
                definition.category = destination;
                EditorUtility.SetDirty(definition);
            }
            DesignerComponentFolderSettings.RemoveTree(path);
            if (changed.Count > 0) AssetDatabase.SaveAssets();
            Invalidate();
            return changed.Count;
        }

        public static void RenameComponent(DesignerComponentDefinitionAsset definition, string displayName)
        {
            if (definition == null || string.IsNullOrWhiteSpace(displayName)) return;
            displayName = displayName.Trim();
            if (definition.EffectiveDisplayName == displayName) return;
            Undo.RecordObject(definition, "Rename NexUI Component");
            definition.displayName = displayName;
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            Invalidate();
        }

        private static bool IsInFolderTree(string candidate, string root)
            => string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase) ||
               candidate.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);

        public static IEnumerable<string> Tags()
        {
            EnsureBuilt();
            var seen = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in Ordered)
                if (d.tags != null)
                    foreach (var tag in d.tags)
                        if (!string.IsNullOrEmpty(tag)) seen.Add(tag);
            return seen;
        }

        /// <summary>
        /// Filters the library. <paramref name="query"/> matches display name, category, description
        /// and tags case-insensitively; empty filters mean "no restriction".
        /// </summary>
        public static List<DesignerComponentDefinitionAsset> Search(string query, string category = null, bool favouritesOnly = false)
        {
            EnsureBuilt();
            var result = new List<DesignerComponentDefinitionAsset>();
            foreach (var d in Ordered)
            {
                if (!string.IsNullOrEmpty(category) &&
                    !string.Equals(EffectiveFolder(d), NormalizeFolder(category), StringComparison.OrdinalIgnoreCase))
                    continue;
                if (favouritesOnly && !IsFavourite(d)) continue;
                if (!Matches(d, query)) continue;
                result.Add(d);
            }
            return result;
        }

        private static bool Matches(DesignerComponentDefinitionAsset d, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            query = query.Trim();
            if (Contains(d.EffectiveDisplayName, query) || Contains(d.category, query) || Contains(d.description, query))
                return true;
            if (d.tags != null)
                foreach (var tag in d.tags)
                    if (Contains(tag, query)) return true;
            return false;
        }

        private static bool Contains(string haystack, string needle)
            => !string.IsNullOrEmpty(haystack) && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        // ---- Favourites -------------------------------------------------------------------

        private static HashSet<string> Favourites
        {
            get
            {
                if (_favourites != null) return _favourites;
                _favourites = new HashSet<string>(StringComparer.Ordinal);
                var raw = EditorPrefs.GetString(FavouritePrefKey, string.Empty);
                foreach (var id in raw.Split('|'))
                    if (!string.IsNullOrEmpty(id)) _favourites.Add(id);
                return _favourites;
            }
        }

        public static bool IsFavourite(DesignerComponentDefinitionAsset definition)
            => definition != null && !string.IsNullOrEmpty(definition.componentId) && Favourites.Contains(definition.componentId);

        public static void SetFavourite(DesignerComponentDefinitionAsset definition, bool favourite)
        {
            if (definition == null || string.IsNullOrEmpty(definition.componentId)) return;
            if (favourite) Favourites.Add(definition.componentId);
            else Favourites.Remove(definition.componentId);
            EditorPrefs.SetString(FavouritePrefKey, string.Join("|", Favourites));
            Changed?.Invoke();
        }

        // ---- Usage ------------------------------------------------------------------------

        public sealed class DesignerComponentUsage
        {
            public DesignerMetadataAsset Screen;
            public string ElementId;
            public bool Detached;
        }

        /// <summary>
        /// Every instance of <paramref name="definition"/> across all Designer metadata assets in the
        /// project. Used by the library window, and by delete/rename flows so the user is never asked
        /// to confirm a destructive change without knowing what it breaks.
        /// </summary>
        public static List<DesignerComponentUsage> FindUsages(DesignerComponentDefinitionAsset definition)
        {
            var usages = new List<DesignerComponentUsage>();
            if (definition == null) return usages;
            var guid = GuidOf(definition);

            foreach (var assetGuid in AssetDatabase.FindAssets("t:" + nameof(DesignerMetadataAsset)))
            {
                var path = AssetDatabase.GUIDToAssetPath(assetGuid);
                var screen = AssetDatabase.LoadAssetAtPath<DesignerMetadataAsset>(path);
                if (screen == null) continue;
                foreach (var element in screen.elements)
                {
                    var reference = element?.componentInstance;
                    if (reference == null || !reference.HasReference) continue;
                    var matches = (!string.IsNullOrEmpty(guid) && reference.definitionGuid == guid) ||
                                  (!string.IsNullOrEmpty(definition.componentId) && reference.definitionId == definition.componentId);
                    if (!matches) continue;
                    usages.Add(new DesignerComponentUsage
                    {
                        Screen = screen,
                        ElementId = element.elementId,
                        Detached = reference.detached
                    });
                }
            }
            return usages;
        }

        private sealed class Watcher : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                if (NeedsRebuild(imported) || NeedsRebuild(deleted) || NeedsRebuild(moved))
                    Invalidate();
            }

            private static bool NeedsRebuild(string[] paths)
            {
                foreach (var path in paths)
                    if (path != null && path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                        return true;
                return false;
            }
        }
    }

    /// <summary>Project-shared empty-folder index for the custom component library.</summary>
    [FilePath("ProjectSettings/NexUIDesignerComponentFolders.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class DesignerComponentFolderSettings : ScriptableSingleton<DesignerComponentFolderSettings>
    {
        [SerializeField] private List<string> folders = new List<string> { DesignerComponentLibrary.DefaultFolder };

        public static IReadOnlyList<string> All
        {
            get
            {
                instance.NormalizeStoredFolders();
                return instance.folders;
            }
        }

        public static bool Add(string path)
        {
            path = DesignerComponentLibrary.NormalizeFolder(path);
            if (string.IsNullOrEmpty(path)) return false;
            instance.NormalizeStoredFolders();
            if (instance.folders.Exists(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase))) return false;
            AddParents(path, instance.folders);
            instance.SaveSettings();
            return true;
        }

        public static bool RenameTree(string oldPath, string newPath)
        {
            oldPath = DesignerComponentLibrary.NormalizeFolder(oldPath);
            newPath = DesignerComponentLibrary.NormalizeFolder(newPath);
            if (string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath) ||
                string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)) return false;

            instance.NormalizeStoredFolders();
            var changed = false;
            for (var i = 0; i < instance.folders.Count; i++)
            {
                var folder = instance.folders[i];
                if (!IsInTree(folder, oldPath)) continue;
                instance.folders[i] = newPath + folder.Substring(oldPath.Length);
                changed = true;
            }
            AddParents(newPath, instance.folders);
            if (changed) instance.SaveSettings();
            return changed;
        }

        public static bool RemoveTree(string path)
        {
            path = DesignerComponentLibrary.NormalizeFolder(path);
            if (string.IsNullOrEmpty(path) || string.Equals(path, DesignerComponentLibrary.DefaultFolder, StringComparison.OrdinalIgnoreCase))
                return false;
            instance.NormalizeStoredFolders();
            var removed = instance.folders.RemoveAll(x => IsInTree(x, path));
            if (removed > 0) instance.SaveSettings();
            return removed > 0;
        }

        private static bool IsInTree(string candidate, string root)
            => string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase) ||
               candidate.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);

        private static void AddParents(string path, List<string> target)
        {
            var segments = path.Split('/');
            var current = string.Empty;
            foreach (var segment in segments)
            {
                current = string.IsNullOrEmpty(current) ? segment : current + "/" + segment;
                if (!target.Exists(x => string.Equals(x, current, StringComparison.OrdinalIgnoreCase)))
                    target.Add(current);
            }
        }

        private void NormalizeStoredFolders()
        {
            folders ??= new List<string>();
            var normalized = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { DesignerComponentLibrary.DefaultFolder };
            foreach (var folder in folders)
            {
                var path = DesignerComponentLibrary.NormalizeFolder(folder);
                if (!string.IsNullOrEmpty(path)) normalized.Add(path);
            }
            folders.Clear();
            folders.AddRange(normalized);
        }

        private void SaveSettings()
        {
            NormalizeStoredFolders();
            Save(true);
        }
    }
}

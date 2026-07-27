using System;
using System.Collections.Generic;
using UnityEditor;

namespace emiteat.NexUI.Designer.Editor.UI.Panels
{
    /// <summary>
    /// Coarse asset categories the Designer's asset browser filters by. Deliberately UI-centric
    /// (what a UI author reaches for) rather than a mirror of Unity's importer types.
    /// </summary>
    public enum DesignerAssetKind
    {
        Other,
        Folder,
        Image,
        Font,
        Material,
        Prefab,
        Uxml,
        Uss,
        ScriptableObject,
        Scene,
        Animation
    }

    /// <summary>One row in the browser. Folders and assets share the shape so listing stays simple.</summary>
    public sealed class DesignerAssetEntry
    {
        public string Path;
        public string Name;
        public bool IsFolder;
        public DesignerAssetKind Kind;

        public override string ToString() => (IsFolder ? "[dir] " : "") + Path;
    }

    /// <summary>
    /// Path/kind/filter logic for the Designer's asset browser, kept free of UI and (where possible)
    /// of <see cref="AssetDatabase"/> so the rules are unit-testable. Only <see cref="List"/> and
    /// <see cref="Search"/> touch the project.
    /// </summary>
    public static class DesignerAssetBrowser
    {
        public const string RootFolder = "Assets";

        /// <summary>Kinds offered in the filter dropdown, in display order.</summary>
        public static readonly DesignerAssetKind[] FilterKinds =
        {
            DesignerAssetKind.Other,           // reused as "All" by the panel via FilterLabel
            DesignerAssetKind.Image,
            DesignerAssetKind.Font,
            DesignerAssetKind.Material,
            DesignerAssetKind.Prefab,
            DesignerAssetKind.Uxml,
            DesignerAssetKind.Uss,
            DesignerAssetKind.ScriptableObject
        };

        /// <summary>Classifies by file extension. Pure - no project access, so it is fully testable.</summary>
        public static DesignerAssetKind KindOf(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return DesignerAssetKind.Other;
            var dot = assetPath.LastIndexOf('.');
            if (dot < 0) return DesignerAssetKind.Other;
            var extension = assetPath.Substring(dot).ToLowerInvariant();

            switch (extension)
            {
                case ".png": case ".jpg": case ".jpeg": case ".tga": case ".psd":
                case ".gif": case ".bmp": case ".tif": case ".tiff": case ".exr": case ".svg":
                    return DesignerAssetKind.Image;
                case ".ttf": case ".otf": case ".fontsettings":
                    return DesignerAssetKind.Font;
                case ".mat": return DesignerAssetKind.Material;
                case ".prefab": return DesignerAssetKind.Prefab;
                case ".uxml": return DesignerAssetKind.Uxml;
                case ".uss": case ".tss": return DesignerAssetKind.Uss;
                case ".asset": return DesignerAssetKind.ScriptableObject;
                case ".unity": return DesignerAssetKind.Scene;
                case ".anim": case ".controller": return DesignerAssetKind.Animation;
                default: return DesignerAssetKind.Other;
            }
        }

        /// <summary>Short glyph shown when no texture preview is available. Pure.</summary>
        public static string GlyphFor(DesignerAssetKind kind)
        {
            switch (kind)
            {
                case DesignerAssetKind.Folder: return "▸";
                case DesignerAssetKind.Image: return "▣";
                case DesignerAssetKind.Font: return "T";
                case DesignerAssetKind.Material: return "◉";
                case DesignerAssetKind.Prefab: return "❐";
                case DesignerAssetKind.Uxml: return "⟨⟩";
                case DesignerAssetKind.Uss: return "≡";
                case DesignerAssetKind.ScriptableObject: return "◈";
                case DesignerAssetKind.Scene: return "⬚";
                case DesignerAssetKind.Animation: return "⏱";
                default: return "·";
            }
        }

        /// <summary>Human label for the filter dropdown. <see cref="DesignerAssetKind.Other"/> is the "All" slot.</summary>
        public static string FilterLabel(DesignerAssetKind kind)
        {
            switch (kind)
            {
                case DesignerAssetKind.Other: return "All";
                case DesignerAssetKind.Uxml: return "UXML";
                case DesignerAssetKind.Uss: return "USS";
                case DesignerAssetKind.ScriptableObject: return "Asset";
                default: return kind.ToString();
            }
        }

        /// <summary>
        /// Whether an entry survives the current search text and kind filter. Folders always pass the
        /// kind filter (you must be able to navigate into a folder to reach the assets it holds) but
        /// still honour the search text. Pure.
        /// </summary>
        public static bool Matches(DesignerAssetEntry entry, string search, DesignerAssetKind filter)
        {
            if (entry == null) return false;

            if (!string.IsNullOrWhiteSpace(search) &&
                (entry.Name == null || entry.Name.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) < 0))
                return false;

            if (filter == DesignerAssetKind.Other) return true;   // "All"
            if (entry.IsFolder) return true;
            return entry.Kind == filter;
        }

        /// <summary>Parent of a project folder path, clamped at <see cref="RootFolder"/>. Pure.</summary>
        public static string ParentFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return RootFolder;
            folderPath = folderPath.Replace('\\', '/').TrimEnd('/');
            var slash = folderPath.LastIndexOf('/');
            if (slash <= 0) return RootFolder;
            var parent = folderPath.Substring(0, slash);
            return string.IsNullOrEmpty(parent) ? RootFolder : parent;
        }

        /// <summary>
        /// Cumulative path segments for a breadcrumb bar, e.g.
        /// <c>Assets/UI/Icons</c> → <c>[Assets, Assets/UI, Assets/UI/Icons]</c>. Pure.
        /// </summary>
        public static List<string> Breadcrumbs(string folderPath)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(folderPath)) return result;

            var parts = folderPath.Replace('\\', '/').TrimEnd('/').Split('/');
            var accumulated = string.Empty;
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                accumulated = accumulated.Length == 0 ? part : accumulated + "/" + part;
                result.Add(accumulated);
            }
            return result;
        }

        /// <summary>Last path segment - the display name of a folder. Pure.</summary>
        public static string LeafName(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            path = path.Replace('\\', '/').TrimEnd('/');
            var slash = path.LastIndexOf('/');
            return slash < 0 ? path : path.Substring(slash + 1);
        }

        /// <summary>
        /// Direct children of <paramref name="folderPath"/>: sub-folders first (alphabetical), then
        /// assets (alphabetical). <c>.meta</c> files are never listed.
        /// </summary>
        public static List<DesignerAssetEntry> List(string folderPath)
        {
            var entries = new List<DesignerAssetEntry>();
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                folderPath = RootFolder;

            foreach (var sub in AssetDatabase.GetSubFolders(folderPath))
                entries.Add(new DesignerAssetEntry
                {
                    Path = sub,
                    Name = LeafName(sub),
                    IsFolder = true,
                    Kind = DesignerAssetKind.Folder
                });
            entries.Sort(CompareByName);

            var assets = new List<DesignerAssetEntry>();
            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { folderPath }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                // FindAssets is recursive; keep only direct children, and skip folders (already listed).
                if (ParentFolder(path) != folderPath) continue;
                if (AssetDatabase.IsValidFolder(path)) continue;
                assets.Add(new DesignerAssetEntry
                {
                    Path = path,
                    Name = LeafName(path),
                    IsFolder = false,
                    Kind = KindOf(path)
                });
            }
            assets.Sort(CompareByName);

            entries.AddRange(assets);
            return entries;
        }

        /// <summary>Recursive search under <paramref name="folderPath"/>, capped so a project-wide query stays responsive.</summary>
        public static List<DesignerAssetEntry> Search(string folderPath, string query, int limit = 300)
        {
            var entries = new List<DesignerAssetEntry>();
            if (string.IsNullOrWhiteSpace(query)) return entries;
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                folderPath = RootFolder;

            foreach (var guid in AssetDatabase.FindAssets(query.Trim(), new[] { folderPath }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) continue;
                entries.Add(new DesignerAssetEntry
                {
                    Path = path,
                    Name = LeafName(path),
                    IsFolder = false,
                    Kind = KindOf(path)
                });
                if (entries.Count >= limit) break;
            }
            entries.Sort(CompareByName);
            return entries;
        }

        private static int CompareByName(DesignerAssetEntry a, DesignerAssetEntry b)
            => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
    }
}

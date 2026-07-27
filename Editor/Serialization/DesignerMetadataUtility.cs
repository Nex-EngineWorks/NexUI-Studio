using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Serialization
{
    /// <summary>
    /// Safe, allocation-light helpers for finding, creating, renaming, duplicating,
    /// deleting and validating <see cref="DesignerElementMetadata"/> entries. All mutators
    /// are Undo-aware and only mark the asset dirty when something actually changed.
    /// </summary>
    public static class DesignerMetadataUtility
    {
        private static readonly Regex ValidIdPattern = new Regex("^[A-Za-z_][A-Za-z0-9_-]*$");

        public static void MarkDirty(UnityEngine.Object asset)
        {
            if (asset != null)
                EditorUtility.SetDirty(asset);
        }

        public static bool IsValidElementId(string id)
            => !string.IsNullOrEmpty(id) && ValidIdPattern.IsMatch(id);

        public static DesignerElementMetadata Find(DesignerMetadataAsset asset, string elementId)
        {
            if (asset == null || string.IsNullOrEmpty(elementId)) return null;
            return asset.Find(elementId);
        }

        public static bool ContainsId(DesignerMetadataAsset asset, string elementId)
            => Find(asset, elementId) != null;

        /// <summary>Returns <paramref name="baseId"/> or a numbered variant that is unused.</summary>
        public static string MakeUniqueId(DesignerMetadataAsset asset, string baseId)
        {
            var id = string.IsNullOrEmpty(baseId) ? "element" : baseId;
            if (!ContainsId(asset, id)) return id;
            var index = 1;
            string candidate;
            do { candidate = id + index++; } while (ContainsId(asset, candidate));
            return candidate;
        }

        public static DesignerElementMetadata Create(DesignerMetadataAsset asset, DesignerElementMetadata element, string undoName = "Create NexUI Element")
        {
            if (asset == null || element == null) return null;
            Undo.RecordObject(asset, undoName);
            element.elementId = MakeUniqueId(asset, element.elementId);
            asset.elements.Add(element);
            MarkDirty(asset);
            return element;
        }

        public static bool Delete(DesignerMetadataAsset asset, DesignerElementMetadata element, string undoName = "Delete NexUI Element")
        {
            if (asset == null || element == null) return false;
            Undo.RecordObject(asset, undoName);
            var removed = asset.elements.Remove(element);
            if (removed)
            {
                // Detach children so we never leave dangling parentIds.
                foreach (var child in asset.elements)
                    if (child != null && child.parentId == element.elementId)
                        child.parentId = null;
                MarkDirty(asset);
            }
            return removed;
        }

        public static bool Rename(DesignerMetadataAsset asset, DesignerElementMetadata element, string newId, string undoName = "Rename NexUI Element")
        {
            if (asset == null || element == null) return false;
            if (string.IsNullOrEmpty(newId) || newId == element.elementId) return false;
            if (ContainsId(asset, newId)) return false;
            Undo.RecordObject(asset, undoName);
            var oldId = element.elementId;
            element.elementId = newId;
            // Repoint children that referenced the old id.
            foreach (var child in asset.elements)
                if (child != null && child.parentId == oldId)
                    child.parentId = newId;
            MarkDirty(asset);
            return true;
        }

        public static DesignerElementMetadata Duplicate(DesignerMetadataAsset asset, DesignerElementMetadata source, string undoName = "Duplicate NexUI Element")
        {
            if (asset == null || source == null) return null;
            Undo.RecordObject(asset, undoName);
            var copy = Clone(source);
            copy.elementId = MakeUniqueId(asset, source.elementId + "Copy");
            copy.stableId = Guid.NewGuid().ToString("N");
            asset.elements.Add(copy);
            MarkDirty(asset);
            return copy;
        }

        /// <summary>Deep copy via JsonUtility so the clone stays correct as fields evolve.</summary>
        public static DesignerElementMetadata Clone(DesignerElementMetadata source)
        {
            if (source == null) return null;
            var json = JsonUtility.ToJson(source);
            var clone = JsonUtility.FromJson<DesignerElementMetadata>(json);
            return clone;
        }

        /// <summary>Returns element ids that appear more than once.</summary>
        public static List<string> FindDuplicateIds(DesignerMetadataAsset asset)
        {
            var duplicates = new List<string>();
            if (asset == null) return duplicates;
            var seen = new HashSet<string>();
            foreach (var e in asset.elements)
            {
                if (e == null || string.IsNullOrEmpty(e.elementId)) continue;
                if (!seen.Add(e.elementId) && !duplicates.Contains(e.elementId))
                    duplicates.Add(e.elementId);
            }
            return duplicates;
        }
    }
}

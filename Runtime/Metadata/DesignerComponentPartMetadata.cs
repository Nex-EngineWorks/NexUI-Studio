using System;
using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Designer
{
    /// <summary>
    /// A sparse transform override for one named internal part of a component (track, handle,
    /// checkmark, viewport...). Values are deltas from the component library's default hierarchy,
    /// so updating that hierarchy does not rewrite every authored screen.
    /// </summary>
    [Serializable]
    public sealed class DesignerComponentPartOverrideMetadata
    {
        public string partId;
        public bool hasPosition;
        public Vector2 position;
        public bool hasSizeDelta;
        public Vector2 sizeDelta;
        public bool hasRotation;
        public float rotation;
        public bool hasScale;
        public Vector2 scale = Vector2.one;
        public bool hasVisibility;
        public bool visible = true;

        public bool HasAnyOverride => hasPosition || hasSizeDelta || hasRotation || hasScale || hasVisibility;

        public DesignerComponentPartOverrideMetadata Clone()
            => new DesignerComponentPartOverrideMetadata
            {
                partId = partId,
                hasPosition = hasPosition,
                position = position,
                hasSizeDelta = hasSizeDelta,
                sizeDelta = sizeDelta,
                hasRotation = hasRotation,
                rotation = rotation,
                hasScale = hasScale,
                scale = scale,
                hasVisibility = hasVisibility,
                visible = visible
            };
    }

    /// <summary>Runtime-safe lookup helpers shared by preview and backend serializers.</summary>
    public static class DesignerComponentPartOverrideBag
    {
        public static DesignerComponentPartOverrideMetadata Find(
            List<DesignerComponentPartOverrideMetadata> entries, string partId)
        {
            if (entries == null || string.IsNullOrEmpty(partId)) return null;
            for (var i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].partId == partId)
                    return entries[i];
            return null;
        }

        public static DesignerComponentPartOverrideMetadata GetOrCreate(
            List<DesignerComponentPartOverrideMetadata> entries, string partId)
        {
            if (entries == null || string.IsNullOrEmpty(partId)) return null;
            var value = Find(entries, partId);
            if (value != null) return value;
            value = new DesignerComponentPartOverrideMetadata { partId = partId };
            entries.Add(value);
            return value;
        }

        public static void RemoveEmpty(List<DesignerComponentPartOverrideMetadata> entries, string partId)
        {
            if (entries == null) return;
            for (var i = entries.Count - 1; i >= 0; i--)
                if (entries[i] != null && entries[i].partId == partId && !entries[i].HasAnyOverride)
                    entries.RemoveAt(i);
        }
    }
}

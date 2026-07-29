using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Designer
{
    /// <summary>
    /// One authored value of a component-specific property ("slider.wholeNumbers", "scroll.inertia").
    /// </summary>
    /// <remarks>
    /// Component properties are stored as a schema-keyed bag rather than as fields on
    /// <see cref="DesignerElementMetadata"/>. Each component type declares its own property schema in
    /// the editor, and only values the user actually changed are written here - so adding a property
    /// to a component never migrates existing screens, and a screen authored in a newer Designer
    /// still loads in an older one (unknown keys are preserved, not dropped).
    /// </remarks>
    [Serializable]
    public sealed class DesignerComponentPropertyEntry
    {
        public string key;
        public DesignerPropertyValue value = new DesignerPropertyValue();

        public DesignerComponentPropertyEntry() { }

        public DesignerComponentPropertyEntry(string key, DesignerPropertyValue value)
        {
            this.key = key;
            this.value = value ?? new DesignerPropertyValue();
        }

        public DesignerComponentPropertyEntry Clone()
            => new DesignerComponentPropertyEntry(key, value?.Clone());
    }

    /// <summary>Helpers shared by the editor and the serializers for reading a property bag.</summary>
    public static class DesignerComponentPropertyBag
    {
        public static DesignerPropertyValue Find(List<DesignerComponentPropertyEntry> entries, string key)
        {
            if (entries == null || string.IsNullOrEmpty(key)) return null;
            for (var i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].key == key)
                    return entries[i].value;
            return null;
        }

        public static bool Has(List<DesignerComponentPropertyEntry> entries, string key)
            => Find(entries, key) != null;

        /// <summary>Sets (or clears) a value in place. Returns true when the bag changed.</summary>
        public static bool Set(List<DesignerComponentPropertyEntry> entries, string key, DesignerPropertyValue value)
        {
            if (entries == null || string.IsNullOrEmpty(key)) return false;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] == null || entries[i].key != key) continue;
                if (value == null) { entries.RemoveAt(i); return true; }
                entries[i].value = value;
                return true;
            }
            if (value == null) return false;
            entries.Add(new DesignerComponentPropertyEntry(key, value));
            return true;
        }
    }
}

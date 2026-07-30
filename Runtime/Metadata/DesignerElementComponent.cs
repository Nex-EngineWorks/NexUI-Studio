using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Designer
{
    /// <summary>
    /// One component attached to a Designer element - the authoring counterpart of a component on a
    /// Unity GameObject.
    /// </summary>
    /// <remarks>
    /// An element is a container of components, exactly like a GameObject: what an element *is* comes
    /// from what is attached to it, not from a type baked into it. A palette entry such as "Slider" is
    /// therefore a preset that stamps a set of components, and everything it added can be inspected,
    /// reordered, disabled or removed afterwards.
    ///
    /// <see cref="typeId"/> is the registry id of the component type ("UGUI.Image", "UITK.Slider",
    /// "NX.RoundedRect"). Values live in the same schema-keyed bag used elsewhere, so only what the
    /// user changed is stored and unknown keys survive a round trip through an older build.
    /// </remarks>
    [Serializable]
    public sealed class DesignerElementComponent
    {
        /// <summary>Stable identity of this attachment, so Undo and reordering never confuse two components of the same type.</summary>
        public string instanceId = Guid.NewGuid().ToString("N");

        /// <summary>Component type id from the Designer's component registry.</summary>
        public string typeId;

        /// <summary>Mirrors Unity's per-component enable checkbox.</summary>
        public bool enabled = true;

        /// <summary>
        /// True when a preset added this component. Purely informational: the user can remove it like
        /// any other, and "decompose" simply clears the preset label from the element.
        /// </summary>
        public bool fromPreset;

        public List<DesignerComponentPropertyEntry> properties = new List<DesignerComponentPropertyEntry>();

        public DesignerElementComponent() { }

        public DesignerElementComponent(string typeId, bool fromPreset = false)
        {
            this.typeId = typeId;
            this.fromPreset = fromPreset;
        }

        public DesignerElementComponent Clone()
        {
            var clone = new DesignerElementComponent
            {
                instanceId = Guid.NewGuid().ToString("N"),
                typeId = typeId,
                enabled = enabled,
                fromPreset = fromPreset
            };
            if (properties != null)
                foreach (var entry in properties)
                    if (entry != null)
                        clone.properties.Add(entry.Clone());
            return clone;
        }
    }
}

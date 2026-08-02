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
    /// <summary>
    /// Which library a component came from. Decides how it is written to a backend and how it is
    /// grouped in Add Component.
    /// </summary>
    public enum DesignerComponentSource
    {
        /// <summary>NexUI's own components, backend-agnostic.</summary>
        NexUI,

        /// <summary>Unity's uGUI controls - Image, Button, ScrollRect.</summary>
        UGUI,

        /// <summary>UI Toolkit elements and manipulators.</summary>
        UIToolkit,

        /// <summary>Unity engine components that are not UI-specific - Animator, CanvasGroup.</summary>
        Unity,

        /// <summary>A MonoBehaviour from the user's own project.</summary>
        Project
    }

    /// <summary>
    /// Which key space a component's <see cref="DesignerElementComponent.properties"/> use.
    /// </summary>
    /// <remarks>
    /// Two writers exist for a reason. Registry components were authored against a curated schema
    /// whose keys read like <c>"preserveAspect"</c> and cover the handful of fields a designer
    /// touches; imported and project components are stored by Unity's own property path
    /// (<c>"m_PreserveAspect"</c>) and cover everything the type has.
    ///
    /// Making the distinction an explicit field rather than inferring it from the key shape is what
    /// keeps a screen authored in either style loading correctly. <see cref="SchemaKeys"/> is value 0
    /// so every asset written before this field existed keeps its current behaviour.
    /// </remarks>
    public enum DesignerComponentValueFormat
    {
        /// <summary>Curated schema keys, written by the registry-backed uGUI writer.</summary>
        SchemaKeys,

        /// <summary>Unity <c>SerializedProperty</c> paths, written by the universal writer.</summary>
        PropertyPath
    }

    [Serializable]
    public sealed class DesignerElementComponent
    {
        /// <summary>Stable identity of this attachment, so Undo and reordering never confuse two components of the same type.</summary>
        public string instanceId = Guid.NewGuid().ToString("N");

        /// <summary>Component type id from the Designer's component registry.</summary>
        public string typeId;

        /// <summary>
        /// Where the type comes from. Defaults to <see cref="DesignerComponentSource.NexUI"/> so
        /// components authored before the universal system keep behaving exactly as they did.
        /// </summary>
        public DesignerComponentSource source = DesignerComponentSource.NexUI;

        /// <summary>
        /// Assembly-qualified name of the real runtime type, for components that are not in the
        /// Designer's own registry.
        /// </summary>
        /// <remarks>
        /// Registry components are found by <see cref="typeId"/> and leave this empty. A project
        /// MonoBehaviour has no registry entry, so the only way back to its <c>System.Type</c> is the
        /// qualified name - and keeping it means a script that is temporarily missing (renamed,
        /// moved to another assembly, not yet compiled) is reported rather than silently dropped.
        /// </remarks>
        public string assemblyQualifiedTypeName;

        /// <summary>Mirrors Unity's per-component enable checkbox.</summary>
        public bool enabled = true;

        /// <summary>
        /// True when a preset added this component. Purely informational: the user can remove it like
        /// any other, and "decompose" simply clears the preset label from the element.
        /// </summary>
        public bool fromPreset;

        /// <summary>
        /// Which key space <see cref="properties"/> use, and therefore which writer owns this entry.
        /// </summary>
        public DesignerComponentValueFormat valueFormat = DesignerComponentValueFormat.SchemaKeys;

        /// <summary>
        /// True when this entry came from an existing prefab component. On the first save the writer
        /// binds the entry to that exact component instead of creating a duplicate. The association
        /// is non-destructive: removing the entry later never deletes the original prefab component.
        /// </summary>
        public bool adoptExistingComponent;

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
                source = source,
                assemblyQualifiedTypeName = assemblyQualifiedTypeName,
                enabled = enabled,
                fromPreset = fromPreset,
                valueFormat = valueFormat,
                adoptExistingComponent = adoptExistingComponent
            };
            if (properties != null)
                foreach (var entry in properties)
                    if (entry != null)
                        clone.properties.Add(entry.Clone());
            return clone;
        }
    }
}

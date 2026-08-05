using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Designer
{
    /// <summary>
    /// One property value an instance (or a variant rule) forces onto an element of the definition.
    ///
    /// Prefer <see cref="exposedPropertyName"/>: it resolves through the definition's exposed-property
    /// contract, so the definition author can move/rename internal elements without invalidating
    /// instances. <see cref="targetElementId"/> + <see cref="propertyId"/> is the raw escape hatch used
    /// by variant rules and by "override anything" authoring.
    /// </summary>
    [Serializable]
    public sealed class DesignerComponentPropertyOverride
    {
        public string exposedPropertyName;
        /// <summary>Definition-local element id. Ignored when <see cref="exposedPropertyName"/> resolves.</summary>
        public string targetElementId;

        /// <summary>
        /// Definition-local <see cref="DesignerElementMetadata.stableId"/> of the same target.
        /// </summary>
        /// <remarks>
        /// The identity that survives a rename. <see cref="targetElementId"/> is what a user reads and
        /// therefore what a definition author eventually changes, and until this field existed such a
        /// rename silently stranded every instance override pointing at the old name.
        ///
        /// Both are stored rather than one: the stable id resolves the target, and the element id stays
        /// as the human-readable record of what the override was authored against - which is what makes
        /// a report about an unresolvable override readable at all. Empty on overrides written before
        /// this field existed; <c>UpdateFromDefinition</c> backfills those.
        /// </remarks>
        public string targetStableId;

        public DesignerPropertyId propertyId;
        public DesignerPropertyValue value = new DesignerPropertyValue();

        public DesignerComponentPropertyOverride Clone() => new DesignerComponentPropertyOverride
        {
            exposedPropertyName = exposedPropertyName,
            targetElementId = targetElementId,
            targetStableId = targetStableId,
            propertyId = propertyId,
            value = value != null ? value.Clone() : new DesignerPropertyValue()
        };

        /// <summary>Stable key used to de-duplicate and to diff two override sets.</summary>
        public string Key => !string.IsNullOrEmpty(exposedPropertyName)
            ? "exposed:" + exposedPropertyName
            : (targetElementId ?? string.Empty) + "." + propertyId;
    }

    /// <summary>One instance-side choice on a definition's variant axis.</summary>
    [Serializable]
    public sealed class DesignerComponentVariantSelection
    {
        public string propertyName;
        public string value;
    }

    /// <summary>
    /// Present on every <see cref="DesignerElementMetadata"/>; only <i>meaningful</i> when
    /// <see cref="definitionGuid"/> is set. An element carrying a live reference is a component
    /// instance: its authored children are slot content, and the definition sub-tree is expanded
    /// underneath it at preview/serialize time rather than being copied into the screen.
    ///
    /// Detaching sets <see cref="detached"/> and materializes the expanded elements into the screen
    /// as ordinary authored elements - the reference is kept (not cleared) so the origin stays
    /// traceable and a later "re-attach" is possible without guessing.
    /// </summary>
    [Serializable]
    public sealed class DesignerComponentInstanceMetadata
    {
        /// <summary>AssetDatabase GUID of the <see cref="DesignerComponentDefinitionAsset"/>.</summary>
        public string definitionGuid;
        /// <summary>Definition <c>componentId</c>, used to recover the reference when the GUID changed.</summary>
        public string definitionId;
        /// <summary>The <c>version</c> of the definition this instance was last reconciled against.</summary>
        public int definitionVersion;
        public bool detached;

        public List<DesignerComponentPropertyOverride> overrides = new List<DesignerComponentPropertyOverride>();
        public List<DesignerComponentVariantSelection> variantSelections = new List<DesignerComponentVariantSelection>();

        /// <summary>True when this element should be expanded from a definition.</summary>
        public bool IsInstance => !detached && !string.IsNullOrEmpty(definitionGuid);

        /// <summary>True when a definition reference exists at all (including a detached one).</summary>
        public bool HasReference => !string.IsNullOrEmpty(definitionGuid) || !string.IsNullOrEmpty(definitionId);

        public string GetVariantSelection(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return null;
            for (int i = 0; i < variantSelections.Count; i++)
                if (variantSelections[i] != null && variantSelections[i].propertyName == propertyName)
                    return variantSelections[i].value;
            return null;
        }

        public void SetVariantSelection(string propertyName, string value)
        {
            if (string.IsNullOrEmpty(propertyName)) return;
            for (int i = 0; i < variantSelections.Count; i++)
            {
                if (variantSelections[i] != null && variantSelections[i].propertyName == propertyName)
                {
                    variantSelections[i].value = value;
                    return;
                }
            }
            variantSelections.Add(new DesignerComponentVariantSelection { propertyName = propertyName, value = value });
        }

        public DesignerComponentPropertyOverride FindOverride(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            for (int i = 0; i < overrides.Count; i++)
                if (overrides[i] != null && overrides[i].Key == key) return overrides[i];
            return null;
        }
    }
}

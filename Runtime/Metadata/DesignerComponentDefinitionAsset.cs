using System;
using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using UnityEngine;

namespace emiteat.NexUI.Designer
{
    /// <summary>How a variant property is authored in the Inspector.</summary>
    public enum DesignerComponentVariantPropertyType
    {
        Boolean,
        Enum,
        String
    }

    /// <summary>
    /// A property the definition author promotes to the instance Inspector. Instances override the
    /// property by <see cref="propertyName"/> instead of poking at a definition-local element id, so
    /// renaming an internal element does not break existing instances (only renaming the exposed
    /// name does, and that is a tracked migration).
    /// </summary>
    [Serializable]
    public sealed class DesignerComponentExposedProperty
    {
        public string propertyName;
        public string displayName;
        /// <summary>Definition-local <see cref="DesignerElementMetadata.elementId"/> this property writes to.</summary>
        public string targetElementId;

        /// <summary>
        /// Definition-local <see cref="DesignerElementMetadata.stableId"/> of the same target, so
        /// renaming the element inside the definition does not break the exposed property either.
        /// Empty on definitions authored before this field existed.
        /// </summary>
        public string targetStableId;

        public DesignerPropertyId propertyId;
        public DesignerPropertyValue defaultValue = new DesignerPropertyValue();
    }

    /// <summary>
    /// A named region of the definition that accepts authored children from the instance's screen.
    /// <see cref="hostElementId"/> names the definition-local element the children are parented to;
    /// when it is empty the definition root is used.
    /// </summary>
    [Serializable]
    public sealed class DesignerComponentSlotDefinition
    {
        public const string Content = "content";

        public string slotId = Content;
        public string displayName;
        public string hostElementId;
        public bool required;
        public int minimumChildren;
        /// <summary>0 or negative ⇒ unbounded. Stored this way so a default-constructed slot is unbounded.</summary>
        public int maximumChildren;
        public List<string> acceptedTypes = new List<string>();
        public bool allowReorder = true;

        public bool IsUnbounded => maximumChildren <= 0;

        public bool Accepts(string typeId)
            => acceptedTypes == null || acceptedTypes.Count == 0 ||
               (!string.IsNullOrEmpty(typeId) && acceptedTypes.Contains(typeId));
    }

    /// <summary>One authorable variant axis, e.g. <c>size = {small, medium, large}</c>.</summary>
    [Serializable]
    public sealed class DesignerComponentVariantProperty
    {
        public string propertyName;
        public string displayName;
        public DesignerComponentVariantPropertyType type = DesignerComponentVariantPropertyType.Enum;
        public List<string> options = new List<string>();
        public string defaultValue;

        /// <summary>The value used when an instance selects nothing (explicit default, else the first option).</summary>
        public string EffectiveDefault
        {
            get
            {
                if (!string.IsNullOrEmpty(defaultValue)) return defaultValue;
                if (type == DesignerComponentVariantPropertyType.Boolean) return "false";
                return options != null && options.Count > 0 ? options[0] : string.Empty;
            }
        }
    }

    /// <summary>
    /// "When <see cref="propertyName"/> equals <see cref="equalsValue"/>, apply these overrides and
    /// visibility changes." Rules are evaluated in declaration order before instance overrides, so an
    /// instance override always wins over a variant rule.
    ///
    /// A rule may additionally - or instead - be conditioned on the authoring resolution and input
    /// mode, which is how a component expresses "use the compact arrangement on a narrow screen"
    /// without the definition having to own screen-level responsive rules.
    /// </summary>
    [Serializable]
    public sealed class DesignerComponentVariantRule
    {
        /// <summary>Empty for a rule driven only by <see cref="constrainResolution"/>/<see cref="constrainInputMode"/>.</summary>
        public string propertyName;
        public string equalsValue;

        /// <summary>Applies only within <see cref="minResolution"/>..<see cref="maxResolution"/>.</summary>
        public bool constrainResolution;
        public Vector2Int minResolution = new Vector2Int(0, 0);
        public Vector2Int maxResolution = new Vector2Int(9999, 9999);

        /// <summary>Applies only under <see cref="inputMode"/>.</summary>
        public bool constrainInputMode;
        public UIInputMode inputMode;

        /// <summary>True when the rule depends on the authoring environment rather than only on a variant axis.</summary>
        public bool HasEnvironmentCondition => constrainResolution || constrainInputMode;
        public List<DesignerComponentPropertyOverride> overrides = new List<DesignerComponentPropertyOverride>();
        /// <summary>Definition-local element ids forced to <c>runtimeVisible = false</c> when the rule matches.</summary>
        public List<string> hiddenElementIds = new List<string>();
        /// <summary>Definition-local element ids forced to <c>runtimeVisible = true</c> when the rule matches.</summary>
        public List<string> shownElementIds = new List<string>();
    }

    /// <summary>
    /// A user-authored, reusable UI component: a named element sub-tree plus the contract
    /// (exposed properties, slots, variants) instances edit it through.
    ///
    /// The definition owns its element list exactly like a <see cref="DesignerMetadataAsset"/> owns a
    /// screen's, which means every existing pure hierarchy/serialization helper works on it unchanged.
    /// Instances never copy these elements - they are expanded on demand by
    /// <c>DesignerComponentExpander</c>, so editing the definition propagates everywhere.
    /// </summary>
    [CreateAssetMenu(menuName = "NexUI/Designer/Component Definition", fileName = "NexUIComponentDefinition")]
    public sealed class DesignerComponentDefinitionAsset : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;

        /// <summary>
        /// Stable identity independent of the asset path/GUID. Instances store both this and the
        /// asset GUID so a definition moved between projects can still be recovered by id.
        /// </summary>
        public string componentId = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Bumped by the author whenever the contract changes in a way instances must react to.
        /// Instances record the version they were created against; a mismatch is reported (never
        /// silently "fixed") by <c>DesignerComponentValidation</c>.
        /// </summary>
        public int version = 1;

        public string displayName;
        public string category = "Custom";
        public string description;
        public List<string> tags = new List<string>();
        public Texture2D thumbnail;
        public Vector2 defaultSize = new Vector2(240f, 96f);

        /// <summary>Definition-local id of the sub-tree root. Empty ⇒ the first root in <see cref="elements"/>.</summary>
        public string rootElementId;
        public List<DesignerElementMetadata> elements = new List<DesignerElementMetadata>();

        public List<DesignerComponentExposedProperty> exposedProperties = new List<DesignerComponentExposedProperty>();
        public List<DesignerComponentSlotDefinition> slots = new List<DesignerComponentSlotDefinition>();
        public List<DesignerComponentVariantProperty> variantProperties = new List<DesignerComponentVariantProperty>();
        public List<DesignerComponentVariantRule> variantRules = new List<DesignerComponentVariantRule>();

        public string EffectiveDisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        public DesignerElementMetadata Find(string elementId)
        {
            if (string.IsNullOrEmpty(elementId)) return null;
            for (int i = 0; i < elements.Count; i++)
                if (elements[i] != null && elements[i].elementId == elementId)
                    return elements[i];
            return null;
        }

        /// <summary>The element carrying <paramref name="stableId"/>, or null.</summary>
        public DesignerElementMetadata FindByStableId(string stableId)
        {
            if (string.IsNullOrEmpty(stableId)) return null;
            for (int i = 0; i < elements.Count; i++)
                if (elements[i] != null && elements[i].stableId == stableId)
                    return elements[i];
            return null;
        }

        /// <summary>
        /// The element an override or exposed property points at, preferring the stable id.
        /// </summary>
        /// <remarks>
        /// The element id is only the fallback, and only for data written before stable ids were
        /// recorded. Preferring it would reintroduce the exact failure this pair exists to prevent: a
        /// renamed element whose id was reused by a different element would silently retarget the
        /// override at the wrong thing.
        /// </remarks>
        public DesignerElementMetadata ResolveTarget(string stableId, string elementId)
            => FindByStableId(stableId) ?? Find(elementId);

        /// <summary>
        /// The sub-tree root: the explicitly named <see cref="rootElementId"/> when it resolves,
        /// otherwise the first parentless element. Null for an empty definition.
        /// </summary>
        public DesignerElementMetadata Root
        {
            get
            {
                var named = Find(rootElementId);
                if (named != null) return named;
                for (int i = 0; i < elements.Count; i++)
                {
                    var e = elements[i];
                    if (e != null && string.IsNullOrEmpty(e.parentId)) return e;
                }
                return null;
            }
        }

        public DesignerComponentSlotDefinition FindSlot(string slotId)
        {
            if (string.IsNullOrEmpty(slotId)) slotId = DesignerComponentSlotDefinition.Content;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i] != null && slots[i].slotId == slotId) return slots[i];
            return null;
        }

        public DesignerComponentExposedProperty FindExposed(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return null;
            for (int i = 0; i < exposedProperties.Count; i++)
                if (exposedProperties[i] != null && exposedProperties[i].propertyName == propertyName)
                    return exposedProperties[i];
            return null;
        }

        public DesignerComponentVariantProperty FindVariantProperty(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return null;
            for (int i = 0; i < variantProperties.Count; i++)
                if (variantProperties[i] != null && variantProperties[i].propertyName == propertyName)
                    return variantProperties[i];
            return null;
        }
    }
}

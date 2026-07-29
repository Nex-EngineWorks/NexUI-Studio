using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Designer
{
    [CreateAssetMenu(menuName = "NexUI/Designer/Metadata", fileName = "NexUIDesignerMetadata")]
    public sealed class DesignerMetadataAsset : ScriptableObject
    {
        /// <summary>
        /// Current metadata schema. 0 = pre-hierarchy assets (no explicit siblingIndex; absolute
        /// canvas rects). Bumped by <see cref="Editor.DesignerHierarchyMigration"/> once sibling
        /// indices have been assigned so migration never runs twice on the same asset.
        /// </summary>
        public const int CurrentSchemaVersion = 5;
        public int schemaVersion;

        public string screenId;
        public List<DesignerElementMetadata> elements = new List<DesignerElementMetadata>();
        public DesignerScreenMotionMetadata screenMotion = new DesignerScreenMotionMetadata();

        // ---- Advanced Extension Pack metadata --------------------------------
        public List<DesignerVariantMetadata> variants = new List<DesignerVariantMetadata>();
        public List<DesignerResponsiveMetadata> responsiveRules = new List<DesignerResponsiveMetadata>();
        public DesignerContractMetadata contract = new DesignerContractMetadata();
        public DesignerSnapshotMetadata snapshots = new DesignerSnapshotMetadata();
        public DesignerLocalizationMetadata localization = new DesignerLocalizationMetadata();
        public DesignerPromptMetadata prompts = new DesignerPromptMetadata();
        public List<DesignerRecipeMetadata> recipes = new List<DesignerRecipeMetadata>();

        public DesignerElementMetadata Find(string elementId)
        {
            for (int i = 0; i < elements.Count; i++)
                if (elements[i] != null && elements[i].elementId == elementId)
                    return elements[i];
            return null;
        }
    }
}

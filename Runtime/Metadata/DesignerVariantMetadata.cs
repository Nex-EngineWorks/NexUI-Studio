using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Designer
{
    /// <summary>
    /// Designer-side authoring record for a screen variant. Serialized inside the
    /// DesignerMetadataAsset and compiled into Core's UIScreenVariant on export.
    /// </summary>
    [Serializable]
    public sealed class DesignerVariantMetadata
    {
        public string variantId;
        public string displayName;
        public bool isDefault;
        public List<DesignerVariantOverrideMetadata> overrides = new();
    }

    [Serializable]
    public sealed class DesignerVariantOverrideMetadata
    {
        public string targetElementId;
        public DesignerPropertyId propertyId;
        public DesignerPropertyValue typedValue = new DesignerPropertyValue();
        public string propertyPath;
        public string value;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Designer
{
    /// <summary>
    /// Designer-side authoring record for a responsive rule (resolution / input-mode
    /// driven layout adaptation). Compiled into Core's UIResponsiveRule on export.
    /// </summary>
    [Serializable]
    public sealed class DesignerResponsiveMetadata
    {
        public string ruleId;
        public Vector2Int minResolution = new Vector2Int(0, 0);
        public Vector2Int maxResolution = new Vector2Int(9999, 9999);
        public UIInputMode inputMode;
        public bool constrainInputMode;
        public List<DesignerResponsiveOverrideMetadata> overrides = new();
    }

    [Serializable]
    public sealed class DesignerResponsiveOverrideMetadata
    {
        public string elementId;
        public DesignerPropertyId propertyId;
        public DesignerPropertyValue typedValue = new DesignerPropertyValue();
        public string propertyPath;
        public string value;
    }
}

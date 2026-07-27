using System.Collections.Generic;
using UnityEngine;
using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor.Properties;

namespace emiteat.NexUI.Designer.Editor.Responsive
{
    /// <summary>
    /// Authoring logic for responsive rules on a <see cref="DesignerMetadataAsset"/>,
    /// compiling to Core's <see cref="UIResponsiveRule"/> and running overlap / range /
    /// unknown-element validation.
    /// </summary>
    public static class ResponsiveService
    {
        public static DesignerResponsiveMetadata Create(DesignerMetadataAsset asset, string ruleId)
        {
            var r = new DesignerResponsiveMetadata { ruleId = ruleId };
            asset.responsiveRules.Add(r);
            return r;
        }

        public static void Delete(DesignerMetadataAsset asset, DesignerResponsiveMetadata rule)
            => asset.responsiveRules.Remove(rule);

        public static DesignerResponsiveMetadata Duplicate(DesignerMetadataAsset asset, DesignerResponsiveMetadata src)
        {
            var copy = new DesignerResponsiveMetadata
            {
                ruleId = src.ruleId + "_Copy",
                minResolution = src.minResolution,
                maxResolution = src.maxResolution,
                inputMode = src.inputMode,
                constrainInputMode = src.constrainInputMode
            };
            foreach (var o in src.overrides)
                copy.overrides.Add(new DesignerResponsiveOverrideMetadata
                {
                    elementId = o.elementId, propertyId = o.propertyId, typedValue = o.typedValue?.Clone(),
                    propertyPath = o.propertyPath, value = o.value
                });
            asset.responsiveRules.Add(copy);
            return copy;
        }

        public static UIResponsiveRule[] Compile(DesignerMetadataAsset asset)
        {
            var result = new List<UIResponsiveRule>();
            foreach (var r in asset.responsiveRules)
            {
                var rule = new UIResponsiveRule
                {
                    ruleId = r.ruleId,
                    minResolution = r.minResolution,
                    maxResolution = r.maxResolution,
                    inputMode = r.inputMode,
                    constrainInputMode = r.constrainInputMode
                };
                foreach (var o in r.overrides)
                    rule.overrides.Add(new UIResponsiveOverride
                    {
                        elementId = o.elementId,
                        propertyPath = DesignerPropertyRegistry.EffectivePath(o.propertyId, o.propertyPath),
                        value = DesignerPropertyRegistry.EffectiveValue(o.propertyId, o.typedValue, o.value)
                    });
                result.Add(rule);
            }
            return result.ToArray();
        }

        public static List<string> Validate(DesignerMetadataAsset asset)
        {
            var messages = new List<string>();
            var seen = new HashSet<string>();
            var rules = asset.responsiveRules;

            for (int i = 0; i < rules.Count; i++)
            {
                var r = rules[i];
                if (string.IsNullOrEmpty(r.ruleId)) messages.Add("• empty ruleId");
                else if (!seen.Add(r.ruleId)) messages.Add($"• duplicate ruleId: {r.ruleId}");

                if (r.minResolution.x > r.maxResolution.x || r.minResolution.y > r.maxResolution.y)
                    messages.Add($"• {r.ruleId}: min resolution greater than max");

                foreach (var o in r.overrides)
                {
                    if (string.IsNullOrEmpty(o.elementId) || asset.Find(o.elementId) == null)
                        messages.Add($"• {r.ruleId}: unknown elementId '{o.elementId}'");
                    if (o.propertyId == DesignerPropertyId.None && string.IsNullOrEmpty(o.propertyPath))
                        messages.Add($"• {r.ruleId}: empty property");
                    else if (o.propertyId == DesignerPropertyId.None && DesignerPropertyRegistry.ResolveLegacyPath(o.propertyPath) == DesignerPropertyId.None)
                        messages.Add($"• {r.ruleId}: legacy/unknown propertyPath '{o.propertyPath}'");
                }

                for (int j = i + 1; j < rules.Count; j++)
                {
                    var other = rules[j];
                    if (other.constrainInputMode && r.constrainInputMode && other.inputMode != r.inputMode) continue;
                    if (Overlap(r, other))
                        messages.Add($"• overlapping rules: {r.ruleId} ↔ {other.ruleId}");
                }
            }
            return messages;
        }

        private static bool Overlap(DesignerResponsiveMetadata a, DesignerResponsiveMetadata b)
        {
            bool x = a.minResolution.x <= b.maxResolution.x && b.minResolution.x <= a.maxResolution.x;
            bool y = a.minResolution.y <= b.maxResolution.y && b.minResolution.y <= a.maxResolution.y;
            return x && y;
        }

        /// <summary>Common authoring resolution presets (label, min, max).</summary>
        public static readonly (string label, Vector2Int min, Vector2Int max)[] Presets =
        {
            ("1080p+", new Vector2Int(1920, 1080), new Vector2Int(9999, 9999)),
            ("720p-1080p", new Vector2Int(1280, 720), new Vector2Int(1919, 1079)),
            ("≤720p", new Vector2Int(0, 0), new Vector2Int(1279, 719)),
            ("Mobile Portrait", new Vector2Int(0, 0), new Vector2Int(1080, 1920)),
        };
    }
}

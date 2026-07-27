using System.Collections.Generic;
using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor.Properties;

namespace emiteat.NexUI.Designer.Editor.Variants
{
    /// <summary>
    /// Backend-independent logic for authoring screen variants on a
    /// <see cref="DesignerMetadataAsset"/> and compiling them into Core's
    /// <see cref="UIScreenVariant"/> array. Also runs Designer-side validation that
    /// needs the element list (which is Designer metadata, not runtime data).
    /// </summary>
    public static class VariantService
    {
        public static DesignerVariantMetadata Create(DesignerMetadataAsset asset, string variantId)
        {
            var v = new DesignerVariantMetadata { variantId = variantId, displayName = variantId };
            asset.variants.Add(v);
            return v;
        }

        public static void Delete(DesignerMetadataAsset asset, DesignerVariantMetadata variant)
            => asset.variants.Remove(variant);

        public static DesignerVariantMetadata Duplicate(DesignerMetadataAsset asset, DesignerVariantMetadata source)
        {
            var copy = new DesignerVariantMetadata
            {
                variantId = source.variantId + "_Copy",
                displayName = source.displayName + " (Copy)",
                isDefault = false
            };
            foreach (var o in source.overrides)
                copy.overrides.Add(new DesignerVariantOverrideMetadata
                {
                    targetElementId = o.targetElementId,
                    propertyId = o.propertyId,
                    typedValue = o.typedValue?.Clone(),
                    propertyPath = o.propertyPath,
                    value = o.value
                });
            asset.variants.Add(copy);
            return copy;
        }

        /// <summary>Compiles Designer variants into runtime <see cref="UIScreenVariant"/>.</summary>
        public static UIScreenVariant[] Compile(DesignerMetadataAsset asset)
        {
            var result = new List<UIScreenVariant>(asset.variants.Count);
            foreach (var v in asset.variants)
            {
                var overrides = new List<UIScreenVariantOverride>();
                foreach (var o in v.overrides)
                    overrides.Add(new UIScreenVariantOverride
                    {
                        targetElementId = o.targetElementId,
                        propertyPath = DesignerPropertyRegistry.EffectivePath(o.propertyId, o.propertyPath),
                        value = DesignerPropertyRegistry.EffectiveValue(o.propertyId, o.typedValue, o.value)
                    });
                result.Add(new UIScreenVariant
                {
                    variantId = v.variantId,
                    displayName = v.displayName,
                    overrides = overrides.ToArray()
                });
            }
            return result.ToArray();
        }

        /// <summary>
        /// Validation that needs the Designer element list: duplicate/empty ids,
        /// missing default, override targets that don't exist, empty property paths.
        /// Returns human-readable messages (localization keys resolved by the caller).
        /// </summary>
        public static List<string> Validate(DesignerMetadataAsset asset)
        {
            var messages = new List<string>();
            var seen = new HashSet<string>();
            bool hasDefault = false;

            foreach (var v in asset.variants)
            {
                if (string.IsNullOrEmpty(v.variantId))
                {
                    messages.Add("• (empty) variantId");
                    continue;
                }
                if (!seen.Add(v.variantId))
                    messages.Add($"• duplicate variantId: {v.variantId}");
                if (v.isDefault || string.Equals(v.variantId, "Default", System.StringComparison.OrdinalIgnoreCase))
                    hasDefault = true;

                foreach (var o in v.overrides)
                {
                    if (string.IsNullOrEmpty(o.targetElementId) || asset.Find(o.targetElementId) == null)
                        messages.Add($"• {v.variantId}: unknown elementId '{o.targetElementId}'");
                    if (o.propertyId == DesignerPropertyId.None && string.IsNullOrEmpty(o.propertyPath))
                        messages.Add($"• {v.variantId}: empty propertyPath");
                    else if (o.propertyId == DesignerPropertyId.None && DesignerPropertyRegistry.ResolveLegacyPath(o.propertyPath) == DesignerPropertyId.None)
                        messages.Add($"• {v.variantId}: legacy/unknown propertyPath '{o.propertyPath}'");
                }
            }

            if (asset.variants.Count > 0 && !hasDefault)
                messages.Add("• no Default variant");

            return messages;
        }

        /// <summary>Lists property-level differences between two variants.</summary>
        public static List<string> Diff(DesignerVariantMetadata a, DesignerVariantMetadata b)
        {
            var messages = new List<string>();
            var mapA = ToMap(a);
            var mapB = ToMap(b);

            foreach (var kv in mapA)
            {
                if (!mapB.TryGetValue(kv.Key, out var vb))
                    messages.Add($"- only in {a.variantId}: {kv.Key} = {kv.Value}");
                else if (vb != kv.Value)
                    messages.Add($"~ {kv.Key}: {kv.Value} → {vb}");
            }
            foreach (var kv in mapB)
                if (!mapA.ContainsKey(kv.Key))
                    messages.Add($"+ only in {b.variantId}: {kv.Key} = {kv.Value}");

            return messages;
        }

        private static Dictionary<string, string> ToMap(DesignerVariantMetadata v)
        {
            var map = new Dictionary<string, string>();
            if (v?.overrides == null) return map;
            foreach (var o in v.overrides)
                map[$"{o.targetElementId}.{DesignerPropertyRegistry.EffectivePath(o.propertyId, o.propertyPath)}"] =
                    DesignerPropertyRegistry.EffectiveValue(o.propertyId, o.typedValue, o.value);
            return map;
        }
    }
}

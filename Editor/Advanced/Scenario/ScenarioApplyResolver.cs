using System.Collections.Generic;

namespace emiteat.NexUI.Designer.Editor.Scenario
{
    /// <summary>
    /// One resolved element mutation produced by applying a scenario: which preview channels change
    /// and to what. Deliberately a plain value type with no Unity dependency so the whole mapping is
    /// unit-testable in EditMode without an open designer, a live surface, or Undo.
    /// </summary>
    public struct ScenarioElementChange
    {
        public string ElementId;
        public bool SetPreviewValue;
        public float PreviewValue;
        public bool SetText;
        public string Text;
        public bool SetHidden;
        public bool Hidden;
        public bool SetPreviewImage;
        public UnityEngine.Sprite PreviewImage;
        public bool SetPreviewItemCount;
        public int PreviewItemCount;

        public bool HasAnyChange => SetPreviewValue || SetText || SetHidden || SetPreviewImage || SetPreviewItemCount;
    }

    /// <summary>
    /// Pure mapping from a scenario's mock binding values to per-element preview-channel changes.
    /// A scenario binding key matches an element when the element binds that key on the matching
    /// channel (<c>valueKey</c>→preview value, <c>textKey</c>→text, <c>visibilityKey</c>→visibility);
    /// one key can drive many elements, and one element can be driven by several keys across channels.
    /// </summary>
    public static class ScenarioApplyResolver
    {
        /// <summary>Builds the list of element changes a scenario would apply to the given elements.
        /// Only elements with at least one matching binding channel appear in the result.</summary>
        public static List<ScenarioElementChange> Resolve(
            IReadOnlyList<DesignerElementMetadata> elements,
            IReadOnlyList<DesignerScenarioBinding> bindings)
        {
            var result = new List<ScenarioElementChange>();
            if (elements == null || bindings == null || bindings.Count == 0)
                return result;

            // First occurrence of a key wins so scenario authoring order is stable/predictable.
            var byKey = new Dictionary<string, DesignerScenarioBinding>();
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b == null || string.IsNullOrEmpty(b.key)) continue;
                if (!byKey.ContainsKey(b.key)) byKey[b.key] = b;
            }
            if (byKey.Count == 0) return result;

            for (int i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                if (element == null || element.binding == null) continue;

                var change = new ScenarioElementChange { ElementId = element.elementId };

                if (TryGet(byKey, element.binding.valueKey, out var valueBinding))
                {
                    if (valueBinding.kind == DesignerScenarioValueKind.Sprite)
                    {
                        change.SetPreviewImage = true;
                        change.PreviewImage = valueBinding.spriteValue;
                    }
                    else if (valueBinding.kind == DesignerScenarioValueKind.List)
                    {
                        change.SetPreviewItemCount = true;
                        change.PreviewItemCount = valueBinding.listValue?.Count ?? 0;
                    }
                    else
                    {
                        change.SetPreviewValue = true;
                        change.PreviewValue = ValueOf(valueBinding);
                    }
                }

                if (TryGet(byKey, element.binding.textKey, out var textBinding) &&
                    textBinding.kind == DesignerScenarioValueKind.Text)
                {
                    change.SetText = true;
                    change.Text = textBinding.textValue ?? string.Empty;
                }

                if (TryGet(byKey, element.binding.visibilityKey, out var visBinding) &&
                    visBinding.kind == DesignerScenarioValueKind.Bool)
                {
                    change.SetHidden = true;
                    change.Hidden = !visBinding.boolValue; // visible when the bound bool is true
                }

                if (change.HasAnyChange)
                    result.Add(change);
            }

            return result;
        }

        private static bool TryGet(Dictionary<string, DesignerScenarioBinding> byKey, string key,
            out DesignerScenarioBinding binding)
        {
            binding = null;
            return !string.IsNullOrEmpty(key) && byKey.TryGetValue(key, out binding);
        }

        /// <summary>Numeric value for a preview-value channel: numbers pass through, booleans become
        /// 1/0, and text falls back to 0 (a text value on a numeric channel is not meaningful).</summary>
        private static float ValueOf(DesignerScenarioBinding binding)
        {
            switch (binding.kind)
            {
                case DesignerScenarioValueKind.Number: return binding.numberValue;
                case DesignerScenarioValueKind.Bool: return binding.boolValue ? 1f : 0f;
                default: return 0f;
            }
        }
    }
}

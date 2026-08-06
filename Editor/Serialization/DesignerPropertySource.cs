using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Integrations.UGUI;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Serialization
{
    /// <summary>
    /// Reads authored settings out of Designer metadata for the shared uGUI applier.
    /// </summary>
    /// <remarks>
    /// The Studio half of <see cref="INexPropertySource"/>. It exists so the prefab writer and the
    /// compiled runtime apply properties through the same code instead of each keeping its own
    /// copy - the drift that let a screen save with a character limit and run without one.
    ///
    /// <see cref="DesignerComponentPropertyAccess"/> already falls back to the schema default for
    /// an untouched property, so "is this authored?" has to be asked separately via
    /// <c>IsOverridden</c>. Reporting an unauthored value as authored would make the applier write
    /// over a control's own defaults, and would make an unchanged screen produce a different
    /// prefab on every save.
    /// </remarks>
    internal sealed class DesignerPropertySource : INexPropertySource
    {
        private readonly DesignerElementMetadata _element;

        public DesignerPropertySource(DesignerElementMetadata element) => _element = element;

        private bool Has(string key)
            => _element != null && DesignerComponentPropertyAccess.IsOverridden(_element, key);

        public bool TryGetFloat(string key, out float value)
        {
            value = Has(key) ? DesignerComponentPropertyAccess.GetFloat(_element, key) : 0f;
            return Has(key);
        }

        public bool TryGetInt(string key, out int value)
        {
            value = Has(key) ? DesignerComponentPropertyAccess.GetInt(_element, key) : 0;
            return Has(key);
        }

        public bool TryGetBool(string key, out bool value)
        {
            value = Has(key) && DesignerComponentPropertyAccess.GetBool(_element, key);
            return Has(key);
        }

        public bool TryGetString(string key, out string value)
        {
            value = Has(key) ? DesignerComponentPropertyAccess.GetString(_element, key) : string.Empty;
            return Has(key);
        }

        public bool TryGetColor(string key, out Color value)
        {
            value = Has(key) ? DesignerComponentPropertyAccess.GetColor(_element, key) : Color.white;
            return Has(key);
        }

        public bool TryGetEnumName(string key, out string value)
        {
            value = Has(key) ? DesignerComponentPropertyAccess.GetEnum(_element, key) : string.Empty;
            return Has(key);
        }
    }
}

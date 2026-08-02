using System;
using emiteat.NexUI.State;

namespace emiteat.NexUI.Designer
{
    [Serializable]
    public sealed class DesignerBindingMetadata
    {
        public string textKey;
        public string valueKey;
        public string visibilityKey;
        public string classKey;
        public string commandKey;
        public string interactableKey;
        public UIBindingMode textMode = UIBindingMode.OneWay;
        public UIBindingMode valueMode = UIBindingMode.OneWay;
        public string textConverterKey;
        public string valueConverterKey;
    }
}

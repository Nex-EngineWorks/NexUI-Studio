using UnityEditor;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.UI.Panels
{
    /// <summary>
    /// Compatibility surface for layouts serialized before the placeholder Assets tab was
    /// removed. Asset selection now uses Unity's Project window as the single source of truth.
    /// </summary>
    public sealed class NexUIAssetsPanel : VisualElement
    {
        public NexUIAssetsPanel()
        {
            var open = new Button(EditorUtility.FocusProjectWindow) { text = "Show Project Assets" };
            open.tooltip = "Open Unity's Project window to select sprites, fonts, themes and motion assets.";
            Add(open);
        }
    }
}

using UnityEditor;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Styles
{
    /// <summary>
    /// Loads the Designer stylesheet onto a root element. Shared so the main window and every
    /// detached pane window style themselves identically from one path - a detached Inspector that
    /// silently lost the theme was the obvious failure mode of duplicating this.
    /// </summary>
    public static class DesignerStyleSheet
    {
        public const string Path = "Packages/com.nexengineworks.nexui.studio/Editor/Styles/NexUIDesigner.uss";

        public static void Apply(VisualElement root)
        {
            if (root == null) return;
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(Path);
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
                root.styleSheets.Add(styleSheet);
        }
    }
}

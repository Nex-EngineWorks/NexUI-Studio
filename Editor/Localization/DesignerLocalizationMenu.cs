using UnityEditor;

namespace emiteat.NexUI.Designer.Editor.Localization
{
    /// <summary>
    /// Validators for the language menu. They tick the entry matching the active language so the
    /// menu shows which one is in use, the same way Unity's own toggle menu items behave.
    /// </summary>
    public static class DesignerLocalizationMenu
    {
        private const string KoreanPath = "Tools/NexUI/Preferences/Language/Korean";
        private const string EnglishPath = "Tools/NexUI/Preferences/Language/English";

        [MenuItem(KoreanPath, true)]
        private static bool KoreanValidate()
        {
            UnityEditor.Menu.SetChecked(KoreanPath, DesignerLocalization.CurrentLanguage == DesignerLanguage.Korean);
            return true;
        }

        [MenuItem(EnglishPath, true)]
        private static bool EnglishValidate()
        {
            UnityEditor.Menu.SetChecked(EnglishPath, DesignerLocalization.CurrentLanguage == DesignerLanguage.English);
            return true;
        }
    }
}

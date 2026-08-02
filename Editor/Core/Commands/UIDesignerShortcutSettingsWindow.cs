using System.Collections.Generic;
using System.Linq;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Commands
{
    /// <summary>User-facing editor for the persisted Designer shortcut registry.</summary>
    public sealed class UIDesignerShortcutSettingsWindow : EditorWindow
    {
        private Vector2 scroll;

        [MenuItem("Tools/Nex/NexUI Studio/Preferences/Shortcuts", priority = NexUIDesignerMenu.PriorityPreferences)]
        public static void Open() => GetWindow<UIDesignerShortcutSettingsWindow>(T("shortcuts.window.title"));

        private static string T(string key) => DesignerLocalization.T(key);

        private void OnGUI()
        {
            EditorGUILayout.LabelField(T("shortcuts.heading"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(T("shortcuts.help"), MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var shortcuts = UIDesignerShortcutRegistry.Current;
            for (var i = 0; i < shortcuts.Count; i++)
            {
                var item = shortcuts[i];
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(item.commandId, GUILayout.Width(170));
                    item.key = (KeyCode)EditorGUILayout.EnumPopup(item.key, GUILayout.Width(140));
                    item.ctrl = GUILayout.Toggle(item.ctrl, UIDesignerShortcut.PrimaryModifierLabel, GUILayout.Width(72));
                    item.shift = GUILayout.Toggle(item.shift, "Shift", GUILayout.Width(52));
                    item.alt = GUILayout.Toggle(item.alt, "Alt", GUILayout.Width(42));
                }
            }
            EditorGUILayout.EndScrollView();

            var duplicates = shortcuts.GroupBy(Signature).Where(x => x.Count() > 1).Select(x => x.Key).ToList();
            if (duplicates.Count > 0)
                EditorGUILayout.HelpBox(T("shortcuts.duplicates") + " " + string.Join(", ", duplicates), MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(T("shortcuts.reset"))) UIDesignerShortcutRegistry.ResetToDefaults();
                if (GUILayout.Button(T("toolbar.save")))
                {
                    UIDesignerShortcutRegistry.Save();
                    ShowNotification(new GUIContent(T("shortcuts.saved")));
                }
            }
        }

        private static string Signature(UIDesignerShortcut x) => x.DisplayString();
    }
}

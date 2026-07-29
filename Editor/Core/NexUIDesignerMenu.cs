using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor
{
    /// <summary>
    /// The Tools/NexUI menu. Paths are grouped the way Unity groups its own menus - the windows
    /// you open first at the top, then verb submenus (Screen / Backend / QA / Preferences) - and
    /// are kept in English so a single menu never mixes languages. Priorities create the
    /// separators between the groups.
    /// <para>
    /// Note that <c>Tools/NexUI/Designer</c> is a leaf again: it used to be both a command and a
    /// submenu parent, which is why the direct "open the Designer" entry was easy to miss.
    /// </para>
    /// </summary>
    public static class NexUIDesignerMenu
    {
        internal const int PriorityWindows = 0;
        internal const int PriorityScreen = 20;
        internal const int PriorityBackend = 40;
        internal const int PriorityQa = 60;
        internal const int PriorityPreferences = 80;

        [MenuItem("Tools/NexUI/Designer", priority = PriorityWindows)]
        public static void OpenDesigner() => NexUIDesigner.Open();

        // Panels open as ordinary EditorWindows, so they dock, tab and float like the rest of the
        // editor and their arrangement is saved in the Unity layout.
        [MenuItem("Tools/NexUI/Panels/Explorer", priority = PriorityWindows + 10)]
        public static void OpenExplorerPanel() => UI.Shell.NexUIPaneWindow.Open(UI.Shell.DesignerPaneKind.Explorer);

        [MenuItem("Tools/NexUI/Panels/Inspector", priority = PriorityWindows + 11)]
        public static void OpenInspectorPanel() => UI.Shell.NexUIPaneWindow.Open(UI.Shell.DesignerPaneKind.Inspector);

        [MenuItem("Tools/NexUI/Panels/Output", priority = PriorityWindows + 12)]
        public static void OpenOutputPanel() => UI.Shell.NexUIPaneWindow.Open(UI.Shell.DesignerPaneKind.Output);

        [MenuItem("Tools/NexUI/Panels/Hierarchy", priority = PriorityWindows + 30)]
        public static void OpenHierarchyPanel() => UI.Shell.NexUIPaneWindow.Open(UI.Shell.DesignerPaneKind.Hierarchy);

        [MenuItem("Tools/NexUI/Panels/Library", priority = PriorityWindows + 31)]
        public static void OpenLibraryPanel() => UI.Shell.NexUIPaneWindow.Open(UI.Shell.DesignerPaneKind.Library);

        [MenuItem("Tools/NexUI/Panels/Project Assets", priority = PriorityWindows + 32)]
        public static void OpenProjectPanel() => UI.Shell.NexUIPaneWindow.Open(UI.Shell.DesignerPaneKind.Project);

        /// <summary>The way back when panes have been scattered across the editor layout.</summary>
        [MenuItem("Tools/NexUI/Panels/Dock All Back Into Designer", priority = PriorityWindows + 50)]
        public static void ResetPaneLayout()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<UI.Shell.NexUIPaneWindow>())
                window.Close();
            UI.Shell.DesignerPaneLayout.ResetLayout();
            NexUIDesigner.Open();
        }

        [MenuItem("Tools/NexUI/Screen/Open Selected Screen", priority = PriorityScreen + 1)]
        public static void OpenSelectedScreen()
        {
            var definition = Selection.activeObject as UIScreenDefinition;
            if (definition != null) NexUIDesigner.Open(definition);
            else NexUIDesigner.Open();
        }

        [MenuItem("Tools/NexUI/Screen/Save Screen", priority = PriorityScreen + 2)]
        public static void SaveCurrent() => NexUIDesigner.SaveCurrent();

        [MenuItem("Tools/NexUI/Screen/Validate Screen", priority = PriorityScreen + 4)]
        public static void ValidateCurrent() => NexUIDesigner.ValidateCurrent();

        [MenuItem("Tools/NexUI/Screen/Rebuild Preview", priority = PriorityScreen + 5)]
        public static void RebuildPreview() => NexUIDesigner.RebuildPreview();

        [MenuItem("Tools/NexUI/Backend/Sync Metadata From Backend", priority = PriorityBackend)]
        public static void SyncMetadataFromBackend()
        {
            var context = NexUIDesigner.Open().Context;
            var added = context.SyncMetadataFromBackend();
            Debug.Log($"[NexUI Designer] Synced metadata from backend: {added} element(s) added.");
        }

        [MenuItem("Tools/NexUI/Backend/Sync Metadata From JSON", priority = PriorityBackend + 1)]
        public static void SyncMetadataFromJson()
        {
            var context = NexUIDesigner.Open().Context;
            var applied = context.SyncMetadataFromJson();
            Debug.Log(applied
                ? "[NexUI Designer] Metadata synced from companion JSON."
                : "[NexUI Designer] No companion JSON found (or it failed to parse) for the current metadata asset - save the screen once to create it.");
        }

        [MenuItem("Tools/NexUI/Backend/Apply Metadata To Preview", priority = PriorityBackend + 2)]
        public static void ApplyMetadataToPreview()
        {
            NexUIDesigner.Open().Context.ApplyMetadataToPreview();
        }

        [MenuItem("Tools/NexUI/Backend/Open Backend Asset In UI Builder", priority = PriorityBackend + 3)]
        public static void OpenBackendAsset()
        {
            var asset = NexUIDesigner.Open().Context.CurrentScreen?.backendAsset.asset;
            if (asset == null) { Debug.LogWarning("[NexUI Designer] No backend asset assigned."); return; }
            AssetDatabase.OpenAsset(asset);
        }

        [MenuItem("Tools/NexUI/Backend/Ping Backend Asset", priority = PriorityBackend + 4)]
        public static void PingBackendAsset()
        {
            var asset = NexUIDesigner.Open().Context.CurrentScreen?.backendAsset.asset;
            if (asset == null) { Debug.LogWarning("[NexUI Designer] No backend asset assigned."); return; }
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }

        [MenuItem("Tools/NexUI/Preferences/Language/Korean", priority = PriorityPreferences + 1)]
        public static void Korean() => DesignerLocalization.SetLanguage(DesignerLanguage.Korean);

        [MenuItem("Tools/NexUI/Preferences/Language/English", priority = PriorityPreferences + 2)]
        public static void English() => DesignerLocalization.SetLanguage(DesignerLanguage.English);
    }
}

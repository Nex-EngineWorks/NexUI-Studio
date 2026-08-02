using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor
{
    /// <summary>
    /// The Tools/Nex/NexUI Studio menu. Paths are grouped the way Unity groups its own menus - the
    /// windows you open first at the top, then verb submenus (Screen / Backend / QA / Preferences)
    /// - and are kept in English so a single menu never mixes languages. Priorities create the
    /// separators between the groups.
    /// <para>
    /// The opener is <c>.../Open NexUI Studio</c> rather than the bare <c>Tools/Nex/NexUI Studio</c>
    /// because Unity drops a menu item whose path is also a submenu parent - the bare path would
    /// render as a submenu arrow and the window could never be opened from the menu at all.
    /// </para>
    /// </summary>
    public static class NexUIDesignerMenu
    {
        internal const int PriorityWindows = 0;
        internal const int PriorityScreen = 20;
        internal const int PriorityBackend = 40;
        internal const int PriorityQa = 60;
        internal const int PriorityPreferences = 80;

        [MenuItem("Tools/Nex/NexUI Studio/Open NexUI Studio", priority = PriorityWindows)]
        public static void OpenDesigner() => NexUIDesigner.Open();

        // Panels open as ordinary EditorWindows, so they dock, tab and float like the rest of the
        // editor and their arrangement is saved in the Unity layout.
        [MenuItem("Tools/Nex/NexUI Studio/Panels/Explorer", priority = PriorityWindows + 10)]
        public static void OpenExplorerPanel() => UI.Shell.NexUIPaneWindow.Open(UI.Shell.DesignerPaneKind.Explorer);

        [MenuItem("Tools/Nex/NexUI Studio/Panels/Inspector", priority = PriorityWindows + 11)]
        public static void OpenInspectorPanel() => UI.Shell.NexUIPaneWindow.Open(UI.Shell.DesignerPaneKind.Inspector);

        [MenuItem("Tools/Nex/NexUI Studio/Panels/Output", priority = PriorityWindows + 12)]
        public static void OpenOutputPanel() => UI.Shell.NexUIPaneWindow.Open(UI.Shell.DesignerPaneKind.Output);

        [MenuItem("Tools/Nex/NexUI Studio/Panels/Hierarchy", priority = PriorityWindows + 30)]
        public static void OpenHierarchyPanel() => UI.Shell.NexUIPaneWindow.Open(UI.Shell.DesignerPaneKind.Hierarchy);

        [MenuItem("Tools/Nex/NexUI Studio/Panels/Library", priority = PriorityWindows + 31)]
        public static void OpenLibraryPanel() => UI.Shell.NexUIPaneWindow.Open(UI.Shell.DesignerPaneKind.Library);

        [MenuItem("Tools/Nex/NexUI Studio/Panels/Project Assets", priority = PriorityWindows + 32)]
        public static void OpenProjectPanel() => UI.Shell.NexUIPaneWindow.Open(UI.Shell.DesignerPaneKind.Project);

        /// <summary>The way back when panes have been scattered across the editor layout.</summary>
        [MenuItem("Tools/Nex/NexUI Studio/Panels/Dock All Back Into Designer", priority = PriorityWindows + 50)]
        public static void ResetPaneLayout()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<UI.Shell.NexUIPaneWindow>())
                window.Close();
            UI.Shell.DesignerPaneLayout.ResetLayout();
            NexUIDesigner.Open();
        }

        [MenuItem("Tools/Nex/NexUI Studio/Screen/Open Selected Screen", priority = PriorityScreen + 1)]
        public static void OpenSelectedScreen()
        {
            var definition = Selection.activeObject as UIScreenDefinition;
            if (definition != null) NexUIDesigner.Open(definition);
            else NexUIDesigner.Open();
        }

        [MenuItem("Tools/Nex/NexUI Studio/Screen/Save Screen", priority = PriorityScreen + 2)]
        public static void SaveCurrent() => NexUIDesigner.SaveCurrent();

        [MenuItem("Tools/Nex/NexUI Studio/Screen/Validate Screen", priority = PriorityScreen + 4)]
        public static void ValidateCurrent() => NexUIDesigner.ValidateCurrent();

        [MenuItem("Tools/Nex/NexUI Studio/Screen/Rebuild Preview", priority = PriorityScreen + 5)]
        public static void RebuildPreview() => NexUIDesigner.RebuildPreview();

        /// <summary>
        /// Reads the screen's backend asset back into metadata. On uGUI this is a full Prefab Import
        /// - hierarchy, components and every serialized value - so it is named for what it does.
        /// </summary>
        [MenuItem("Tools/Nex/NexUI Studio/Backend/Import From Backend Asset", priority = PriorityBackend)]
        public static void SyncMetadataFromBackend()
        {
            var context = NexUIDesigner.Open().Context;
            var count = context.SyncMetadataFromBackend();
            Debug.Log($"[NexUI Studio] Imported {count} element(s) from the backend asset.");
        }

        [MenuItem("Tools/Nex/NexUI Studio/Backend/Sync Metadata From JSON", priority = PriorityBackend + 1)]
        public static void SyncMetadataFromJson()
        {
            var context = NexUIDesigner.Open().Context;
            var applied = context.SyncMetadataFromJson();
            Debug.Log(applied
                ? "[NexUI Studio] Metadata synced from companion JSON."
                : "[NexUI Studio] No companion JSON found (or it failed to parse) for the current metadata asset - save the screen once to create it.");
        }

        [MenuItem("Tools/Nex/NexUI Studio/Backend/Apply Metadata To Preview", priority = PriorityBackend + 2)]
        public static void ApplyMetadataToPreview()
        {
            NexUIDesigner.Open().Context.ApplyMetadataToPreview();
        }

        [MenuItem("Tools/Nex/NexUI Studio/Backend/Open Backend Asset In UI Builder", priority = PriorityBackend + 3)]
        public static void OpenBackendAsset()
        {
            var asset = NexUIDesigner.Open().Context.CurrentScreen?.backendAsset.asset;
            if (asset == null) { Debug.LogWarning("[NexUI Studio] No backend asset assigned."); return; }
            AssetDatabase.OpenAsset(asset);
        }

        [MenuItem("Tools/Nex/NexUI Studio/Backend/Ping Backend Asset", priority = PriorityBackend + 4)]
        public static void PingBackendAsset()
        {
            var asset = NexUIDesigner.Open().Context.CurrentScreen?.backendAsset.asset;
            if (asset == null) { Debug.LogWarning("[NexUI Studio] No backend asset assigned."); return; }
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }

        [MenuItem("Tools/Nex/NexUI Studio/Preferences/Language/Korean", priority = PriorityPreferences + 1)]
        public static void Korean() => DesignerLocalization.SetLanguage(DesignerLanguage.Korean);

        [MenuItem("Tools/Nex/NexUI Studio/Preferences/Language/English", priority = PriorityPreferences + 2)]
        public static void English() => DesignerLocalization.SetLanguage(DesignerLanguage.English);
    }
}

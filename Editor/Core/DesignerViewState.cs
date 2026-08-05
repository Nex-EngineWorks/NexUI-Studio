using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor
{
    /// <summary>
    /// How the designer window is currently arranged: zoom, snapping, the active tool and which
    /// panels are open. Persisted per user, never per document.
    /// </summary>
    /// <remarks>
    /// Split out of <c>NexUIDesignerContext</c> because it shares nothing with the rest of it. The
    /// context's real job is the open document - loading, editing, validating and saving it - and
    /// none of that is affected by how far the user has zoomed in. Keeping the two together meant
    /// every reader of the context had to scroll past the other one.
    ///
    /// <b>This class deliberately raises no events.</b> Each setter returns whether the change was
    /// real, and the context decides which of its events to raise. Two reasons: the events belong
    /// to the context's public API and moving them would have changed it, and the notification
    /// rules are not uniform - zoom notifies on every call while the tool only notifies when it
    /// actually changed. Returning a bool preserves each of those exactly rather than smoothing
    /// them into a rule that looks tidier and behaves differently.
    /// </remarks>
    public sealed class DesignerViewState
    {
        private readonly string _prefPrefix;

        public float Zoom { get; private set; }
        public bool SnapEnabled { get; private set; }
        public float GridSize { get; private set; }

        public DesignerTool CurrentTool { get; private set; }
        public DesignerSidebarTab SidebarTab { get; private set; }
        public DesignerInspectorTab InspectorTab { get; private set; }
        public DesignerBottomTab BottomTab { get; private set; }
        public bool BottomDrawerOpen { get; private set; }
        public float BottomDrawerHeight { get; private set; }

        /// <summary>Restores the last session's arrangement, or the defaults on a first run.</summary>
        public DesignerViewState(string prefPrefix)
        {
            _prefPrefix = prefPrefix ?? string.Empty;

            Zoom = EditorPrefs.GetFloat(_prefPrefix + "Zoom", 0.5f);
            SnapEnabled = EditorPrefs.GetBool(_prefPrefix + "Snap", true);
            GridSize = EditorPrefs.GetFloat(_prefPrefix + "GridSize", 8f);
            CurrentTool = (DesignerTool)EditorPrefs.GetInt(_prefPrefix + "Tool", (int)DesignerTool.Select);
            SidebarTab = (DesignerSidebarTab)EditorPrefs.GetInt(_prefPrefix + "SidebarTab", (int)DesignerSidebarTab.Layers);
            InspectorTab = (DesignerInspectorTab)EditorPrefs.GetInt(_prefPrefix + "InspectorTab", (int)DesignerInspectorTab.Design);
            BottomTab = (DesignerBottomTab)EditorPrefs.GetInt(_prefPrefix + "BottomTab", (int)DesignerBottomTab.Validation);
            BottomDrawerOpen = EditorPrefs.GetBool(_prefPrefix + "BottomOpen", false);
            BottomDrawerHeight = EditorPrefs.GetFloat(_prefPrefix + "BottomHeight", 220f);
        }

        // ---- canvas ---------------------------------------------------------
        // These three notify on every call, including a set to the value already held. That is how
        // they behaved before the split: a viewport mid-drag repaints on every step even when the
        // clamp means the number did not move.

        public bool SetZoom(float zoom)
        {
            Zoom = Mathf.Clamp(zoom, 0.15f, 2.0f);
            EditorPrefs.SetFloat(_prefPrefix + "Zoom", Zoom);
            return true;
        }

        public bool SetSnap(bool enabled)
        {
            SnapEnabled = enabled;
            EditorPrefs.SetBool(_prefPrefix + "Snap", enabled);
            return true;
        }

        public bool SetGridSize(float size)
        {
            GridSize = Mathf.Clamp(size, 1f, 64f);
            EditorPrefs.SetFloat(_prefPrefix + "GridSize", GridSize);
            return true;
        }

        // ---- shell ----------------------------------------------------------
        // These guard against a no-op change, because rebuilding the whole inspector or sidebar
        // for a tab that is already active is visible as a flicker.

        public bool SetTool(DesignerTool tool)
        {
            if (CurrentTool == tool) return false;

            CurrentTool = tool;
            EditorPrefs.SetInt(_prefPrefix + "Tool", (int)tool);
            return true;
        }

        public bool SetSidebarTab(DesignerSidebarTab tab)
        {
            if (SidebarTab == tab) return false;

            SidebarTab = tab;
            EditorPrefs.SetInt(_prefPrefix + "SidebarTab", (int)tab);
            return true;
        }

        public bool SetInspectorTab(DesignerInspectorTab tab)
        {
            if (InspectorTab == tab) return false;

            InspectorTab = tab;
            EditorPrefs.SetInt(_prefPrefix + "InspectorTab", (int)tab);
            return true;
        }

        /// <summary>
        /// Selects a bottom tab and sets the drawer's open state in one step.
        /// </summary>
        /// <remarks>
        /// No guard here, unlike the other tab setters: "show me the validation tab" has to open
        /// the drawer even when validation was already the selected tab behind a closed drawer.
        /// </remarks>
        public bool SetBottomTab(DesignerBottomTab tab, bool open)
        {
            BottomTab = tab;
            BottomDrawerOpen = open;
            EditorPrefs.SetInt(_prefPrefix + "BottomTab", (int)tab);
            EditorPrefs.SetBool(_prefPrefix + "BottomOpen", BottomDrawerOpen);
            return true;
        }

        public bool SetBottomDrawerOpen(bool open)
        {
            if (BottomDrawerOpen == open) return false;

            BottomDrawerOpen = open;
            EditorPrefs.SetBool(_prefPrefix + "BottomOpen", open);
            return true;
        }

        public bool SetBottomDrawerHeight(float height)
        {
            BottomDrawerHeight = Mathf.Clamp(height, 180f, 520f);
            EditorPrefs.SetFloat(_prefPrefix + "BottomHeight", BottomDrawerHeight);
            return true;
        }
    }
}

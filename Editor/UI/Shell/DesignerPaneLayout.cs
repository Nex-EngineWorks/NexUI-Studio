using System;
using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.UI.Panels;
using UnityEditor;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.UI.Shell
{
    /// <summary>The regions of the Designer window that can be pulled out into their own dockable window.</summary>
    public enum DesignerPaneKind
    {
        Explorer,
        Inspector,
        Output,
        Hierarchy,
        Library,
        Project
    }

    /// <summary>Everything the shell and the floating window need to know about one pane.</summary>
    public sealed class DesignerPaneDescriptor
    {
        public DesignerPaneKind Kind;
        public string TitleKey;
        public string DetailKey;
        /// <summary>Builds a fresh view. Panes are bound to a context, so each host gets its own instance.</summary>
        public Func<NexUIDesignerContext, VisualElement> Create;
        /// <summary>Regions occupy a slot in the shell layout; the rest are extra views that only ever float.</summary>
        public bool IsShellRegion;
    }

    /// <summary>
    /// Which panes the user has pulled out of the Designer window, and what each pane is.
    ///
    /// Rather than building a docking system, a detached pane becomes a plain <c>EditorWindow</c>
    /// (<see cref="NexUIPaneWindow"/>) - so Unity's own docking, tabbing, floating and layout saving
    /// apply, and the arrangement survives restarts and layout switches like any other editor window.
    /// The shell just leaves out whatever is currently detached.
    /// </summary>
    public static class DesignerPaneLayout
    {
        private const string PrefKey = "NexUI.Designer.DetachedPanes";

        private static readonly Dictionary<DesignerPaneKind, DesignerPaneDescriptor> Descriptors = new();
        private static HashSet<string> _detached;

        /// <summary>Raised when a pane is detached or re-docked so open shells can re-lay themselves out.</summary>
        public static event Action Changed;

        static DesignerPaneLayout()
        {
            Register(new DesignerPaneDescriptor
            {
                Kind = DesignerPaneKind.Explorer,
                TitleKey = "pane.sidebar",
                DetailKey = "pane.sidebar.hierarchy",
                IsShellRegion = true,
                Create = context => new NexUILeftSidebar(context)
            });
            Register(new DesignerPaneDescriptor
            {
                Kind = DesignerPaneKind.Inspector,
                TitleKey = "pane.inspector",
                DetailKey = "pane.inspector.detail",
                IsShellRegion = true,
                Create = context => new NexUIRightInspector(context)
            });
            Register(new DesignerPaneDescriptor
            {
                Kind = DesignerPaneKind.Output,
                TitleKey = "pane.drawer",
                DetailKey = "pane.drawer.validation",
                IsShellRegion = true,
                Create = context => new NexUIBottomDrawer(context)
            });

            // Individual views. These never leave a hole in the shell - they are opened as extra
            // windows, the way you can have two Project windows in Unity.
            Register(new DesignerPaneDescriptor
            {
                Kind = DesignerPaneKind.Hierarchy,
                TitleKey = "shell.tab.hierarchy",
                DetailKey = "pane.sidebar.hierarchy",
                Create = context => new NexUILayersPanel(context)
            });
            Register(new DesignerPaneDescriptor
            {
                Kind = DesignerPaneKind.Library,
                TitleKey = "shell.tab.library",
                DetailKey = "pane.sidebar.library",
                Create = context => new NexUIComponentsPanel(context)
            });
            Register(new DesignerPaneDescriptor
            {
                Kind = DesignerPaneKind.Project,
                TitleKey = "shell.tab.project",
                DetailKey = "pane.sidebar.project",
                Create = _ => new NexUIAssetsPanel()
            });
        }

        private static void Register(DesignerPaneDescriptor descriptor) => Descriptors[descriptor.Kind] = descriptor;

        public static IEnumerable<DesignerPaneDescriptor> All => Descriptors.Values;

        public static DesignerPaneDescriptor Get(DesignerPaneKind kind)
            => Descriptors.TryGetValue(kind, out var descriptor) ? descriptor : null;

        public static string Title(DesignerPaneKind kind)
        {
            var descriptor = Get(kind);
            return descriptor != null ? DesignerLocalization.T(descriptor.TitleKey) : kind.ToString();
        }

        public static string Detail(DesignerPaneKind kind)
        {
            var descriptor = Get(kind);
            return descriptor != null && !string.IsNullOrEmpty(descriptor.DetailKey)
                ? DesignerLocalization.T(descriptor.DetailKey)
                : null;
        }

        private static HashSet<string> Detached
        {
            get
            {
                if (_detached != null) return _detached;
                _detached = new HashSet<string>();
                foreach (var entry in EditorPrefs.GetString(PrefKey, string.Empty).Split('|'))
                    if (!string.IsNullOrEmpty(entry)) _detached.Add(entry);
                return _detached;
            }
        }

        /// <summary>Whether the shell should leave this region out because it lives in its own window.</summary>
        public static bool IsDetached(DesignerPaneKind kind)
        {
            var descriptor = Get(kind);
            if (descriptor == null || !descriptor.IsShellRegion) return false;
            return Detached.Contains(kind.ToString());
        }

        public static void SetDetached(DesignerPaneKind kind, bool detached)
        {
            var descriptor = Get(kind);
            if (descriptor == null || !descriptor.IsShellRegion) return;

            var changed = detached ? Detached.Add(kind.ToString()) : Detached.Remove(kind.ToString());
            if (!changed) return;

            EditorPrefs.SetString(PrefKey, string.Join("|", Detached));
            Changed?.Invoke();
        }

        /// <summary>Puts every region back in the Designer window. The safety net for a layout the user has lost track of.</summary>
        public static void ResetLayout()
        {
            if (Detached.Count == 0) return;
            Detached.Clear();
            EditorPrefs.SetString(PrefKey, string.Empty);
            Changed?.Invoke();
        }
    }
}

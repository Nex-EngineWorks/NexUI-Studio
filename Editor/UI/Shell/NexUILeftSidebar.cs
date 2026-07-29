using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.UI.Controls;
using emiteat.NexUI.Designer.Editor.UI.Panels;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.UI.Shell
{
    public sealed class NexUILeftSidebar : VisualElement
    {
        private readonly NexUIDesignerContext _context;
        private readonly NexUITabBar<DesignerSidebarTab> _tabs;
        private readonly VisualElement _host;
        private readonly NexUILayersPanel _layers;
        private readonly NexUIComponentsPanel _components;
        private readonly NexUIAssetsPanel _assets;
        private readonly NexUIPaneHeader _header;

        public NexUILeftSidebar(NexUIDesignerContext context)
        {
            _context = context;
            AddToClassList("nexui-left-sidebar");

            _header = new NexUIPaneHeader(DesignerLocalization.T("pane.sidebar"))
                .WithDetachButton(() => NexUIPaneWindow.Open(DesignerPaneKind.Explorer),
                    DesignerLocalization.T("pane.detach.tooltip"));
            Add(_header);

            // Tab names follow Unity's own windows so the vocabulary transfers: the element tree is
            // the Hierarchy, the element palette is the Library (as in UI Builder) and the asset
            // browser is the Project. The enum values keep their original names so saved layouts,
            // EditorPrefs and command ids stay valid.
            _tabs = new NexUITabBar<DesignerSidebarTab>(context.SidebarTab, context.SetSidebarTab,
                (DesignerSidebarTab.Layers, DesignerLocalization.T("shell.tab.hierarchy"), DesignerLocalization.T("shell.tab.hierarchy.tooltip")),
                (DesignerSidebarTab.Components, DesignerLocalization.T("shell.tab.library"), DesignerLocalization.T("shell.tab.library.tooltip")),
                (DesignerSidebarTab.Assets, DesignerLocalization.T("shell.tab.project"), DesignerLocalization.T("shell.tab.project.tooltip")));
            Add(_tabs);

            var metadataRow = new VisualElement();
            metadataRow.AddToClassList("nexui-metadata-row");
            var metadata = new ObjectField
            {
                objectType = typeof(DesignerMetadataAsset),
                allowSceneObjects = false,
                label = DesignerLocalization.T("shell.field.metadata"),
                tooltip = DesignerLocalization.T("tooltip.toolbar.metadata")
            };
            metadata.SetValueWithoutNotify(context.Metadata);
            metadata.RegisterValueChangedCallback(evt =>
            {
                if (!context.TrySetMetadata(evt.newValue as DesignerMetadataAsset))
                    metadata.SetValueWithoutNotify(context.Metadata);
            });
            var subscriptions = new ContextBoundSubscriptions(this);
            subscriptions.Add<DesignerMetadataAsset>(h => context.MetadataChanged += h, h => context.MetadataChanged -= h, value => metadata.SetValueWithoutNotify(value));
            metadataRow.Add(metadata);
            var create = new Button(() => context.CreateMetadataAsset()) { text = "+", tooltip = DesignerLocalization.T("tooltip.toolbar.newMetadata") };
            create.AddToClassList("nexui-square-button");
            metadataRow.Add(create);
            Add(metadataRow);

            _host = new VisualElement();
            _host.AddToClassList("nexui-sidebar-host");
            Add(_host);

            _layers = new NexUILayersPanel(context);
            _components = new NexUIComponentsPanel(context);
            _assets = new NexUIAssetsPanel();

            subscriptions.Add(h => context.UIStateChanged += h, h => context.UIStateChanged -= h, Refresh);
            Refresh();
        }

        private void Refresh()
        {
            _tabs.SetCurrent(_context.SidebarTab);
            _host.Clear();

            // The caption names the pane and then says what the *active* tab is for, so the header
            // adds information instead of repeating the tab label underneath it.
            switch (_context.SidebarTab)
            {
                case DesignerSidebarTab.Components:
                    _host.Add(_components);
                    _header.Set(DesignerLocalization.T("pane.sidebar"), DesignerLocalization.T("pane.sidebar.library"));
                    break;
                case DesignerSidebarTab.Assets:
                    _host.Add(_assets);
                    _header.Set(DesignerLocalization.T("pane.sidebar"), DesignerLocalization.T("pane.sidebar.project"));
                    break;
                default:
                    _host.Add(_layers);
                    _header.Set(DesignerLocalization.T("pane.sidebar"), DesignerLocalization.T("pane.sidebar.hierarchy"));
                    break;
            }
        }
    }
}

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

        public NexUILeftSidebar(NexUIDesignerContext context)
        {
            _context = context;
            AddToClassList("nexui-left-sidebar");

            if (context.SidebarTab == DesignerSidebarTab.Assets)
                context.SetSidebarTab(DesignerSidebarTab.Layers);

            _tabs = new NexUITabBar<DesignerSidebarTab>(context.SidebarTab, context.SetSidebarTab,
                (DesignerSidebarTab.Layers, "Layers", "Show screen element layers."),
                (DesignerSidebarTab.Components, "Components", "Add UI components."));
            Add(_tabs);

            var metadataRow = new VisualElement();
            metadataRow.AddToClassList("nexui-metadata-row");
            var metadata = new ObjectField
            {
                objectType = typeof(DesignerMetadataAsset),
                allowSceneObjects = false,
                label = "Metadata",
                tooltip = DesignerLocalization.T("tooltip.toolbar.metadata")
            };
            metadata.RegisterValueChangedCallback(evt => context.SetMetadata(evt.newValue as DesignerMetadataAsset));
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

            subscriptions.Add(h => context.UIStateChanged += h, h => context.UIStateChanged -= h, Refresh);
            Refresh();
        }

        private void Refresh()
        {
            _tabs.SetCurrent(_context.SidebarTab);
            _host.Clear();
            switch (_context.SidebarTab)
            {
                case DesignerSidebarTab.Components:
                    _host.Add(_components);
                    break;
                default:
                    _host.Add(_layers);
                    break;
            }
        }
    }
}

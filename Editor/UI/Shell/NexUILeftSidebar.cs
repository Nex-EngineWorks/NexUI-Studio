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

        public NexUILeftSidebar(NexUIDesignerContext context)
        {
            _context = context;
            AddToClassList("nexui-left-sidebar");

            _tabs = new NexUITabBar<DesignerSidebarTab>(context.SidebarTab, context.SetSidebarTab,
                (DesignerSidebarTab.Layers, "Layers", "Show screen element layers."),
                (DesignerSidebarTab.Components, "Components", "Add UI components."),
                (DesignerSidebarTab.Assets, "Assets", "Browse project sprites, fonts and components; drag them onto the canvas."));
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
            switch (_context.SidebarTab)
            {
                case DesignerSidebarTab.Components:
                    _host.Add(_components);
                    break;
                case DesignerSidebarTab.Assets:
                    _host.Add(_assets);
                    break;
                default:
                    _host.Add(_layers);
                    break;
            }
        }
    }
}

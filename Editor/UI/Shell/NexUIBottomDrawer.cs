using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.Panels;
using emiteat.NexUI.Designer.Editor.UI.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.UI.Shell
{
    public sealed class NexUIBottomDrawer : VisualElement
    {
        private readonly NexUIDesignerContext _context;
        private readonly NexUITabBar<DesignerBottomTab> _tabs;
        private readonly VisualElement _content;
        private readonly Label _badge;
        private readonly Button _toggle;
        private readonly NexUIDesignerValidationPanel _validation;
        private readonly NexUIDesignerHistoryPanel _history;
        private readonly NexUIDesignerScreenGraphPanel _graph;
        private readonly NexUIPreviewLogPanel _previewLog;
        private Vector2 _resizeStart;
        private float _heightStart;

        public NexUIBottomDrawer(NexUIDesignerContext context)
        {
            _context = context;
            AddToClassList("nexui-bottom-drawer");

            var handle = new VisualElement();
            handle.AddToClassList("nexui-drawer-resize");
            handle.RegisterCallback<PointerDownEvent>(OnResizeDown);
            handle.RegisterCallback<PointerMoveEvent>(OnResizeMove);
            handle.RegisterCallback<PointerUpEvent>(OnResizeUp);
            Add(handle);

            var bar = new VisualElement();
            bar.AddToClassList("nexui-drawer-bar");
            Add(bar);

            if (context.BottomTab == DesignerBottomTab.Timeline)
                context.SetBottomTab(DesignerBottomTab.Validation, false);

            // Named after the Unity windows that do the same job (Console, Undo History) so the
            // drawer reads like the rest of the editor. Enum values are unchanged.
            _tabs = new NexUITabBar<DesignerBottomTab>(context.BottomTab, tab => context.SetBottomTab(tab, true),
                (DesignerBottomTab.Validation, DesignerLocalization.T("shell.tab.console"), DesignerLocalization.T("shell.tab.console.tooltip")),
                (DesignerBottomTab.History, DesignerLocalization.T("shell.tab.undoHistory"), DesignerLocalization.T("shell.tab.undoHistory.tooltip")),
                (DesignerBottomTab.Graph, DesignerLocalization.T("shell.tab.screenGraph"), DesignerLocalization.T("shell.tab.screenGraph.tooltip")),
                (DesignerBottomTab.Preview, DesignerLocalization.T("shell.tab.eventLog"), DesignerLocalization.T("shell.tab.eventLog.tooltip")));
            bar.Add(_tabs);

            _badge = new Label();
            _badge.AddToClassList("nexui-drawer-badge");
            bar.Add(_badge);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            bar.Add(spacer);

            _toggle = new Button(() => context.SetBottomDrawerOpen(!context.BottomDrawerOpen));
            _toggle.AddToClassList("nexui-toolbar-button");
            _toggle.AddToClassList("nexui-button-secondary");
            bar.Add(_toggle);

            _content = new VisualElement();
            _content.AddToClassList("nexui-drawer-content");
            Add(_content);

            _validation = new NexUIDesignerValidationPanel(_context);
            _history = new NexUIDesignerHistoryPanel(_context);
            _graph = new NexUIDesignerScreenGraphPanel(_context);
            _previewLog = new NexUIPreviewLogPanel(_context);

            var subscriptions = new ContextBoundSubscriptions(this);
            subscriptions.Add(h => context.UIStateChanged += h, h => context.UIStateChanged -= h, Refresh);
            subscriptions.Add(h => context.ValidationChanged += h, h => context.ValidationChanged -= h, RefreshBadge);
            // Auto-open the Preview tab when a command is simulated so the user sees the result.
            subscriptions.Add<DesignerPreviewLogEntry>(h => context.PreviewLog.CommandSimulated += h,
                h => context.PreviewLog.CommandSimulated -= h,
                _ => _context.SetBottomTab(DesignerBottomTab.Preview, true));
            Refresh();
            RefreshBadge();
        }

        private void Refresh()
        {
            _tabs.SetCurrent(_context.BottomTab);
            _toggle.text = _context.BottomDrawerOpen ? "▾" : "▴";
            _toggle.tooltip = DesignerLocalization.T(_context.BottomDrawerOpen ? "shell.drawer.collapse" : "shell.drawer.expand");
            style.height = _context.BottomDrawerOpen ? _context.BottomDrawerHeight : 36f;
            EnableInClassList("is-open", _context.BottomDrawerOpen);
            _content.style.display = _context.BottomDrawerOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_context.BottomDrawerOpen) return;

            _content.Clear();
            switch (_context.BottomTab)
            {
                case DesignerBottomTab.History:
                    _content.Add(_history);
                    break;
                case DesignerBottomTab.Graph:
                    _content.Add(_graph);
                    break;
                case DesignerBottomTab.Preview:
                    _content.Add(_previewLog);
                    break;
                default:
                    _content.Add(_validation);
                    break;
            }
        }

        private void RefreshBadge()
        {
            if (_context.ErrorCount > 0)
                _badge.text = DesignerLocalization.T("shell.status.errors", _context.ErrorCount);
            else if (_context.WarningCount > 0)
                _badge.text = DesignerLocalization.T("shell.status.warnings", _context.WarningCount);
            else
                _badge.text = DesignerLocalization.T("shell.status.noIssues");
            _badge.EnableInClassList("is-warning", _context.ErrorCount > 0 || _context.WarningCount > 0);
            _badge.EnableInClassList("is-ok", _context.ErrorCount == 0 && _context.WarningCount == 0);
        }

        private void OnResizeDown(PointerDownEvent evt)
        {
            if (!_context.BottomDrawerOpen) return;
            _resizeStart = evt.position;
            _heightStart = _context.BottomDrawerHeight;
            ((VisualElement)evt.currentTarget).CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnResizeMove(PointerMoveEvent evt)
        {
            var target = (VisualElement)evt.currentTarget;
            if (!target.HasPointerCapture(evt.pointerId)) return;
            _context.SetBottomDrawerHeight(_heightStart - (evt.position.y - _resizeStart.y));
            evt.StopPropagation();
        }

        private void OnResizeUp(PointerUpEvent evt)
        {
            var target = (VisualElement)evt.currentTarget;
            if (target.HasPointerCapture(evt.pointerId))
                target.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }
    }
}

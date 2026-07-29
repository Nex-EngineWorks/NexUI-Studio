using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.UI.Controls;
using emiteat.NexUI.Designer.Editor.Viewport;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.UI.Shell
{
    public sealed class NexUIDesignerShell : VisualElement
    {
        public NexUIDesignerContext Context { get; }

        private readonly NexUIDesignerViewport _viewport;
        private readonly NexUICommandPalette _commandPalette;
        private readonly NexUIPaneHeader _canvasHeader;
        private readonly VisualElement _canvasColumn;
        private readonly VisualElement _body;

        public NexUIDesignerShell(NexUIDesignerContext context)
        {
            Context = context;
            AddToClassList("nexui-shell");
            focusable = true;

            var globalToolbar = new NexUIGlobalToolbar(context);
            Add(globalToolbar);

            _viewport = new NexUIDesignerViewport(context);

            var canvasColumn = new VisualElement();
            canvasColumn.AddToClassList("nexui-canvas-column");

            // The canvas caption doubles as the "what am I editing" line: it names the pane and then
            // the screen currently open, which is the question asked most often with several
            // Designer windows or several screens in play.
            _canvasHeader = new NexUIPaneHeader(DesignerLocalization.T("pane.canvas"));
            canvasColumn.Add(_canvasHeader);

            canvasColumn.Add(new NexUICanvasToolbar(context, () => _viewport.FitToView()));
            canvasColumn.Add(new NexUITransformBar(context));
            canvasColumn.Add(_viewport);

            var shellSubscriptions = new ContextBoundSubscriptions(this);
            shellSubscriptions.Add<emiteat.NexUI.Core.UIScreenDefinition>(
                h => context.ScreenChanged += h, h => context.ScreenChanged -= h, _ => RefreshCanvasHeader());
            RefreshCanvasHeader();

            _canvasColumn = canvasColumn;
            _body = new VisualElement();
            _body.AddToClassList("nexui-body-and-drawer");
            Add(_body);

            _commandPalette = new NexUICommandPalette(context);
            Add(_commandPalette);

            RegisterCallback<KeyDownEvent>(OnKeyDown);

            // Panes the user pulled out live in their own windows, so the shell has to be able to
            // re-lay itself out at any time rather than baking the arrangement once at construction.
            RegisterCallback<AttachToPanelEvent>(_ => DesignerPaneLayout.Changed += RebuildBody);
            RegisterCallback<DetachFromPanelEvent>(_ => DesignerPaneLayout.Changed -= RebuildBody);
            RebuildBody();
        }

        /// <summary>
        /// Arranges the regions that are still docked in the window. The canvas column is never
        /// detachable - it is what the Designer window <i>is</i> - and it is kept as a live instance
        /// across rebuilds so re-arranging panes does not throw away the viewport and its state.
        /// </summary>
        private void RebuildBody()
        {
            _body.Clear();
            _canvasColumn.RemoveFromHierarchy();

            var showExplorer = !DesignerPaneLayout.IsDetached(DesignerPaneKind.Explorer);
            var showInspector = !DesignerPaneLayout.IsDetached(DesignerPaneKind.Inspector);
            var showOutput = !DesignerPaneLayout.IsDetached(DesignerPaneKind.Output);

            VisualElement center = _canvasColumn;
            if (showInspector)
            {
                var centerRightSplit = new TwoPaneSplitView(1, 340, TwoPaneSplitViewOrientation.Horizontal)
                {
                    name = "NexUICenterRightSplit",
                    viewDataKey = "NexUI.Rebuild.Split.CenterRight"
                };
                centerRightSplit.Add(_canvasColumn);
                centerRightSplit.Add(new NexUIRightInspector(Context));
                center = centerRightSplit;
            }

            VisualElement bodyRow = center;
            if (showExplorer)
            {
                var bodySplit = new TwoPaneSplitView(0, 260, TwoPaneSplitViewOrientation.Horizontal)
                {
                    name = "NexUIBodySplit",
                    viewDataKey = "NexUI.Rebuild.Split.LeftBody"
                };
                bodySplit.AddToClassList("nexui-rebuild-body");
                bodySplit.Add(new NexUILeftSidebar(Context));
                bodySplit.Add(center);
                bodyRow = bodySplit;
            }
            else
            {
                bodyRow.AddToClassList("nexui-rebuild-body");
            }

            _body.Add(bodyRow);
            if (showOutput) _body.Add(new NexUIBottomDrawer(Context));
        }

        private void RefreshCanvasHeader()
        {
            var screen = Context.CurrentScreen;
            _canvasHeader.Set(DesignerLocalization.T("pane.canvas"),
                screen != null ? screen.ScreenId : DesignerLocalization.T("pane.canvas.noScreen"));
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            var commandPaletteShortcut = evt.keyCode == UnityEngine.KeyCode.K && (evt.ctrlKey || evt.commandKey);
            var vscodeShortcut = evt.keyCode == UnityEngine.KeyCode.P && (evt.ctrlKey || evt.commandKey) && evt.shiftKey;
            if (!commandPaletteShortcut && !vscodeShortcut) return;

            _commandPalette.Toggle();
            evt.StopPropagation();
        }
    }
}

using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.Viewport;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using emiteat.NexUI.Designer.Editor.Productivity;

namespace emiteat.NexUI.Designer.Editor.UI.Shell
{
    /// <summary>
    /// The canvas toolbar, laid out like Unity's Scene view toolbar: transform tools on the left,
    /// then what you are looking at (resolution / preview state), then how you are looking at it
    /// (snapping, zoom). Occasional actions sit in the trailing overflow menu rather than taking a
    /// permanent button, which is what made the old single-row toolbar overflow.
    /// </summary>
    public sealed class NexUICanvasToolbar : VisualElement
    {
        private readonly NexUIDesignerContext _context;
        private readonly Label _zoom;

        public NexUICanvasToolbar(NexUIDesignerContext context, System.Action fitCanvas)
        {
            _context = context;
            AddToClassList("nexui-canvas-toolbar");

            // Tool names mirror the Scene view: View pans, Move drags the selection, Rect edits
            // size and position with handles.
            AddTool(context, DesignerTool.Select, "shell.tool.select");
            AddTool(context, DesignerTool.Move, "shell.tool.move");
            AddTool(context, DesignerTool.Frame, "shell.tool.rect");
            AddTool(context, DesignerTool.Hand, "shell.tool.view");
            AddTool(context, DesignerTool.Pen, "shell.tool.pen");

            Add(Divider());

            var resolution = new PopupField<string>(DesignerLocalization.T("shell.canvas.resolution"))
            {
                tooltip = DesignerLocalization.T("shell.canvas.resolution.tooltip")
            };
            foreach (var preset in DesignerResolutionPreset.Defaults)
                resolution.choices.Add(preset.Name);
            resolution.value = "1920x1080";
            resolution.AddToClassList("nexui-canvas-field");
            resolution.RegisterValueChangedCallback(evt =>
            {
                foreach (var preset in DesignerResolutionPreset.Defaults)
                    if (preset.Name == evt.newValue)
                        context.SetResolution(preset.Resolution);
            });
            Add(resolution);

            var state = new PopupField<string>(DesignerLocalization.T("shell.canvas.state"),
                new List<string> { "Normal", "Hover", "Pressed", "Disabled", "Focused" }, context.PreviewState)
            {
                tooltip = DesignerLocalization.T("tooltip.toolbar.state")
            };
            state.AddToClassList("nexui-mini-popup");
            state.RegisterValueChangedCallback(evt => context.SetPreviewState(evt.newValue));
            Add(state);

            Add(Divider());

            // Grid snapping collapses into one dropdown, the way the Scene view keeps its grid and
            // snap settings behind a single control instead of a toggle plus a loose number field.
            var snapping = MakeButton(null, DesignerLocalization.T("shell.canvas.snapping"),
                DesignerLocalization.T("shell.canvas.snapping.tooltip"));
            snapping.clicked += () => ShowSnappingMenu(context, snapping.worldBound);
            Add(snapping);

            Add(MakeButton(() => context.ZoomBy(-0.1f), "-", DesignerLocalization.T("tooltip.toolbar.zoomOut")));
            _zoom = new Label();
            _zoom.AddToClassList("nexui-zoom-readout");
            _zoom.tooltip = DesignerLocalization.T("shell.canvas.zoom");
            Add(_zoom);
            Add(MakeButton(() => context.ZoomBy(0.1f), "+", DesignerLocalization.T("tooltip.toolbar.zoomIn")));
            Add(MakeButton(fitCanvas, DesignerLocalization.T("shell.canvas.frameAll"),
                DesignerLocalization.T("shell.canvas.frameAll.tooltip")));

            var more = MakeButton(null, "⋮", DesignerLocalization.T("shell.more.tooltip"));
            more.clicked += () => ShowMoreMenu(context, more.worldBound);
            Add(more);

            var subscriptions = new ContextBoundSubscriptions(this);
            subscriptions.Add(h => context.CanvasChanged += h, h => context.CanvasChanged -= h, Refresh);
            subscriptions.Add(h => context.UIStateChanged += h, h => context.UIStateChanged -= h, RefreshTools);
            Refresh();
            RefreshTools();
        }

        private static void ShowSnappingMenu(NexUIDesignerContext context, Rect anchor)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(DesignerLocalization.T("shell.canvas.gridSnap")), context.SnapEnabled,
                () => context.SetSnap(!context.SnapEnabled));
            menu.AddSeparator("");

            var sizeLabel = DesignerLocalization.T("shell.canvas.gridSize") + "/";
            foreach (var size in new[] { 1f, 2f, 4f, 8f, 16f, 32f })
            {
                var captured = size;
                menu.AddItem(new GUIContent(sizeLabel + size.ToString("0")),
                    Mathf.Approximately(context.GridSize, captured), () => context.SetGridSize(captured));
            }
            menu.DropDown(anchor);
        }

        private static void ShowMoreMenu(NexUIDesignerContext context, Rect anchor)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(DesignerLocalization.T("toolbar.rebuildPreview")), false, context.RebuildPreview);
            menu.AddSeparator("");

            var inputLabel = DesignerLocalization.T("shell.canvas.input") + "/";
            foreach (var device in new[] { "Keyboard", "Gamepad", "Touch", "SteamDeck" })
            {
                var captured = device;
                menu.AddItem(new GUIContent(inputLabel + device), context.InputMode == captured,
                    () => context.SetInputMode(captured));
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent(DesignerLocalization.T("productivity.layout")), false,
                () => DesignerLayoutConversionWindow.Open(context));
            menu.AddItem(new GUIContent(DesignerLocalization.T("productivity.transition")), false,
                () => DesignerTransitionPresetWindow.Open(context));
            menu.DropDown(anchor);
        }

        private void AddTool(NexUIDesignerContext context, DesignerTool tool, string labelKey)
        {
            var button = new Button(() => context.SetTool(tool))
            {
                text = DesignerLocalization.T(labelKey),
                tooltip = DesignerLocalization.T(labelKey + ".tooltip")
            };
            button.AddToClassList("nexui-tool-button");
            button.userData = tool;
            Add(button);
        }

        private void Refresh()
        {
            _zoom.text = Mathf.RoundToInt(_context.Zoom * 100f) + "%";
        }

        private void RefreshTools()
        {
            foreach (var child in Children())
                if (child is Button button && button.userData is DesignerTool tool)
                    button.EnableInClassList("is-active", tool == _context.CurrentTool);
            Refresh();
        }

        private static Button MakeButton(System.Action action, string text, string tooltip)
        {
            var button = new Button(action) { text = text, tooltip = tooltip };
            button.AddToClassList("nexui-toolbar-button");
            button.AddToClassList("nexui-button-secondary");
            return button;
        }

        private static VisualElement Divider()
        {
            var divider = new VisualElement();
            divider.AddToClassList("nexui-toolbar-divider");
            return divider;
        }
    }
}

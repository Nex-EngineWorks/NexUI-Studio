using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.Serialization;
using emiteat.NexUI.Designer.Editor.Viewport;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Panels
{
    public sealed class NexUIDesignerToolbar : Toolbar
    {
        private readonly Label _status;
        private string _lastSaveSummary;

        public NexUIDesignerToolbar(NexUIDesignerContext context)
        {
            var subscriptions = new ContextBoundSubscriptions(this);
            AddToClassList("nexui-toolbar");

            var topRow = new VisualElement();
            topRow.AddToClassList("nexui-toolbar-row");
            var bottomRow = new VisualElement();
            bottomRow.AddToClassList("nexui-toolbar-row");
            bottomRow.AddToClassList("nexui-toolbar-row-secondary");
            Add(topRow);
            Add(bottomRow);

            var brand = new VisualElement();
            brand.AddToClassList("nexui-toolbar-brand");
            brand.Add(new Label("NexUI") { name = "BrandTitle" });
            brand.Add(new Label("Designer") { name = "BrandSubtitle" });
            topRow.Add(brand);

            var objectField = new ObjectField
            {
                objectType = typeof(UIScreenDefinition),
                allowSceneObjects = false,
                label = "Screen",
                tooltip = DesignerLocalization.T("tooltip.toolbar.screen")
            };
            objectField.AddToClassList("nexui-screen-field");
            objectField.RegisterValueChangedCallback(evt => context.Open(evt.newValue as UIScreenDefinition));
            topRow.Add(objectField);

            var metadataField = new ObjectField
            {
                objectType = typeof(DesignerMetadataAsset),
                allowSceneObjects = false,
                label = "Metadata",
                tooltip = DesignerLocalization.T("tooltip.toolbar.metadata")
            };
            metadataField.AddToClassList("nexui-metadata-field");
            metadataField.RegisterValueChangedCallback(evt => context.SetMetadata(evt.newValue as DesignerMetadataAsset));
            topRow.Add(metadataField);
            subscriptions.Add<DesignerMetadataAsset>(h => context.MetadataChanged += h, h => context.MetadataChanged -= h, metadata => metadataField.SetValueWithoutNotify(metadata));
            topRow.Add(MakeButton(() => context.CreateMetadataAsset(), "New", "nexui-button-secondary", DesignerLocalization.T("tooltip.toolbar.newMetadata")));

            _status = new Label { tooltip = DesignerLocalization.T("tooltip.toolbar.status") };
            _status.AddToClassList("nexui-toolbar-status");
            topRow.Add(_status);

            var editModeToggle = new ToolbarButton(() =>
                DesignerEditMode.Current = DesignerEditMode.IsAdvanced ? DesignerMode.Simple : DesignerMode.Advanced)
            {
                tooltip = DesignerLocalization.T("tooltip.toolbar.modeToggle")
            };
            editModeToggle.AddToClassList("nexui-toolbar-button");
            editModeToggle.AddToClassList("nexui-button-secondary");
            var editModeHint = new Label { style = { fontSize = 9, color = new StyleColor(new Color(0.55f, 0.63f, 0.71f)) } };
            void RefreshEditModeLabel()
            {
                var advanced = DesignerEditMode.IsAdvanced;
                editModeToggle.text = advanced ? "Mode: Advanced" : "Mode: Simple";
                // C3: "advanced option available" hint - subtle, so users discover Advanced
                // mode organically instead of it being pushed on them.
                editModeHint.text = advanced ? "" : "Theme/Motion/Policy hidden";
                editModeHint.style.display = advanced ? DisplayStyle.None : DisplayStyle.Flex;
            }
            RefreshEditModeLabel();
            subscriptions.Add<DesignerMode>(h => DesignerEditMode.Changed += h, h => DesignerEditMode.Changed -= h, _ => RefreshEditModeLabel());
            topRow.Add(editModeToggle);
            topRow.Add(editModeHint);

            var resolution = new PopupField<string>("Preview") { tooltip = DesignerLocalization.T("tooltip.toolbar.preview") };
            foreach (var preset in DesignerResolutionPreset.Defaults)
                resolution.choices.Add(preset.Name);
            resolution.value = "1920x1080";
            resolution.AddToClassList("nexui-resolution-field");
            resolution.RegisterValueChangedCallback(evt =>
            {
                foreach (var preset in DesignerResolutionPreset.Defaults)
                    if (preset.Name == evt.newValue)
                        context.SetResolution(preset.Resolution);
            });
            bottomRow.Add(resolution);

            var state = new PopupField<string>("State", new System.Collections.Generic.List<string> { "Normal", "Hover", "Pressed", "Disabled", "Focused" }, context.PreviewState)
            {
                tooltip = DesignerLocalization.T("tooltip.toolbar.state")
            };
            state.AddToClassList("nexui-compact-popup");
            state.RegisterValueChangedCallback(evt => context.SetPreviewState(evt.newValue));
            bottomRow.Add(state);

            var input = new PopupField<string>("Input", new System.Collections.Generic.List<string> { "Keyboard", "Gamepad", "Touch", "SteamDeck" }, context.InputMode)
            {
                tooltip = DesignerLocalization.T("tooltip.toolbar.input")
            };
            input.AddToClassList("nexui-compact-popup");
            input.RegisterValueChangedCallback(evt => context.SetInputMode(evt.newValue));
            bottomRow.Add(input);

            var snap = new Toggle("Snap") { value = context.SnapEnabled, tooltip = DesignerLocalization.T("tooltip.toolbar.snap") };
            snap.AddToClassList("nexui-toolbar-toggle");
            snap.RegisterValueChangedCallback(evt => context.SetSnap(evt.newValue));
            bottomRow.Add(snap);

            bottomRow.Add(MakeDivider());

            var zoomOut = MakeButton(() => context.ZoomBy(-0.1f), "-", "nexui-button-secondary", DesignerLocalization.T("tooltip.toolbar.zoomOut"));
            var zoomIn = MakeButton(() => context.ZoomBy(0.1f), "+", "nexui-button-secondary", DesignerLocalization.T("tooltip.toolbar.zoomIn"));
            bottomRow.Add(zoomOut);
            bottomRow.Add(zoomIn);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            bottomRow.Add(spacer);

            bottomRow.Add(MakeButton(context.RebuildPreview, "Rebuild", "nexui-button-secondary", DesignerLocalization.T("tooltip.toolbar.rebuild")));
            bottomRow.Add(MakeButton(() => context.Save(), DesignerLocalization.T("toolbar.save"), "nexui-button-primary", DesignerLocalization.T("tooltip.toolbar.save")));
            bottomRow.Add(MakeButton(context.Validate, DesignerLocalization.T("toolbar.validate"), "nexui-button-secondary", DesignerLocalization.T("tooltip.toolbar.validate")));

            // Align/Distribute/Layer moved to NexUIDesignerAlignPanel (docked beside the
            // Palette) - a third toolbar row for them overlapped the panels below it.

            subscriptions.Add<UIScreenDefinition>(h => context.ScreenChanged += h, h => context.ScreenChanged -= h, _ => RefreshStatus(context));
            subscriptions.Add(h => context.ValidationChanged += h, h => context.ValidationChanged -= h, () => RefreshStatus(context));
            subscriptions.Add<bool>(h => context.DirtyStateChanged += h, h => context.DirtyStateChanged -= h, _ => RefreshStatus(context));
            subscriptions.Add<DesignerSaveReport>(h => context.SaveCompleted += h, h => context.SaveCompleted -= h, report =>
            {
                _lastSaveSummary = report.Summary();
                RefreshStatus(context);
            });
            RefreshStatus(context);
        }

        private static ToolbarButton MakeButton(System.Action action, string text, string className, string tooltip = null)
        {
            var button = new ToolbarButton(action) { text = text, tooltip = tooltip };
            button.AddToClassList("nexui-toolbar-button");
            button.AddToClassList(className);
            return button;
        }

        /// <summary>Thin vertical rule separating logical toolbar groups (Align | Distribute | Layer, etc.).</summary>
        private static VisualElement MakeDivider()
        {
            var divider = new VisualElement();
            divider.AddToClassList("nexui-toolbar-divider");
            return divider;
        }

        private void RefreshStatus(NexUIDesignerContext context)
        {
            if (context.CurrentScreen == null)
            {
                _status.text = "No screen";
                _status.tooltip = "Select a Screen Definition or create a new screen.";
                SetStatusClass("is-muted");
                return;
            }

            var saveState = context.HasUnsavedChanges ? "Unsaved" : _lastSaveSummary == null ? "Loaded" : "Saved";
            var validationState = context.ErrorCount > 0
                ? $"{context.ErrorCount} errors"
                : context.WarningCount > 0
                    ? $"{context.WarningCount} warnings"
                    : "Valid";
            _status.text = $"{context.Backend} | {context.CurrentScreen.ScreenId} | {saveState} | {validationState}";
            _status.tooltip = context.HasUnsavedChanges
                ? "Preview/metadata has unsaved changes. Save writes metadata and the supported backend subset."
                : _lastSaveSummary ?? "Loaded. Run Validate, then Save and verify the result in Play Mode.";
            SetStatusClass(context.HasUnsavedChanges || context.ErrorCount > 0 || context.WarningCount > 0 ? "is-warning" : "is-ok");
        }

        private void SetStatusClass(string className)
        {
            _status.RemoveFromClassList("is-muted");
            _status.RemoveFromClassList("is-ok");
            _status.RemoveFromClassList("is-warning");
            _status.AddToClassList(className);
        }
    }
}

using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.Utilities;
using emiteat.NexUI.Designer.Editor.Productivity;
using emiteat.NexUI.Designer.Editor.Scenario;
using emiteat.NexUI.Designer.Editor.AI;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.UI.Shell
{
    public sealed class NexUIGlobalToolbar : VisualElement
    {
        private readonly Label _backend;
        private readonly Label _status;

        public NexUIGlobalToolbar(NexUIDesignerContext context)
        {
            AddToClassList("nexui-global-toolbar");

            var brand = new Label("NexUI Designer");
            brand.AddToClassList("nexui-global-brand");
            Add(brand);

            Add(MakeButton(DesignerScreenCreationWindow.Open, "+ " + DesignerLocalization.T("productivity.newScreen"), DesignerLocalization.T("productivity.tooltip.newScreen"), "nexui-button-secondary"));

            var screen = new ObjectField
            {
                objectType = typeof(UIScreenDefinition),
                allowSceneObjects = false,
                label = "Screen",
                tooltip = DesignerLocalization.T("tooltip.toolbar.screen")
            };
            screen.AddToClassList("nexui-global-screen");
            screen.RegisterValueChangedCallback(evt => context.Open(evt.newValue as UIScreenDefinition));
            Add(screen);

            _backend = new Label("Backend");
            _backend.AddToClassList("nexui-backend-badge");
            Add(_backend);

            var scenario = new ObjectField
            {
                objectType = typeof(DesignerScenarioAsset),
                allowSceneObjects = false,
                label = DesignerLocalization.T("productivity.scenario"),
                tooltip = "선택한 Mock Data 시나리오를 현재 화면에 적용합니다."
            };
            scenario.AddToClassList("nexui-global-screen");
            scenario.RegisterValueChangedCallback(evt => ScenarioService.Apply(evt.newValue as DesignerScenarioAsset, context));
            Add(scenario);

            var mode = new Button(() =>
                DesignerEditMode.Current = DesignerEditMode.IsAdvanced ? DesignerMode.Simple : DesignerMode.Advanced)
            {
                tooltip = DesignerLocalization.T("tooltip.toolbar.modeToggle")
            };
            mode.AddToClassList("nexui-icon-text-button");
            Add(mode);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            Add(spacer);

            _status = new Label();
            _status.AddToClassList("nexui-toolbar-status");
            Add(_status);

            Add(MakeButton(NexUIAIWindow.Open, DesignerLocalization.T("ai.toolbar"),
                DesignerLocalization.T("ai.toolbar.tooltip"), "nexui-button-secondary"));
            Add(MakeButton(NexUIUtilitiesWindow.Open, DesignerLocalization.T("utilities.title"),
                DesignerLocalization.T("utilities.open.tooltip"), "nexui-button-secondary"));
            Add(MakeButton(context.ApplyMetadataToPreview, "Preview", DesignerLocalization.T("tooltip.toolbar.rebuild"), "nexui-button-secondary"));
            Add(MakeButton(context.Validate, DesignerLocalization.T("toolbar.validate"), DesignerLocalization.T("tooltip.toolbar.validate"), "nexui-button-secondary"));
            Add(MakeButton(() => context.Save(), DesignerLocalization.T("toolbar.save"), DesignerLocalization.T("tooltip.toolbar.save"), "nexui-button-primary"));

            void RefreshMode()
            {
                mode.text = DesignerEditMode.IsAdvanced ? "Pro" : "Beginner";
            }

            void RefreshStatus()
            {
                _backend.text = context.CurrentScreen != null ? context.Backend.ToString() : "No Backend";
                _status.text = context.CurrentScreen == null
                    ? "No screen"
                    : context.ErrorCount > 0 ? context.ErrorCount + " errors"
                    : context.WarningCount > 0 ? context.WarningCount + " warnings"
                    : "Ready";
                _status.EnableInClassList("is-ok", context.CurrentScreen != null && context.ErrorCount == 0 && context.WarningCount == 0);
                _status.EnableInClassList("is-warning", context.ErrorCount > 0 || context.WarningCount > 0);
                _status.EnableInClassList("is-muted", context.CurrentScreen == null);
            }

            var subscriptions = new ContextBoundSubscriptions(this);
            subscriptions.Add<emiteat.NexUI.Core.UIScreenDefinition>(h => context.ScreenChanged += h, h => context.ScreenChanged -= h, _ => RefreshStatus());
            subscriptions.Add(h => context.ValidationChanged += h, h => context.ValidationChanged -= h, RefreshStatus);
            subscriptions.Add<DesignerMode>(h => DesignerEditMode.Changed += h,
                h => DesignerEditMode.Changed -= h, _ => RefreshMode());
            RefreshMode();
            RefreshStatus();
        }

        private static Button MakeButton(System.Action action, string text, string tooltip, string className)
        {
            var button = new Button(action) { text = text, tooltip = tooltip };
            button.AddToClassList("nexui-toolbar-button");
            button.AddToClassList(className);
            return button;
        }
    }
}

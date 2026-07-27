using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor.Commands;
using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.Utilities;
using emiteat.NexUI.Designer.Editor.Productivity;
using emiteat.NexUI.Designer.Editor.Scenario;
using emiteat.NexUI.Designer.Editor.AI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.UI.Shell
{
    /// <summary>
    /// The window-wide toolbar. It carries only what is needed on every edit - what you are
    /// editing (screen + backend), how healthy it is (status) and the three actions used
    /// constantly (Apply To Preview / Validate / Save). Everything else lives behind the
    /// trailing overflow button, the same pattern Unity uses for its own window toolbars.
    /// </summary>
    public sealed class NexUIGlobalToolbar : VisualElement
    {
        private readonly Label _backend;
        private readonly Label _status;
        private readonly ObjectField _scenario;

        public NexUIGlobalToolbar(NexUIDesignerContext context)
        {
            AddToClassList("nexui-global-toolbar");

            var brand = new Label("NexUI");
            brand.AddToClassList("nexui-global-brand");
            Add(brand);

            Add(MakeButton(DesignerScreenCreationWindow.Open, "+",
                DesignerLocalization.T("productivity.tooltip.newScreen"), "nexui-button-secondary"));

            var screen = new ObjectField
            {
                objectType = typeof(UIScreenDefinition),
                allowSceneObjects = false,
                label = DesignerLocalization.T("shell.field.screen"),
                tooltip = DesignerLocalization.T("tooltip.toolbar.screen")
            };
            screen.AddToClassList("nexui-global-screen");
            screen.SetValueWithoutNotify(context.CurrentScreen);
            screen.RegisterValueChangedCallback(evt =>
            {
                if (!context.TryOpen(evt.newValue as UIScreenDefinition))
                    screen.SetValueWithoutNotify(context.CurrentScreen);
            });
            Add(screen);

            _backend = new Label();
            _backend.AddToClassList("nexui-backend-badge");
            Add(_backend);

            // Mock-data scenarios are an authoring-time diagnostic, so the field only takes toolbar
            // space in Advanced mode. The Advanced toggle lives in the overflow menu below.
            _scenario = new ObjectField
            {
                objectType = typeof(DesignerScenarioAsset),
                allowSceneObjects = false,
                label = DesignerLocalization.T("shell.field.scenario"),
                tooltip = DesignerLocalization.T("shell.field.scenario.tooltip")
            };
            _scenario.AddToClassList("nexui-global-screen");
            _scenario.RegisterValueChangedCallback(evt => ScenarioService.Apply(evt.newValue as DesignerScenarioAsset, context));
            Add(_scenario);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            Add(spacer);

            _status = new Label();
            _status.AddToClassList("nexui-toolbar-status");
            Add(_status);

            Add(MakeButton(context.ApplyMetadataToPreview, DesignerLocalization.T("shell.menu.applyToPreview"),
                DesignerLocalization.T("tooltip.toolbar.rebuild"), "nexui-button-secondary"));
            Add(MakeButton(context.Validate, DesignerLocalization.T("toolbar.validate"),
                DesignerLocalization.T("tooltip.toolbar.validate"), "nexui-button-secondary"));
            Add(MakeButton(() => context.Save(), DesignerLocalization.T("toolbar.save"),
                DesignerLocalization.T("tooltip.toolbar.save"), "nexui-button-primary"));

            var more = MakeButton(null, "⋮", DesignerLocalization.T("shell.more.tooltip"), "nexui-button-secondary");
            more.clicked += () => ShowMoreMenu(context, more.worldBound);
            Add(more);

            void RefreshMode()
            {
                _scenario.style.display = DesignerEditMode.IsAdvanced ? DisplayStyle.Flex : DisplayStyle.None;
            }

            void RefreshStatus()
            {
                _backend.text = context.CurrentScreen != null
                    ? context.Backend.ToString()
                    : DesignerLocalization.T("shell.backend.none");
                _status.text = context.CurrentScreen == null
                    ? DesignerLocalization.T("shell.status.noScreen")
                    : context.ErrorCount > 0 ? DesignerLocalization.T("shell.status.errors", context.ErrorCount)
                    : context.WarningCount > 0 ? DesignerLocalization.T("shell.status.warnings", context.WarningCount)
                    : DesignerLocalization.T("shell.status.ready");
                _status.EnableInClassList("is-ok", context.CurrentScreen != null && context.ErrorCount == 0 && context.WarningCount == 0);
                _status.EnableInClassList("is-warning", context.ErrorCount > 0 || context.WarningCount > 0);
                _status.EnableInClassList("is-muted", context.CurrentScreen == null);
            }

            var subscriptions = new ContextBoundSubscriptions(this);
            subscriptions.Add<emiteat.NexUI.Core.UIScreenDefinition>(h => context.ScreenChanged += h, h => context.ScreenChanged -= h, value =>
            {
                screen.SetValueWithoutNotify(value);
                RefreshStatus();
            });
            subscriptions.Add(h => context.ValidationChanged += h, h => context.ValidationChanged -= h, RefreshStatus);
            subscriptions.Add<DesignerMode>(h => DesignerEditMode.Changed += h,
                h => DesignerEditMode.Changed -= h, _ => RefreshMode());
            RefreshMode();
            RefreshStatus();
        }

        /// <summary>
        /// The overflow menu: secondary tools and editor-wide settings, so the toolbar itself only
        /// ever shows the current screen and the actions used on every edit.
        /// </summary>
        private static void ShowMoreMenu(NexUIDesignerContext context, Rect anchor)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent(DesignerLocalization.T("productivity.newScreen")), false, DesignerScreenCreationWindow.Open);
            menu.AddItem(new GUIContent(DesignerLocalization.T("toolbar.rebuildPreview")), false, context.RebuildPreview);
            menu.AddSeparator("");

            menu.AddItem(new GUIContent(DesignerLocalization.T("ai.command.open")), false, NexUIAIWindow.Open);
            menu.AddItem(new GUIContent(DesignerLocalization.T("utilities.command.open")), false, NexUIUtilitiesWindow.Open);
            menu.AddSeparator("");

            var modeLabel = DesignerLocalization.T("shell.menu.mode") + "/";
            menu.AddItem(new GUIContent(modeLabel + DesignerLocalization.T("shell.mode.normal")),
                !DesignerEditMode.IsAdvanced, () => DesignerEditMode.Current = DesignerMode.Simple);
            menu.AddItem(new GUIContent(modeLabel + DesignerLocalization.T("shell.mode.advanced")),
                DesignerEditMode.IsAdvanced, () => DesignerEditMode.Current = DesignerMode.Advanced);

            var languageLabel = DesignerLocalization.T("shell.menu.language") + "/";
            menu.AddItem(new GUIContent(languageLabel + "한국어"), DesignerLocalization.CurrentLanguage == DesignerLanguage.Korean,
                () => DesignerLocalization.SetLanguage(DesignerLanguage.Korean));
            menu.AddItem(new GUIContent(languageLabel + "English"), DesignerLocalization.CurrentLanguage == DesignerLanguage.English,
                () => DesignerLocalization.SetLanguage(DesignerLanguage.English));

            menu.AddSeparator("");
            menu.AddItem(new GUIContent(DesignerLocalization.T("shell.menu.shortcuts")), false,
                UIDesignerShortcutSettingsWindow.Open);

            menu.DropDown(anchor);
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

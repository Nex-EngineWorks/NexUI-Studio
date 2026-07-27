using System;
using System.Collections.Generic;
using System.Text;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.AI
{
    /// <summary>Dockable, approval-first AI chat for the currently focused NexUI Designer session.</summary>
    public sealed class NexUIAIWindow : EditorWindow
    {
        private const string StylePath = "Packages/com.emiteat.nexui.designer/Editor/Styles/NexUIDesigner.uss";
        private readonly List<NexUIAIChatMessage> _messages = new List<NexUIAIChatMessage>();
        private readonly INexUIAIProvider _provider = new OpenAIResponsesProvider();

        // Deliberately session-only. API keys and chat content are not serialized into layouts,
        // EditorPrefs, assets, logs, or source control.
        private string _sessionApiKey = string.Empty;
        private NexUIAIActionPlan _pendingPlan;
        private NexUIAIPlanValidation _pendingValidation;
        private NexUIDesignerContext _pendingContext;
        private DesignerMetadataAsset _pendingMetadata;
        private bool _sending;

        private Label _contextStatus;
        private ScrollView _chat;
        private VisualElement _planHost;
        private TextField _prompt;
        private TextField _keyField;
        private TextField _modelField;
        private Button _sendButton;
        private IDesignerSessionProvider _sessionProvider;

        [MenuItem("Tools/NexUI/AI Assistant", priority = NexUIDesignerMenu.PriorityWindows + 2)]
        public static void Open()
        {
            var window = GetWindow<NexUIAIWindow>();
            window.titleContent = new GUIContent(DesignerLocalization.T("ai.title"));
            window.minSize = new Vector2(420, 520);
            window.Show();
        }

        private void OnEnable()
        {
            _sessionProvider = DesignerSessions.Provider;
            _sessionProvider.ActiveContextChanged += OnActiveContextChanged;
            DesignerLocalization.LanguageChanged += Rebuild;
        }

        private void OnDisable()
        {
            if (_sessionProvider != null) _sessionProvider.ActiveContextChanged -= OnActiveContextChanged;
            _sessionProvider = null;
            DesignerLocalization.LanguageChanged -= Rebuild;
            _sessionApiKey = string.Empty;
            _keyField?.SetValueWithoutNotify(string.Empty);
        }

        public void CreateGUI() => BuildUI();

        private void Rebuild()
        {
            if (rootVisualElement == null) return;
            BuildUI();
            Repaint();
        }

        private void BuildUI()
        {
            titleContent = new GUIContent(DesignerLocalization.T("ai.title"));
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("nexui-designer-root");
            rootVisualElement.AddToClassList("nexui-ai-root");

            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (styles != null) rootVisualElement.styleSheets.Add(styles);

            var header = new VisualElement();
            header.AddToClassList("nexui-ai-header");
            var title = new Label(DesignerLocalization.T("ai.title"));
            title.AddToClassList("nexui-ai-title");
            header.Add(title);
            var subtitle = new Label(DesignerLocalization.T("ai.subtitle"));
            subtitle.AddToClassList("nexui-ai-subtitle");
            header.Add(subtitle);
            _contextStatus = new Label();
            _contextStatus.AddToClassList("nexui-ai-context");
            header.Add(_contextStatus);
            rootVisualElement.Add(header);

            var settings = new Foldout { text = DesignerLocalization.T("ai.settings"), value = false };
            settings.AddToClassList("nexui-ai-settings");

            _keyField = new TextField(DesignerLocalization.T("ai.apiKey"))
            {
                isPasswordField = true,
                value = _sessionApiKey,
                tooltip = DesignerLocalization.T("ai.apiKey.tooltip")
            };
            _keyField.RegisterValueChangedCallback(evt => _sessionApiKey = evt.newValue ?? string.Empty);
            settings.Add(_keyField);

            _modelField = new TextField(DesignerLocalization.T("ai.model")) { value = NexUIAISettings.Model };
            _modelField.RegisterCallback<FocusOutEvent>(_ => NexUIAISettings.Model = _modelField.value);
            settings.Add(_modelField);

            var includeProject = new Toggle(DesignerLocalization.T("ai.includeProject"))
            {
                value = NexUIAISettings.IncludeProjectManifest,
                tooltip = DesignerLocalization.T("ai.includeProject.tooltip")
            };
            includeProject.RegisterValueChangedCallback(evt => NexUIAISettings.IncludeProjectManifest = evt.newValue);
            settings.Add(includeProject);

            var keySource = string.IsNullOrWhiteSpace(NexUIAISettings.EnvironmentApiKey)
                ? DesignerLocalization.T("ai.keySource.session")
                : DesignerLocalization.T("ai.keySource.environment");
            settings.Add(new HelpBox(keySource + "\n" + DesignerLocalization.T("ai.security"), HelpBoxMessageType.Info));
            rootVisualElement.Add(settings);

            var chatBar = new VisualElement();
            chatBar.AddToClassList("nexui-ai-chat-bar");
            var conversationLabel = new Label(DesignerLocalization.T("ai.conversation"));
            conversationLabel.AddToClassList("nexui-ai-section-title");
            chatBar.Add(conversationLabel);
            var clear = new Button(ClearConversation) { text = DesignerLocalization.T("ai.clear") };
            clear.AddToClassList("nexui-button-secondary");
            chatBar.Add(clear);
            rootVisualElement.Add(chatBar);

            _chat = new ScrollView();
            _chat.AddToClassList("nexui-ai-chat");
            rootVisualElement.Add(_chat);

            _planHost = new VisualElement();
            _planHost.AddToClassList("nexui-ai-plan-host");
            rootVisualElement.Add(_planHost);

            var composer = new VisualElement();
            composer.AddToClassList("nexui-ai-composer");
            _prompt = new TextField { multiline = true, tooltip = DesignerLocalization.T("ai.prompt.tooltip") };
            _prompt.AddToClassList("nexui-ai-prompt");
            _prompt.RegisterCallback<KeyDownEvent>(OnPromptKeyDown);
            composer.Add(_prompt);
            _sendButton = new Button(Send) { text = DesignerLocalization.T("ai.send") };
            _sendButton.AddToClassList("nexui-button-primary");
            composer.Add(_sendButton);
            rootVisualElement.Add(composer);

            var hint = new Label(DesignerLocalization.T("ai.prompt.hint"));
            hint.AddToClassList("nexui-ai-hint");
            rootVisualElement.Add(hint);

            RefreshContextStatus();
            RebuildConversation();
            RebuildPlan();
            RefreshBusyState();
            rootVisualElement.schedule.Execute(() => _prompt?.Focus());
        }

        private async void Send()
        {
            if (_sending) return;
            var userText = (_prompt?.value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(userText)) return;

            var context = DesignerSessions.ActiveContext;
            if (context == null || context.IsDisposed || context.CurrentScreen == null || context.Metadata == null)
            {
                AddMessage("error", DesignerLocalization.T("ai.noContext"));
                return;
            }

            var apiKey = string.IsNullOrWhiteSpace(_sessionApiKey)
                ? NexUIAISettings.EnvironmentApiKey
                : _sessionApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                AddMessage("error", DesignerLocalization.T("ai.noKey"));
                _keyField?.Focus();
                return;
            }

            if (_modelField != null) NexUIAISettings.Model = _modelField.value;
            _prompt.value = string.Empty;
            AddMessage("user", userText);
            _pendingPlan = null;
            _pendingValidation = null;
            _pendingContext = null;
            _pendingMetadata = null;
            _sending = true;
            RefreshBusyState();
            RebuildPlan();

            try
            {
                var snapshot = NexUIAIContextBuilder.Build(context, NexUIAISettings.IncludeProjectManifest);
                var raw = await _provider.CompleteAsync(new NexUIAIProviderRequest
                {
                    ApiKey = apiKey,
                    Model = NexUIAISettings.Model,
                    Instructions = NexUIAIContextBuilder.Instructions,
                    Input = BuildInput(snapshot)
                });

                if (this == null) return;
                if (!NexUIAIPlanParser.TryParse(raw, out var plan, out var parseError))
                {
                    AddMessage("error", parseError);
                    return;
                }

                var validation = NexUIAIActionService.Validate(context, plan);
                AddMessage(validation.IsValid ? "assistant" : "error", plan.message);
                _pendingPlan = plan;
                _pendingValidation = validation;
                _pendingContext = context;
                _pendingMetadata = context.Metadata;
                RebuildPlan();
            }
            catch (Exception exception)
            {
                if (this != null) AddMessage("error", exception.Message);
            }
            finally
            {
                if (this != null)
                {
                    _sending = false;
                    RefreshBusyState();
                }
            }
        }

        private string BuildInput(string snapshot)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Conversation (oldest to newest):");
            var start = Math.Max(0, _messages.Count - 8);
            for (var i = start; i < _messages.Count; i++)
            {
                var message = _messages[i];
                if (message == null || message.role == "error") continue;
                builder.Append(message.role).Append(": ").AppendLine(message.text ?? string.Empty);
            }
            builder.AppendLine("Current NexUI Designer snapshot:");
            builder.Append(snapshot);
            return builder.ToString();
        }

        private void ApplyPendingPlan()
        {
            var context = DesignerSessions.ActiveContext;
            if (_pendingPlan == null || _pendingValidation == null || !_pendingValidation.IsValid) return;
            if (context == null || context != _pendingContext || context.Metadata != _pendingMetadata)
            {
                AddMessage("error", DesignerLocalization.T("ai.contextChanged"));
                return;
            }

            if (_pendingPlan.HasDestructiveActions &&
                !EditorUtility.DisplayDialog(DesignerLocalization.T("ai.delete.title"),
                    DesignerLocalization.T("ai.delete.message"), DesignerLocalization.T("ai.apply"),
                    DesignerLocalization.T("ai.cancel")))
                return;

            try
            {
                var count = _pendingPlan.actions?.Count ?? 0;
                NexUIAIActionService.Apply(context, _pendingPlan);
                AddMessage("assistant", string.Format(DesignerLocalization.T("ai.applySuccess"), count));
                DiscardPendingPlan();
            }
            catch (Exception exception)
            {
                AddMessage("error", exception.Message);
                _pendingValidation = NexUIAIActionService.Validate(context, _pendingPlan);
                RebuildPlan();
            }
        }

        private void DiscardPendingPlan()
        {
            _pendingPlan = null;
            _pendingValidation = null;
            _pendingContext = null;
            _pendingMetadata = null;
            RebuildPlan();
        }

        private void RebuildPlan()
        {
            if (_planHost == null) return;
            _planHost.Clear();
            if (_pendingPlan == null) return;

            var card = new VisualElement();
            card.AddToClassList("nexui-ai-plan");
            var title = new Label(DesignerLocalization.T("ai.plan"));
            title.AddToClassList("nexui-ai-section-title");
            card.Add(title);

            foreach (var description in NexUIAIActionService.Describe(_pendingPlan))
            {
                var row = new Label("• " + description);
                row.AddToClassList("nexui-ai-action-row");
                card.Add(row);
            }

            if (_pendingPlan.actions == null || _pendingPlan.actions.Count == 0)
            {
                var empty = new Label(DesignerLocalization.T("ai.plan.empty"));
                empty.AddToClassList("nexui-ai-hint");
                card.Add(empty);
            }

            if (_pendingValidation != null && !_pendingValidation.IsValid)
            {
                foreach (var error in _pendingValidation.Errors)
                    card.Add(new HelpBox(error, HelpBoxMessageType.Error));
            }

            var buttons = new VisualElement();
            buttons.AddToClassList("nexui-ai-plan-buttons");
            var apply = new Button(ApplyPendingPlan) { text = DesignerLocalization.T("ai.apply") };
            apply.AddToClassList("nexui-button-primary");
            apply.SetEnabled(_pendingValidation != null && _pendingValidation.IsValid &&
                             _pendingPlan.actions != null && _pendingPlan.actions.Count > 0);
            buttons.Add(apply);
            var discard = new Button(DiscardPendingPlan) { text = DesignerLocalization.T("ai.discard") };
            discard.AddToClassList("nexui-button-secondary");
            buttons.Add(discard);
            card.Add(buttons);
            _planHost.Add(card);
        }

        private void AddMessage(string role, string text)
        {
            _messages.Add(new NexUIAIChatMessage(role, text ?? string.Empty));
            RebuildConversation();
        }

        private void RebuildConversation()
        {
            if (_chat == null) return;
            _chat.Clear();
            if (_messages.Count == 0)
            {
                var welcome = new Label(DesignerLocalization.T("ai.welcome"));
                welcome.AddToClassList("nexui-ai-welcome");
                _chat.Add(welcome);
                return;
            }

            foreach (var message in _messages)
            {
                var bubble = new VisualElement();
                bubble.AddToClassList("nexui-ai-message");
                bubble.AddToClassList("is-" + message.role);
                var role = new Label(DesignerLocalization.T("ai.role." + message.role));
                role.AddToClassList("nexui-ai-message-role");
                bubble.Add(role);
                var body = new Label(message.text) { enableRichText = false };
                body.AddToClassList("nexui-ai-message-body");
                bubble.Add(body);
                _chat.Add(bubble);
            }
            _chat.schedule.Execute(() => _chat.scrollOffset = new Vector2(0, float.MaxValue));
        }

        private void ClearConversation()
        {
            _messages.Clear();
            DiscardPendingPlan();
            RebuildConversation();
        }

        private void OnPromptKeyDown(KeyDownEvent evt)
        {
            if ((evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) || !evt.ctrlKey) return;
            Send();
            evt.StopPropagation();
        }

        private void OnActiveContextChanged(NexUIDesignerContext _) => RefreshContextStatus();

        private void RefreshContextStatus()
        {
            if (_contextStatus == null) return;
            var context = DesignerSessions.ActiveContext;
            _contextStatus.text = context?.CurrentScreen == null
                ? DesignerLocalization.T("ai.status.noContext")
                : string.Format(DesignerLocalization.T("ai.status.context"), context.CurrentScreen.ScreenId,
                    context.SelectedElements.Count);
            _contextStatus.EnableInClassList("is-ready", context?.Metadata != null);
        }

        private void RefreshBusyState()
        {
            if (_sendButton == null) return;
            _sendButton.text = _sending ? DesignerLocalization.T("ai.busy") : DesignerLocalization.T("ai.send");
            _sendButton.SetEnabled(!_sending);
            _prompt?.SetEnabled(!_sending);
        }
    }
}

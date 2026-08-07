using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace emiteat.NexUI.Designer.Editor.AI
{
    public enum NexUIAIProviderKind { OpenAI, Anthropic, Gemini, OpenAICompatible }

    public sealed class NexUIAIProviderDescriptor
    {
        public NexUIAIProviderKind Kind;
        public string DisplayName;
        public string DefaultModel;
        public string EnvironmentVariable;
        public string DefaultEndpoint;
        public bool CustomEndpoint;
        public bool RequiresApiKey = true;
        public Func<INexUIAIProvider> Create;
    }

    public static class NexUIAIProviderRegistry
    {
        private static readonly NexUIAIProviderDescriptor[] Providers =
        {
            new NexUIAIProviderDescriptor { Kind = NexUIAIProviderKind.OpenAI, DisplayName = "OpenAI", DefaultModel = "gpt-5.6-sol", EnvironmentVariable = "OPENAI_API_KEY", DefaultEndpoint = "https://api.openai.com/v1/responses", Create = () => new OpenAIResponsesProvider() },
            new NexUIAIProviderDescriptor { Kind = NexUIAIProviderKind.Anthropic, DisplayName = "Anthropic Claude", DefaultModel = "claude-sonnet-5", EnvironmentVariable = "ANTHROPIC_API_KEY", DefaultEndpoint = "https://api.anthropic.com/v1/messages", Create = () => new AnthropicMessagesProvider() },
            new NexUIAIProviderDescriptor { Kind = NexUIAIProviderKind.Gemini, DisplayName = "Google Gemini", DefaultModel = "gemini-3.5-flash", EnvironmentVariable = "GEMINI_API_KEY", DefaultEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent", Create = () => new GeminiGenerateContentProvider() },
            new NexUIAIProviderDescriptor { Kind = NexUIAIProviderKind.OpenAICompatible, DisplayName = "OpenAI-compatible", DefaultModel = "model-name", EnvironmentVariable = "NEXUI_AI_API_KEY", DefaultEndpoint = "http://localhost:11434/v1/chat/completions", CustomEndpoint = true, RequiresApiKey = false, Create = () => new OpenAICompatibleProvider() }
        };

        public static IReadOnlyList<NexUIAIProviderDescriptor> All => Providers;
        public static NexUIAIProviderDescriptor Get(NexUIAIProviderKind kind) => Array.Find(Providers, item => item.Kind == kind) ?? Providers[0];
    }

    public static class NexUIAISettings
    {
        private const string Prefix = "NexUI.Designer.AI.";
        public static NexUIAIProviderKind Provider { get => (NexUIAIProviderKind)EditorPrefs.GetInt(Prefix + "Provider", 0); set => EditorPrefs.SetInt(Prefix + "Provider", (int)value); }
        public static bool IncludeProjectManifest { get => EditorPrefs.GetBool(Prefix + "IncludeProjectManifest", false); set => EditorPrefs.SetBool(Prefix + "IncludeProjectManifest", value); }
        public static string Model(NexUIAIProviderKind kind) => EditorPrefs.GetString(Prefix + "Model." + kind, NexUIAIProviderRegistry.Get(kind).DefaultModel);
        public static void SetModel(NexUIAIProviderKind kind, string value) => EditorPrefs.SetString(Prefix + "Model." + kind, string.IsNullOrWhiteSpace(value) ? NexUIAIProviderRegistry.Get(kind).DefaultModel : value.Trim());
        public static string Endpoint(NexUIAIProviderKind kind) => EditorPrefs.GetString(Prefix + "Endpoint." + kind, NexUIAIProviderRegistry.Get(kind).DefaultEndpoint);
        public static void SetEndpoint(NexUIAIProviderKind kind, string value) => EditorPrefs.SetString(Prefix + "Endpoint." + kind, string.IsNullOrWhiteSpace(value) ? NexUIAIProviderRegistry.Get(kind).DefaultEndpoint : value.Trim());
        public static string EnvironmentApiKey(NexUIAIProviderKind kind) => Environment.GetEnvironmentVariable(NexUIAIProviderRegistry.Get(kind).EnvironmentVariable) ?? string.Empty;
        public static NexUIAIScopePolicy Scope
        {
            get
            {
                var preset = (NexUIAIScopePreset)EditorPrefs.GetInt(Prefix + "ScopePreset", (int)NexUIAIScopePreset.SelectedSafe);
                var policy = preset == NexUIAIScopePreset.Custom ? new NexUIAIScopePolicy { preset = preset } : NexUIAIScopePolicy.ForPreset(preset);
                policy.targetScope = (NexUIAITargetScope)EditorPrefs.GetInt(Prefix + "TargetScope", (int)policy.targetScope);
                policy.capabilities = (NexUIAICapability)EditorPrefs.GetInt(Prefix + "Capabilities", (int)policy.capabilities);
                policy.allowDestructiveActions = EditorPrefs.GetBool(Prefix + "AllowDestructive", policy.allowDestructiveActions);
                return policy;
            }
            set
            {
                value ??= NexUIAIScopePolicy.ForPreset(NexUIAIScopePreset.SelectedSafe);
                EditorPrefs.SetInt(Prefix + "ScopePreset", (int)value.preset);
                EditorPrefs.SetInt(Prefix + "TargetScope", (int)value.targetScope);
                EditorPrefs.SetInt(Prefix + "Capabilities", (int)value.capabilities);
                EditorPrefs.SetBool(Prefix + "AllowDestructive", value.allowDestructiveActions);
            }
        }
    }

    public sealed class AnthropicMessagesProvider : INexUIAIProvider
    {
        public string DisplayName => "Anthropic Claude";
        public async Task<string> CompleteAsync(NexUIAIProviderRequest requestData)
        {
            NexUIAIProviderUtility.RequireRequest(requestData, "ANTHROPIC_API_KEY");
            var payload = JsonUtility.ToJson(new AnthropicRequest
            {
                model = requestData.Model, max_tokens = 6000, system = requestData.Instructions ?? string.Empty,
                messages = new[] { new AnthropicMessage { role = "user", content = requestData.Input ?? string.Empty } }
            });
            using var request = NexUIAIProviderUtility.CreatePost("https://api.anthropic.com/v1/messages", payload, 120);
            request.SetRequestHeader("x-api-key", requestData.ApiKey.Trim());
            request.SetRequestHeader("anthropic-version", "2023-06-01");
            await request.SendWebRequest();
            var response = JsonUtility.FromJson<AnthropicResponse>(NexUIAIProviderUtility.EnsureSuccess(request, DisplayName));
            if (response?.content != null)
                foreach (var part in response.content)
                    if (part != null && part.type == "text" && !string.IsNullOrWhiteSpace(part.text)) return part.text;
            throw new InvalidOperationException("Anthropic response did not contain text content.");
        }
        [Serializable] private sealed class AnthropicRequest { public string model; public int max_tokens; public string system; public AnthropicMessage[] messages; }
        [Serializable] private sealed class AnthropicMessage { public string role; public string content; }
        [Serializable] private sealed class AnthropicResponse { public AnthropicContent[] content; }
        [Serializable] private sealed class AnthropicContent { public string type; public string text; }
    }

    public sealed class GeminiGenerateContentProvider : INexUIAIProvider
    {
        public string DisplayName => "Google Gemini";
        public async Task<string> CompleteAsync(NexUIAIProviderRequest requestData)
        {
            NexUIAIProviderUtility.RequireRequest(requestData, "GEMINI_API_KEY");
            var payload = JsonUtility.ToJson(new GeminiRequest
            {
                system_instruction = new GeminiContent { parts = new[] { new GeminiPart { text = requestData.Instructions ?? string.Empty } } },
                contents = new[] { new GeminiContent { role = "user", parts = new[] { new GeminiPart { text = requestData.Input ?? string.Empty } } } },
                generationConfig = new GeminiGenerationConfig { responseMimeType = "application/json", maxOutputTokens = 6000 }
            });
            var endpoint = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent"
                .Replace("{model}", UnityWebRequest.EscapeURL(requestData.Model));
            using var request = NexUIAIProviderUtility.CreatePost(endpoint, payload, 120);
            request.SetRequestHeader("x-goog-api-key", requestData.ApiKey.Trim());
            await request.SendWebRequest();
            var response = JsonUtility.FromJson<GeminiResponse>(NexUIAIProviderUtility.EnsureSuccess(request, DisplayName));
            if (response?.candidates != null)
                foreach (var candidate in response.candidates)
                    if (candidate?.content?.parts != null)
                        foreach (var part in candidate.content.parts)
                            if (part != null && !string.IsNullOrWhiteSpace(part.text)) return part.text;
            throw new InvalidOperationException("Gemini response did not contain text content.");
        }
        [Serializable] private sealed class GeminiRequest { public GeminiContent system_instruction; public GeminiContent[] contents; public GeminiGenerationConfig generationConfig; }
        [Serializable] private sealed class GeminiGenerationConfig { public string responseMimeType; public int maxOutputTokens; }
        [Serializable] private sealed class GeminiContent { public string role; public GeminiPart[] parts; }
        [Serializable] private sealed class GeminiPart { public string text; }
        [Serializable] private sealed class GeminiResponse { public GeminiCandidate[] candidates; }
        [Serializable] private sealed class GeminiCandidate { public GeminiContent content; }
    }

    public sealed class OpenAICompatibleProvider : INexUIAIProvider
    {
        public string DisplayName => "OpenAI-compatible";
        public async Task<string> CompleteAsync(NexUIAIProviderRequest requestData)
        {
            if (requestData == null) throw new ArgumentNullException(nameof(requestData));
            if (string.IsNullOrWhiteSpace(requestData.Model)) throw new InvalidOperationException("Choose a model before sending the request.");
            if (string.IsNullOrWhiteSpace(requestData.Endpoint)) throw new InvalidOperationException("Set the compatible API endpoint.");
            var payload = JsonUtility.ToJson(new CompatibleRequest
            {
                model = requestData.Model, temperature = 0.1f,
                messages = new[] { new CompatibleMessage { role = "system", content = requestData.Instructions ?? string.Empty }, new CompatibleMessage { role = "user", content = requestData.Input ?? string.Empty } }
            });
            using var request = NexUIAIProviderUtility.CreatePost(requestData.Endpoint.Trim(), payload, 120);
            if (!string.IsNullOrWhiteSpace(requestData.ApiKey))
                request.SetRequestHeader("Authorization", "Bearer " + requestData.ApiKey.Trim());
            await request.SendWebRequest();
            var response = JsonUtility.FromJson<CompatibleResponse>(NexUIAIProviderUtility.EnsureSuccess(request, DisplayName));
            if (response?.choices != null && response.choices.Length > 0 && !string.IsNullOrWhiteSpace(response.choices[0]?.message?.content))
                return response.choices[0].message.content;
            throw new InvalidOperationException("Compatible API response did not contain message content.");
        }
        [Serializable] private sealed class CompatibleRequest { public string model; public float temperature; public CompatibleMessage[] messages; }
        [Serializable] private sealed class CompatibleMessage { public string role; public string content; }
        [Serializable] private sealed class CompatibleResponse { public CompatibleChoice[] choices; }
        [Serializable] private sealed class CompatibleChoice { public CompatibleMessage message; }
    }
}

using System;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace emiteat.NexUI.Designer.Editor.AI
{
    public static class NexUIAISettings
    {
        private const string ModelKey = "NexUI.Designer.AI.Model";
        private const string ProjectManifestKey = "NexUI.Designer.AI.IncludeProjectManifest";
        public const string ApiKeyEnvironmentVariable = "OPENAI_API_KEY";
        public const string DefaultModel = "gpt-5.6-sol";

        public static string Model
        {
            get => EditorPrefs.GetString(ModelKey, DefaultModel);
            set => EditorPrefs.SetString(ModelKey, string.IsNullOrWhiteSpace(value) ? DefaultModel : value.Trim());
        }

        public static bool IncludeProjectManifest
        {
            get => EditorPrefs.GetBool(ProjectManifestKey, false);
            set => EditorPrefs.SetBool(ProjectManifestKey, value);
        }

        public static string EnvironmentApiKey
            => Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable) ?? string.Empty;
    }

    /// <summary>
    /// Minimal OpenAI Responses API transport. The API key is supplied per request and is never
    /// serialized into a Unity asset or EditorPrefs.
    /// </summary>
    public sealed class OpenAIResponsesProvider : INexUIAIProvider
    {
        private const string Endpoint = "https://api.openai.com/v1/responses";
        public string DisplayName => "OpenAI Responses API";

        public async UniTask<string> CompleteAsync(NexUIAIProviderRequest requestData)
        {
            if (requestData == null) throw new ArgumentNullException(nameof(requestData));
            if (string.IsNullOrWhiteSpace(requestData.ApiKey))
                throw new InvalidOperationException($"Set {NexUIAISettings.ApiKeyEnvironmentVariable} or enter a session API key.");

            var payload = JsonUtility.ToJson(new ResponsesRequest
            {
                model = string.IsNullOrWhiteSpace(requestData.Model) ? NexUIAISettings.DefaultModel : requestData.Model,
                instructions = requestData.Instructions ?? string.Empty,
                input = requestData.Input ?? string.Empty,
                max_output_tokens = 4000,
                store = false
            });

            using var webRequest = new UnityWebRequest(Endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 120
            };
            webRequest.SetRequestHeader("Authorization", "Bearer " + requestData.ApiKey.Trim());
            webRequest.SetRequestHeader("Content-Type", "application/json");

            await webRequest.SendWebRequest().ToUniTask();

            var body = webRequest.downloadHandler?.text ?? string.Empty;
            if (webRequest.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException(FormatApiError(webRequest.responseCode, webRequest.error, body));

            var response = JsonUtility.FromJson<ResponsesResponse>(body);
            var text = ExtractOutputText(response);
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("OpenAI response did not contain output_text.");
            return text;
        }

        private static string ExtractOutputText(ResponsesResponse response)
        {
            if (response?.output == null) return null;
            foreach (var item in response.output)
            {
                if (item?.content == null) continue;
                foreach (var content in item.content)
                    if (content != null && content.type == "output_text" && !string.IsNullOrEmpty(content.text))
                        return content.text;
            }
            return null;
        }

        private static string FormatApiError(long code, string transportError, string body)
        {
            string message = null;
            try { message = JsonUtility.FromJson<ResponsesErrorEnvelope>(body)?.error?.message; }
            catch { /* fall back to the transport error */ }
            if (string.IsNullOrWhiteSpace(message)) message = transportError;
            if (string.IsNullOrWhiteSpace(message)) message = "Unknown API error.";
            if (message.Length > 800) message = message.Substring(0, 800) + "…";
            return $"OpenAI API error ({code}): {message}";
        }

        [Serializable]
        private sealed class ResponsesRequest
        {
            public string model;
            public string instructions;
            public string input;
            public int max_output_tokens;
            public bool store;
        }

        [Serializable]
        private sealed class ResponsesResponse
        {
            public ResponsesOutputItem[] output;
        }

        [Serializable]
        private sealed class ResponsesOutputItem
        {
            public ResponsesContent[] content;
        }

        [Serializable]
        private sealed class ResponsesContent
        {
            public string type;
            public string text;
        }

        [Serializable]
        private sealed class ResponsesErrorEnvelope
        {
            public ResponsesError error;
        }

        [Serializable]
        private sealed class ResponsesError
        {
            public string message;
        }
    }
}

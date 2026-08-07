using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace emiteat.NexUI.Designer.Editor.AI
{
    /// <summary>OpenAI Responses API transport. Secrets are supplied per request and never persisted.</summary>
    public sealed class OpenAIResponsesProvider : INexUIAIProvider
    {
        private const string DefaultEndpoint = "https://api.openai.com/v1/responses";
        public string DisplayName => "OpenAI";

        public async Task<string> CompleteAsync(NexUIAIProviderRequest requestData)
        {
            NexUIAIProviderUtility.RequireRequest(requestData, "OPENAI_API_KEY");
            var payload = JsonUtility.ToJson(new ResponsesRequest
            {
                model = requestData.Model,
                instructions = requestData.Instructions ?? string.Empty,
                input = requestData.Input ?? string.Empty,
                max_output_tokens = 6000,
                store = false
            });
            using var webRequest = NexUIAIProviderUtility.CreatePost(DefaultEndpoint, payload, 120);
            webRequest.SetRequestHeader("Authorization", "Bearer " + requestData.ApiKey.Trim());
            await webRequest.SendWebRequest();
            var body = NexUIAIProviderUtility.EnsureSuccess(webRequest, DisplayName);
            var response = JsonUtility.FromJson<ResponsesResponse>(body);
            if (response?.output != null)
                foreach (var item in response.output)
                    if (item?.content != null)
                        foreach (var content in item.content)
                            if (content != null && content.type == "output_text" && !string.IsNullOrWhiteSpace(content.text))
                                return content.text;
            throw new InvalidOperationException("OpenAI response did not contain output text.");
        }

        [Serializable] private sealed class ResponsesRequest { public string model; public string instructions; public string input; public int max_output_tokens; public bool store; }
        [Serializable] private sealed class ResponsesResponse { public ResponsesOutputItem[] output; }
        [Serializable] private sealed class ResponsesOutputItem { public ResponsesContent[] content; }
        [Serializable] private sealed class ResponsesContent { public string type; public string text; }
    }

    internal static class NexUIAIProviderUtility
    {
        public static void RequireRequest(NexUIAIProviderRequest request, string environmentVariable)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.ApiKey))
                throw new InvalidOperationException($"Set {environmentVariable} or enter a session API key.");
            if (string.IsNullOrWhiteSpace(request.Model))
                throw new InvalidOperationException("Choose a model before sending the request.");
        }

        public static UnityWebRequest CreatePost(string endpoint, string payload, int timeout)
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                throw new InvalidOperationException("The provider endpoint must be an absolute HTTP(S) URL.");
            var request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload ?? string.Empty)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = timeout
            };
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        public static string EnsureSuccess(UnityWebRequest request, string provider)
        {
            var body = request.downloadHandler?.text ?? string.Empty;
            if (request.result == UnityWebRequest.Result.Success) return body;
            var detail = string.IsNullOrWhiteSpace(body) ? request.error : body;
            if (string.IsNullOrWhiteSpace(detail)) detail = "Unknown API error.";
            if (detail.Length > 800) detail = detail.Substring(0, 800) + "...";
            throw new InvalidOperationException($"{provider} API error ({request.responseCode}): {detail}");
        }
    }
}

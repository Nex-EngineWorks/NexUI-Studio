using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace emiteat.NexUI.Integrations.Figma
{
    /// <summary>Deserialized response of Figma's <c>GET /v1/me</c> - used only to confirm a token is valid.</summary>
    [Serializable]
    public sealed class FigmaUser
    {
        public string id;
        public string email;
        public string handle;
    }

    /// <summary>
    /// Thin wrapper over the Figma REST API authentication and file surfaces. Mapping the fetched
    /// document is intentionally kept in <see cref="FigmaDocumentImporter"/> so transport and
    /// Designer mutation remain independently testable.
    /// </summary>
    public static class FigmaApiClient
    {
        private const string BaseUrl = "https://api.figma.com/v1";

        /// <summary>Calls GET /v1/me. Throws if the token is missing/invalid or the request fails.</summary>
        public static async Task<FigmaUser> GetAuthenticatedUserAsync(string token)
        {
            using var request = UnityWebRequest.Get($"{BaseUrl}/me");
            request.SetRequestHeader("X-Figma-Token", token);
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException($"Figma API error ({request.responseCode}): {request.error}");

            return JsonUtility.FromJson<FigmaUser>(request.downloadHandler.text);
        }

        /// <summary>Calls GET /v1/files/{fileKey} and returns the raw JSON document (the file's full node tree).</summary>
        public static async Task<string> GetFileJsonAsync(string token, string fileKey)
        {
            if (string.IsNullOrEmpty(fileKey))
                throw new ArgumentException("Figma file key is required.", nameof(fileKey));

            using var request = UnityWebRequest.Get($"{BaseUrl}/files/{fileKey}");
            request.SetRequestHeader("X-Figma-Token", token);
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException($"Figma API error ({request.responseCode}): {request.error}");

            return request.downloadHandler.text;
        }
    }
}

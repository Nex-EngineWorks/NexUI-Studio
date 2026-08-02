using System;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Integrations.Figma
{
    /// <summary>
    /// C5: stores the user's Figma personal access token in <see cref="EditorPrefs"/>, keyed
    /// per-project (via <see cref="Application.dataPath"/>) so it is scoped to this machine and
    /// this project only. Never written to any asset, scene, or other file that could end up in
    /// version control - EditorPrefs lives outside the project folder entirely.
    /// </summary>
    public static class FigmaCredentials
    {
        private static string Key => "NexUI.Figma.Token.v2." + ProjectKeyFor(
            Application.dataPath, Application.platform == RuntimePlatform.WindowsEditor);
        private static string LegacyKey => "NexUI.Figma.Token." + Application.dataPath.GetHashCode();

        /// <summary>
        /// Stable project suffix. Unlike <see cref="string.GetHashCode()"/>, SHA-256 does not change
        /// between Editor launches, Mono runtimes, CPU architectures, or operating systems.
        /// </summary>
        public static string ProjectKeyFor(string dataPath, bool caseInsensitiveFileSystem)
        {
            var normalized = (dataPath ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            if (caseInsensitiveFileSystem) normalized = normalized.ToUpperInvariant();
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
            var builder = new StringBuilder(32);
            for (var i = 0; i < 16; i++) builder.Append(bytes[i].ToString("x2"));
            return builder.ToString();
        }

        public static string Token
        {
            get
            {
                if (EditorPrefs.HasKey(Key)) return EditorPrefs.GetString(Key, string.Empty);
                if (!EditorPrefs.HasKey(LegacyKey)) return string.Empty;
                var legacy = EditorPrefs.GetString(LegacyKey, string.Empty);
                EditorPrefs.SetString(Key, legacy);
                EditorPrefs.DeleteKey(LegacyKey);
                return legacy;
            }
            set
            {
                EditorPrefs.SetString(Key, value ?? string.Empty);
                EditorPrefs.DeleteKey(LegacyKey);
            }
        }

        public static bool HasToken => !string.IsNullOrEmpty(Token);

        public static void Clear()
        {
            EditorPrefs.DeleteKey(Key);
            EditorPrefs.DeleteKey(LegacyKey);
        }
    }
}

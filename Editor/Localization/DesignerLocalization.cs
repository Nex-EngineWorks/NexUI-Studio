using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Localization
{
    public static class DesignerLocalization
    {
        private const string PrefKey = "NexUI.Designer.Language";
        private const string PackageRoot = "Packages/com.emiteat.nexui.designer/Localization";
        private static readonly Dictionary<string, string> Current = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> English = new Dictionary<string, string>();
        private static DesignerLanguage _language;

        public static DesignerLanguage CurrentLanguage => _language;
        public static event Action LanguageChanged;

        static DesignerLocalization()
        {
            _language = (DesignerLanguage)EditorPrefs.GetInt(PrefKey, (int)DesignerLanguage.Korean);
            Load();
        }

        public static void SetLanguage(DesignerLanguage language)
        {
            _language = language;
            EditorPrefs.SetInt(PrefKey, (int)language);
            Load();
            LanguageChanged?.Invoke();
        }

        public static string T(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (Current.TryGetValue(key, out var value)) return value;
            if (English.TryGetValue(key, out value)) return value;
            return key;
        }

        public static string T(string key, params object[] args)
            => string.Format(T(key), args);

        private static void Load()
        {
            Current.Clear();
            English.Clear();
            LoadFile(Path.Combine(PackageRoot, "en-US.json"), English);
            LoadFile(Path.Combine(PackageRoot, _language == DesignerLanguage.Korean ? "ko-KR.json" : "en-US.json"), Current);
        }

        private static void LoadFile(string path, Dictionary<string, string> target)
        {
            if (!File.Exists(path)) return;
            try
            {
                // Localization files are ordinary UTF-8 JSON objects. The old line parser only
                // recognized lines beginning with a quote, so the equally valid comma-first style
                // used by appended sections ( ,"key": "value" ) was silently ignored. Parse the
                // JSON token stream instead; this also handles escaped quotes/newlines/unicode and
                // keeps English fallback available if one file is malformed.
                ParseFlatStringObject(File.ReadAllText(path, Encoding.UTF8), target);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NexUI Designer] Failed to load localization file '{path}': {ex.Message}");
            }
        }

        internal static void ParseFlatStringObject(string json, Dictionary<string, string> target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrWhiteSpace(json)) return;

            var index = 0;
            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == '\uFEFF') index++;
            SkipWhitespace(json, ref index);
            if (index >= json.Length || json[index] != '{')
                throw new FormatException("Expected a JSON object.");
            index++;

            while (index < json.Length)
            {
                SkipWhitespaceAndCommas(json, ref index);
                if (index < json.Length && json[index] == '}') return;
                var key = ReadJsonString(json, ref index);
                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] != ':')
                    throw new FormatException($"Expected ':' after localization key '{key}'.");
                index++;
                SkipWhitespace(json, ref index);
                var value = ReadJsonString(json, ref index);
                if (!string.IsNullOrEmpty(key)) target[key] = value;
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ',') index++;
            }

            throw new FormatException("Localization JSON object was not closed.");
        }

        private static string ReadJsonString(string json, ref int index)
        {
            if (index >= json.Length || json[index] != '"')
                throw new FormatException($"Expected a JSON string at character {index}.");
            index++;
            var value = new StringBuilder();
            while (index < json.Length)
            {
                var c = json[index++];
                if (c == '"') return value.ToString();
                if (c != '\\')
                {
                    value.Append(c);
                    continue;
                }

                if (index >= json.Length) throw new FormatException("Unterminated JSON escape.");
                var escape = json[index++];
                switch (escape)
                {
                    case '"': value.Append('"'); break;
                    case '\\': value.Append('\\'); break;
                    case '/': value.Append('/'); break;
                    case 'b': value.Append('\b'); break;
                    case 'f': value.Append('\f'); break;
                    case 'n': value.Append('\n'); break;
                    case 'r': value.Append('\r'); break;
                    case 't': value.Append('\t'); break;
                    case 'u':
                        if (index + 4 > json.Length) throw new FormatException("Incomplete unicode escape.");
                        if (!ushort.TryParse(json.Substring(index, 4), System.Globalization.NumberStyles.HexNumber,
                                System.Globalization.CultureInfo.InvariantCulture, out var code))
                            throw new FormatException("Invalid unicode escape.");
                        value.Append((char)code);
                        index += 4;
                        break;
                    default: throw new FormatException($"Unsupported JSON escape '\\{escape}'.");
                }
            }
            throw new FormatException("Unterminated JSON string.");
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
        }

        private static void SkipWhitespaceAndCommas(string json, ref int index)
        {
            while (index < json.Length && (char.IsWhiteSpace(json[index]) || json[index] == ',')) index++;
        }
    }
}

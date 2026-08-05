using System;
using System.Collections.Generic;
using System.Text;

namespace emiteat.NexUI.Designer.Editor.Properties
{
    /// <summary>
    /// Text form of the two list-valued theme fields, so they can travel through the single-value
    /// override pipeline.
    /// </summary>
    /// <remarks>
    /// Classes and token overrides are lists, and every mechanism that carries an override - the typed
    /// <see cref="DesignerPropertyValue"/>, the companion JSON, exposed properties, variant rules -
    /// carries one value. Giving them a text form means all of that keeps working unchanged; a nested
    /// list would have needed a parallel path through every one.
    ///
    /// The formats are the ones already used for the same data elsewhere: classes are space separated
    /// the way USS and HTML write a class list, and tokens are <c>key=value</c> pairs separated by
    /// <c>;</c>. Round-tripping is exact for every value that does not itself contain the separator,
    /// and a value that does is rejected at authoring rather than silently split.
    /// </remarks>
    public static class DesignerThemeValueCodec
    {
        public static string FormatClasses(List<string> classes)
        {
            if (classes == null || classes.Count == 0) return string.Empty;
            return string.Join(" ", classes);
        }

        public static List<string> ParseClasses(string value)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(value)) return result;
            foreach (var part in value.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                if (!result.Contains(part)) result.Add(part);
            return result;
        }

        public static string FormatTokens(List<DesignerTokenOverride> tokens)
        {
            if (tokens == null || tokens.Count == 0) return string.Empty;
            var builder = new StringBuilder();
            foreach (var token in tokens)
            {
                if (token == null || string.IsNullOrEmpty(token.key)) continue;
                if (builder.Length > 0) builder.Append(';');
                builder.Append(token.key).Append('=').Append(token.value ?? string.Empty);
            }
            return builder.ToString();
        }

        /// <summary>
        /// Parses <c>key=value;key=value</c>. A pair with no <c>=</c> is dropped rather than stored as
        /// a key with a null value, which would read back as "this token is set to nothing".
        /// </summary>
        public static List<DesignerTokenOverride> ParseTokens(string value)
        {
            var result = new List<DesignerTokenOverride>();
            if (string.IsNullOrWhiteSpace(value)) return result;

            foreach (var pair in value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var equals = pair.IndexOf('=');
                if (equals <= 0) continue;

                var key = pair.Substring(0, equals).Trim();
                if (key.Length == 0) continue;

                var existing = result.Find(t => string.Equals(t.key, key, StringComparison.Ordinal));
                var token = existing ?? new DesignerTokenOverride { key = key };
                token.value = pair.Substring(equals + 1).Trim();
                if (existing == null) result.Add(token);
            }
            return result;
        }

        /// <summary>Why <paramref name="value"/> cannot be stored as a class list, or null when it can.</summary>
        public static string ValidateClasses(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            foreach (var part in ParseClasses(value))
                if (part.IndexOf('=') >= 0 || part.IndexOf(';') >= 0)
                    return $"Class '{part}' contains a reserved character.";
            return null;
        }

        /// <summary>Why <paramref name="value"/> cannot be stored as a token list, or null when it can.</summary>
        public static string ValidateTokens(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            foreach (var pair in value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                if (pair.IndexOf('=') <= 0)
                    return $"'{pair.Trim()}' is not a key=value pair.";
            return null;
        }
    }
}

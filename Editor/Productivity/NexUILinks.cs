using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Productivity
{
    /// <summary>
    /// Every outward-facing URL NexUI shows, in one place.
    /// </summary>
    /// <remarks>
    /// Two rules this exists to enforce.
    ///
    /// First, a link is only drawn when it has an address. A button that opens nothing, or a
    /// documentation URL pointing at a repository that moved, is worse than no button - it is the
    /// dead link an Asset Store submission gets flagged for. Empty entries here simply do not render.
    ///
    /// Second, nothing here opens a browser on its own. Unity's Asset Store guidelines forbid a
    /// package launching external pages as a side effect of being imported, so opening a URL is
    /// always the result of the user clicking one of these buttons.
    /// </remarks>
    internal static class NexUILinks
    {
        /// <summary>Project repository.</summary>
        public const string Repository = "https://github.com/Nex-EngineWorks/NexUI-Studio";

        /// <summary>User documentation entry point.</summary>
        public const string Documentation = "https://github.com/Nex-EngineWorks/NexUI-Studio/tree/master/Documentation~";

        /// <summary>Where users report problems.</summary>
        public const string IssueTracker = "https://github.com/Nex-EngineWorks/NexUI-Studio/issues";

        /// <summary>
        /// Community chat. Never opened automatically, and never used to gate a feature.
        /// </summary>
        /// <remarks>
        /// Still empty, so no button is drawn. Asset Store rule 3.1.f forbids distributing perks
        /// such as discount codes through a community channel, so this stays a link and nothing more.
        /// </remarks>
        public const string Community = "";

        public static bool Any =>
            Has(Repository) || Has(Documentation) || Has(Community) || Has(IssueTracker);

        private static bool Has(string url) => !string.IsNullOrWhiteSpace(url);

        /// <summary>Draws a link button, or nothing at all when the address is not set yet.</summary>
        public static void Button(string label, string url, float width = 0f)
        {
            if (!Has(url)) return;

            var options = width > 0f
                ? new[] { GUILayout.Width(width), GUILayout.Height(22f) }
                : new[] { GUILayout.Height(22f) };

            if (GUILayout.Button(new GUIContent(label, url), options))
                Application.OpenURL(url);
        }

        /// <summary>Draws the whole link row. Renders nothing while no address is set.</summary>
        public static void DrawRow()
        {
            if (!Any) return;

            EditorGUILayout.LabelField("Links", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                Button("Documentation", Documentation);
                Button("Repository", Repository);
                Button("Community", Community);
                Button("Report an issue", IssueTracker);
            }
        }
    }
}

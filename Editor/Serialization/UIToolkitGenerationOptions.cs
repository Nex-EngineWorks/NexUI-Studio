using System.Collections.Generic;
using UnityEditor;

namespace emiteat.NexUI.Designer.Editor.Serialization
{
    /// <summary>
    /// How the UXML generator writes NexUI base components.
    /// </summary>
    /// <remarks>
    /// Two honest answers exist and projects want different ones, so this is a setting rather than a
    /// decision baked into the generator:
    ///
    /// <b>Custom elements</b> emit <c>&lt;emiteat.NexUI...NXGradientElement /&gt;</c>. It works
    /// immediately, shows up in UI Builder, and makes the generated UXML depend on the NexUI runtime
    /// assembly - fine for a project already using NexUI at runtime.
    ///
    /// <b>Standard tags</b> emit <c>ui:VisualElement</c> plus a <c>nx-gradient</c> class. The UXML
    /// stays dependency-free and portable, and the project supplies the behaviour or styling.
    ///
    /// The struct itself carries no editor state, so the generator stays a pure function of its input.
    /// </remarks>
    public readonly struct UIToolkitGenerationOptions
    {
        private const string CustomElementsPrefKey = "NexUI.Designer.Generation.EmitCustomElements";

        public readonly bool EmitCustomElements;

        public UIToolkitGenerationOptions(bool emitCustomElements) => EmitCustomElements = emitCustomElements;

        /// <summary>Custom elements, because that is the option that works with no extra project setup.</summary>
        public static UIToolkitGenerationOptions Default => new UIToolkitGenerationOptions(true);

        public static UIToolkitGenerationOptions FromSettings()
            => new UIToolkitGenerationOptions(EditorPrefs.GetBool(CustomElementsPrefKey, true));

        public static void SetEmitCustomElements(bool value) => EditorPrefs.SetBool(CustomElementsPrefKey, value);
    }

    /// <summary>
    /// UXML identity for NexUI base components: the custom element tag they emit, and the class used
    /// instead when the generator is asked to keep the UXML free of NexUI types.
    /// </summary>
    public static class NexUIBaseUxmlTags
    {
        private const string Namespace = "emiteat.NexUI.Integrations.UIToolkit";

        /// <summary>Component type id → the UI Toolkit element it becomes. Null means it has no element of its own.</summary>
        private static readonly Dictionary<string, string> Elements = new Dictionary<string, string>
        {
            { "NX.Gradient", "NXGradientElement" },
            { "NX.SafeArea", "NXSafeAreaElement" },
            { "NX.RadialLayout", "NXRadialContainer" },
            { "NX.SegmentedBar", "NXSegmentedBarElement" },
            { "NX.CooldownOverlay", "NXCooldownElement" },
            { "NX.MarqueeText", "NXMarqueeLabel" },
            { "NX.TypewriterText", "NXTypewriterLabel" },
            { "NX.NumberTicker", "NXNumberTickerLabel" },
            { "NX.HoldButton", "NXHoldButton" }
        };

        /// <summary>Fully qualified tag, or null for components with no UI Toolkit element.</summary>
        public static string TagFor(string componentTypeId)
            => componentTypeId != null && Elements.TryGetValue(componentTypeId, out var element)
                ? Namespace + "." + element
                : null;

        /// <summary>"NX.RoundedRect" → "nx-rounded-rect".</summary>
        public static string ClassFor(string componentTypeId)
        {
            if (string.IsNullOrEmpty(componentTypeId)) return string.Empty;
            var name = componentTypeId.StartsWith("NX.", System.StringComparison.Ordinal)
                ? componentTypeId.Substring(3)
                : componentTypeId;

            var builder = new System.Text.StringBuilder("nx", name.Length + 8);
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (char.IsUpper(c)) builder.Append('-').Append(char.ToLowerInvariant(c));
                else builder.Append(c);
            }
            return builder.ToString();
        }
    }
}

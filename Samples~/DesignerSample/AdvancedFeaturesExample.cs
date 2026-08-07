using Cysharp.Threading.Tasks;
using UnityEngine;
using emiteat.NexUI.Core;
using emiteat.NexUI.Localization;
using emiteat.NexUI.Prompt;
using emiteat.NexUI.Theme;

namespace emiteat.NexUI.Samples
{
    /// <summary>
    /// Minimal runtime example of the Advanced Extension Pack surface (variants, game
    /// localization, prompt glyphs, contrast). This lives under Samples~ so it is not
    /// compiled until the user imports the sample.
    /// </summary>
    public sealed class AdvancedFeaturesExample : MonoBehaviour
    {
        [SerializeField] private UIGameLocalizationTable _localization;
        [SerializeField] private UIPromptGlyphTable _prompts;
        [SerializeField] private string _language = "ko-KR";

        private async Task OpenInventoryForController()
        {
            // Open the "ControllerMode" variant of the Inventory screen.
            // Bare `NexUI` resolves to the namespace inside the emiteat.NexUI.* tree, so
            // the static facade is referenced as Core.NexUIApp.
            await Core.NexUIApp.OpenAsync("Inventory", new UIOpenArgs { variantId = "ControllerMode" });
        }

        private string ResumeLabel()
            => _localization != null ? _localization.Resolve("button.resume", _language) : "Resume";

        private UIPromptGlyphEntry SubmitGlyph(UIPromptDevice device)
            => _prompts != null ? _prompts.Find("Submit", device) : null;

        private bool TextIsReadable(Color text, Color background)
            => ThemeContrastChecker.MeetsAa(text, background);
    }
}

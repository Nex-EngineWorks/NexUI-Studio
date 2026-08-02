using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Commands
{
    /// <summary>A rebindable key combination that triggers a <see cref="IUIDesignerCommand"/> by id.</summary>
    [Serializable]
    public sealed class UIDesignerShortcut
    {
        public string commandId;
        public KeyCode key;
        public bool ctrl;
        public bool shift;
        public bool alt;

        public UIDesignerShortcut() { }

        public UIDesignerShortcut(string commandId, KeyCode key, bool ctrl = false, bool shift = false, bool alt = false)
        {
            this.commandId = commandId;
            this.key = key;
            this.ctrl = ctrl;
            this.shift = shift;
            this.alt = alt;
        }

        public bool Matches(KeyDownEvent evt)
        {
            if (evt.keyCode != key) return false;
            if ((evt.ctrlKey || evt.commandKey) != ctrl) return false;
            if (evt.shiftKey != shift) return false;
            if (evt.altKey != alt) return false;
            return true;
        }

        public static string PrimaryModifierLabel
            => PrimaryModifierLabelFor(Application.platform);

        public static string PrimaryModifierLabelFor(RuntimePlatform platform)
            => platform == RuntimePlatform.OSXEditor ? "Command" : "Ctrl";

        public static string Format(KeyCode key, bool primary = false, bool shift = false,
            bool alt = false, RuntimePlatform? platform = null)
            => $"{(primary ? PrimaryModifierLabelFor(platform ?? Application.platform) + "+" : string.Empty)}" +
               $"{(shift ? "Shift+" : string.Empty)}{(alt ? "Alt+" : string.Empty)}{key}";

        public string DisplayString(RuntimePlatform? platform = null)
            => Format(key, ctrl, shift, alt, platform);
    }

    /// <summary>
    /// Holds the active shortcut bindings, persisted to <see cref="EditorPrefs"/> and edited by
    /// <see cref="UIDesignerShortcutSettingsWindow"/>.
    /// </summary>
    public static class UIDesignerShortcutRegistry
    {
        private const string PrefsKey = "NexUIDesigner.Shortcuts.v1";

        public static readonly IReadOnlyList<UIDesignerShortcut> Defaults = new List<UIDesignerShortcut>
        {
            new UIDesignerShortcut("selectAll", KeyCode.A, ctrl: true),
            new UIDesignerShortcut("clearSelection", KeyCode.Escape),
            new UIDesignerShortcut("deleteSelection", KeyCode.Delete),
            new UIDesignerShortcut("deleteSelection", KeyCode.Backspace),
            new UIDesignerShortcut("duplicateSelection", KeyCode.D, ctrl: true),
            new UIDesignerShortcut("copySelection", KeyCode.C, ctrl: true),
            new UIDesignerShortcut("pasteSelection", KeyCode.V, ctrl: true),
            new UIDesignerShortcut("group", KeyCode.G, ctrl: true),
            new UIDesignerShortcut("ungroup", KeyCode.G, ctrl: true, shift: true),

            new UIDesignerShortcut("moveLeft", KeyCode.LeftArrow),
            new UIDesignerShortcut("moveRight", KeyCode.RightArrow),
            new UIDesignerShortcut("moveUp", KeyCode.UpArrow),
            new UIDesignerShortcut("moveDown", KeyCode.DownArrow),
            new UIDesignerShortcut("moveLeftFast", KeyCode.LeftArrow, shift: true),
            new UIDesignerShortcut("moveRightFast", KeyCode.RightArrow, shift: true),
            new UIDesignerShortcut("moveUpFast", KeyCode.UpArrow, shift: true),
            new UIDesignerShortcut("moveDownFast", KeyCode.DownArrow, shift: true),

            new UIDesignerShortcut("bringForward", KeyCode.RightBracket, ctrl: true),
            new UIDesignerShortcut("sendBackward", KeyCode.LeftBracket, ctrl: true),
            new UIDesignerShortcut("bringToFront", KeyCode.RightBracket, ctrl: true, shift: true),
            new UIDesignerShortcut("sendToBack", KeyCode.LeftBracket, ctrl: true, shift: true),

            new UIDesignerShortcut("distributeHorizontal", KeyCode.H, alt: true),
            new UIDesignerShortcut("distributeVertical", KeyCode.V, alt: true),

            new UIDesignerShortcut("alignLeft", KeyCode.L, alt: true),
            new UIDesignerShortcut("alignCenterX", KeyCode.C, alt: true),
            new UIDesignerShortcut("alignRight", KeyCode.R, alt: true),
            new UIDesignerShortcut("alignTop", KeyCode.T, alt: true),
            new UIDesignerShortcut("alignCenterY", KeyCode.M, alt: true),
            new UIDesignerShortcut("alignBottom", KeyCode.B, alt: true),
        };

        private static List<UIDesignerShortcut> _current;

        public static List<UIDesignerShortcut> Current
        {
            get
            {
                if (_current == null) Load();
                return _current;
            }
        }

        public static void ResetToDefaults()
        {
            _current = new List<UIDesignerShortcut>(Defaults);
            Save();
        }

        public static void Load()
        {
            var json = EditorPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                _current = new List<UIDesignerShortcut>(Defaults);
                return;
            }

            try
            {
                var wrapper = JsonUtility.FromJson<Wrapper>(json);
                _current = wrapper != null && wrapper.items != null && wrapper.items.Count > 0
                    ? wrapper.items
                    : new List<UIDesignerShortcut>(Defaults);
            }
            catch
            {
                _current = new List<UIDesignerShortcut>(Defaults);
            }
        }

        public static void Save()
        {
            var wrapper = new Wrapper { items = _current ?? new List<UIDesignerShortcut>(Defaults) };
            EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(wrapper));
        }

        [Serializable]
        private sealed class Wrapper
        {
            public List<UIDesignerShortcut> items;
        }
    }
}

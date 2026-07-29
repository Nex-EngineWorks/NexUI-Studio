namespace emiteat.NexUI.Designer.Editor.Validation
{
    public enum DesignerValidationCategory
    {
        General,
        Identity,
        Backend,
        Asset,
        Hierarchy,
        Layout,
        Binding,
        Motion,
        Localization,
        Accessibility,
        Component
    }

    public enum DesignerValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// A single, actionable validation result. Carries enough context (severity, stable
    /// code, screen / element ids, a human message and a suggested fix) to be understood
    /// without reading source code.
    /// </summary>
    public sealed class DesignerValidationIssue
    {
        public DesignerValidationSeverity Severity;
        public string Code;
        public string ScreenId;
        public string ElementId;
        public string Message;
        public string Fix;
        public UnityEngine.Object Asset;
        public DesignerValidationCategory Category;
        public string Backend;
        public string Cause;
        public bool CanAutoFix;
        public bool IsSafeAutoFix;
        public bool RequiresUserAction;

        public DesignerValidationIssue(DesignerValidationSeverity severity, string code, string message, string fix,
            string screenId = null, string elementId = null)
        {
            Severity = severity;
            Code = code;
            Message = message;
            Fix = fix;
            ScreenId = screenId;
            ElementId = elementId;
            Cause = message;
            Category = CategoryFor(code);
            Backend = BackendFor(code);
            CanAutoFix = IsAutoFixable(code);
            IsSafeAutoFix = IsSafeFix(code);
            RequiresUserAction = !IsSafeAutoFix;
        }

        private static DesignerValidationCategory CategoryFor(string code)
        {
            code = code ?? string.Empty;
            if (code.Contains("id") || code.Contains("identity")) return DesignerValidationCategory.Identity;
            if (code.Contains("parent") || code.Contains("sibling") || code.Contains("cycle")) return DesignerValidationCategory.Hierarchy;
            if (code.Contains("size") || code.Contains("canvas") || code.Contains("anchor")) return DesignerValidationCategory.Layout;
            if (code.Contains("binding") || code.Contains("command") || code.Contains("state-key")) return DesignerValidationCategory.Binding;
            if (code.Contains("motion") || code.Contains("clip")) return DesignerValidationCategory.Motion;
            if (code.Contains("localization") || code.Contains("text-key")) return DesignerValidationCategory.Localization;
            if (code.Contains("accessibility") || code.Contains("contrast") || code.Contains("touch-target")) return DesignerValidationCategory.Accessibility;
            if (code.Contains("component")) return DesignerValidationCategory.Component;
            if (code.Contains("asset") || code.Contains("prefab") || code.Contains("uxml") || code.Contains("uss")) return DesignerValidationCategory.Asset;
            if (code.StartsWith("ugui-") || code.StartsWith("uitk-") || code.Contains("backend")) return DesignerValidationCategory.Backend;
            return DesignerValidationCategory.General;
        }

        private static string BackendFor(string code)
        {
            if ((code ?? string.Empty).StartsWith("ugui-")) return "uGUI";
            if ((code ?? string.Empty).StartsWith("uitk-")) return "UI Toolkit";
            return string.Empty;
        }

        private static bool IsAutoFixable(string code)
            => IsSafeFix(code) || code == "ugui-button-target-graphic-missing" || code == "motion-close-missing";

        private static bool IsSafeFix(string code)
            => code == "empty-element-id" || code == "duplicate-element-id" || code == "missing-parent" ||
               code == "self-parent" || code == "circular-parent" || code == "zero-size-element" ||
               code == "small-touch-target" || code == "outside-canvas" || code == "ugui-decorative-raycast" ||
               code == "ugui-invisible-canvasgroup-blocks-input";

        /// <summary>Compact one-line rendering, e.g. "[Error] duplicate-id (loginButton): ... → Fix: ...".</summary>
        public override string ToString()
        {
            var scope = string.IsNullOrEmpty(ElementId) ? Code : $"{Code} ({ElementId})";
            var text = $"[{Severity}] {scope}: {Message}";
            if (!string.IsNullOrEmpty(Fix)) text += $"  →  Fix: {Fix}";
            return text;
        }
    }
}

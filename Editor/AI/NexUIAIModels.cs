using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.AI
{
    [Serializable]
    public sealed class NexUIAIActionPlan
    {
        public string message;
        public List<NexUIAIAction> actions = new List<NexUIAIAction>();

        public bool HasDestructiveActions
            => actions != null && actions.Exists(action => action != null && action.type == NexUIAIActionTypes.Delete);
    }

    [Serializable]
    public sealed class NexUIAIAction
    {
        public string type;
        public string targetId;
        public string elementId;
        public string elementType;
        public string parentId;
        public string property;
        public string value;
        public bool hasRect;
        public float x;
        public float y;
        public float width;
        public float height;
    }

    public static class NexUIAIActionTypes
    {
        public const string Create = "create";
        public const string Set = "set";
        public const string SetRect = "set_rect";
        public const string Reparent = "reparent";
        public const string AddClass = "add_class";
        public const string RemoveClass = "remove_class";
        public const string Select = "select";
        public const string Delete = "delete";
    }

    public sealed class NexUIAIPlanValidation
    {
        public readonly List<string> Errors = new List<string>();
        public bool IsValid => Errors.Count == 0;
    }

    [Serializable]
    public sealed class NexUIAIChatMessage
    {
        public string role;
        [TextArea(2, 12)] public string text;

        public NexUIAIChatMessage() { }
        public NexUIAIChatMessage(string role, string text)
        {
            this.role = role;
            this.text = text;
        }
    }

    public sealed class NexUIAIProviderRequest
    {
        public string ApiKey;
        public string Model;
        public string Instructions;
        public string Input;
    }

    public interface INexUIAIProvider
    {
        string DisplayName { get; }
        UniTask<string> CompleteAsync(NexUIAIProviderRequest request);
    }

    public static class NexUIAIPlanParser
    {
        public static bool TryParse(string text, out NexUIAIActionPlan plan, out string error)
        {
            plan = null;
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "AI returned an empty response.";
                return false;
            }

            var json = ExtractJson(text.Trim());
            try
            {
                plan = JsonUtility.FromJson<NexUIAIActionPlan>(json);
            }
            catch (Exception ex)
            {
                error = "AI response was not a valid NexUI action plan: " + ex.Message;
                return false;
            }

            if (plan == null)
            {
                error = "AI response did not contain an action plan.";
                return false;
            }

            if (plan.actions == null) plan.actions = new List<NexUIAIAction>();
            if (string.IsNullOrWhiteSpace(plan.message)) plan.message = "Plan ready.";
            return true;
        }

        private static string ExtractJson(string text)
        {
            if (text.StartsWith("```", StringComparison.Ordinal))
            {
                var firstLine = text.IndexOf('\n');
                var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
                if (firstLine >= 0 && lastFence > firstLine)
                    text = text.Substring(firstLine + 1, lastFence - firstLine - 1).Trim();
            }

            var firstBrace = text.IndexOf('{');
            var lastBrace = text.LastIndexOf('}');
            return firstBrace >= 0 && lastBrace > firstBrace
                ? text.Substring(firstBrace, lastBrace - firstBrace + 1)
                : text;
        }
    }
}

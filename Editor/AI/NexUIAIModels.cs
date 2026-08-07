using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.AI
{
    [Flags]
    public enum NexUIAICapability
    {
        None = 0,
        ReadContext = 1 << 0,
        Selection = 1 << 1,
        Content = 1 << 2,
        Layout = 1 << 3,
        VisualStyle = 1 << 4,
        Binding = 1 << 5,
        Hierarchy = 1 << 6,
        CreateElements = 1 << 7,
        DeleteElements = 1 << 8,
        Motion = 1 << 9,
        Components = 1 << 10,
        AssetCreation = 1 << 11,
        All = ReadContext | Selection | Content | Layout | VisualStyle | Binding | Hierarchy |
              CreateElements | DeleteElements | Motion | Components | AssetCreation
    }

    public enum NexUIAITargetScope
    {
        SelectedElements,
        SelectedSubtree,
        CurrentScreen
    }

    public enum NexUIAIScopePreset
    {
        InspectOnly,
        SelectedSafe,
        ScreenDesign,
        FullDesigner,
        Custom
    }

    [Serializable]
    public sealed class NexUIAIScopePolicy
    {
        public NexUIAIScopePreset preset = NexUIAIScopePreset.SelectedSafe;
        public NexUIAITargetScope targetScope = NexUIAITargetScope.SelectedSubtree;
        public NexUIAICapability capabilities = SafeDesignCapabilities;
        public bool allowDestructiveActions;

        public const NexUIAICapability SafeDesignCapabilities = NexUIAICapability.ReadContext |
            NexUIAICapability.Selection | NexUIAICapability.Content | NexUIAICapability.Layout |
            NexUIAICapability.VisualStyle | NexUIAICapability.Binding | NexUIAICapability.Hierarchy |
            NexUIAICapability.CreateElements | NexUIAICapability.Motion | NexUIAICapability.Components;

        public bool Allows(NexUIAICapability capability) => (capabilities & capability) == capability;

        public static NexUIAIScopePolicy ForPreset(NexUIAIScopePreset preset)
        {
            switch (preset)
            {
                case NexUIAIScopePreset.InspectOnly:
                    return new NexUIAIScopePolicy { preset = preset, targetScope = NexUIAITargetScope.CurrentScreen, capabilities = NexUIAICapability.ReadContext };
                case NexUIAIScopePreset.ScreenDesign:
                    return new NexUIAIScopePolicy { preset = preset, targetScope = NexUIAITargetScope.CurrentScreen, capabilities = SafeDesignCapabilities };
                case NexUIAIScopePreset.FullDesigner:
                    return new NexUIAIScopePolicy { preset = preset, targetScope = NexUIAITargetScope.CurrentScreen, capabilities = NexUIAICapability.All, allowDestructiveActions = true };
                case NexUIAIScopePreset.Custom:
                    return new NexUIAIScopePolicy { preset = preset };
                default:
                    return new NexUIAIScopePolicy { preset = NexUIAIScopePreset.SelectedSafe };
            }
        }
    }

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
        public string componentId;
        public string componentType;
        public string preset;
        public float duration;
        public float delay;
        public float distance;
        public float startAlpha;
        public float startScale;
        public float overshoot;
        public float staggerInterval;
        public bool includeChildren;
        public bool reverseOrder;
        public string clipName;
        public string assignTo;
        public bool loop;
        public int fps;
        public List<NexUIAIMotionTrack> motionTracks = new List<NexUIAIMotionTrack>();
    }

    [Serializable]
    public sealed class NexUIAIMotionTrack
    {
        public string targetId;
        public string property;
        public List<NexUIAIMotionKeyframe> keyframes = new List<NexUIAIMotionKeyframe>();
    }

    [Serializable]
    public sealed class NexUIAIMotionKeyframe
    {
        public float time;
        public string value;
        public string easing;
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
        public const string SetMotion = "set_motion";
        public const string ApplyTransition = "apply_transition";
        public const string CreateMotionClip = "create_motion_clip";
        public const string InstantiateComponent = "instantiate_component";
        public const string AttachComponent = "attach_component";
        public const string DetachComponent = "detach_component";
        public const string SetComponentVariant = "set_component_variant";
        public const string SetComponentProperty = "set_component_property";
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
        public string Endpoint;
    }

    public interface INexUIAIProvider
    {
        string DisplayName { get; }
        Task<string> CompleteAsync(NexUIAIProviderRequest request);
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

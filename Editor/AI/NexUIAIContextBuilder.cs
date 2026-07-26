using System;
using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.AgentHandoff;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.AI
{
    public static class NexUIAIContextBuilder
    {
        public const string Instructions =
            "You are NexUI Designer's in-editor UI assistant. Work only with the supplied Designer metadata. " +
            "Never propose C# execution, shell commands, arbitrary file writes, package changes, or actions outside the schema. " +
            "Return exactly one JSON object and no markdown. Schema: " +
            "{\"message\":\"short explanation in the user's language\",\"actions\":[" +
            "{\"type\":\"create|set|set_rect|reparent|add_class|remove_class|select|delete\"," +
            "\"targetId\":\"existing id\",\"elementId\":\"new id\",\"elementType\":\"registered type\"," +
            "\"parentId\":\"parent id or empty for root\",\"property\":\"whitelisted property\",\"value\":\"string value\"," +
            "\"hasRect\":true,\"x\":0,\"y\":0,\"width\":100,\"height\":40}]}. " +
            "For create, elementId and elementType are required; set hasRect only when an explicit rect is intended. " +
            "For set_rect, targetId and positive width/height are required. " +
            "For reparent, empty parentId means canvas root. For delete, use only when explicitly requested. " +
            "Allowed set properties: displayName,text,tint,textColor,fontSize,locked,hiddenInDesigner,anchorPreset,shape," +
            "previewValue,previewItemCount,clipChildren,accessibilityLabel,binding.textKey,binding.valueKey," +
            "binding.visibilityKey,binding.classKey,binding.commandKey,binding.interactableKey,autoLayout.enabled," +
            "autoLayout.direction,autoLayout.spacing,autoLayout.paddingLeft,autoLayout.paddingTop," +
            "autoLayout.paddingRight,autoLayout.paddingBottom. " +
            "Use #RRGGBB or #RRGGBBAA colors and invariant decimal numbers. Prefer a small plan of at most 32 actions. " +
            "If the request is ambiguous or cannot be represented safely, explain it and return an empty actions array.";

        public static string Build(NexUIDesignerContext context, bool includeProjectManifest)
        {
            var snapshot = new NexUIAIContextSnapshot
            {
                screenId = context?.CurrentScreen != null ? context.CurrentScreen.ScreenId : string.Empty,
                backend = context != null ? context.Backend.ToString() : string.Empty,
                resolutionWidth = context != null ? context.Resolution.x : 0,
                resolutionHeight = context != null ? context.Resolution.y : 0,
                project = includeProjectManifest ? AgentHandoffService.Collect() : null
            };

            if (context == null) return JsonUtility.ToJson(snapshot, true);

            foreach (var selected in context.SelectedElements)
                if (selected != null) snapshot.selectedElementIds.Add(selected.elementId);

            if (context.Metadata != null)
            {
                snapshot.metadataScreenId = context.Metadata.screenId;
                foreach (var element in context.Metadata.elements)
                {
                    if (element == null) continue;
                    snapshot.elements.Add(Element(element));
                }
            }

            foreach (var issue in context.ValidationIssues)
                if (issue != null) snapshot.validation.Add(issue.ToString());

            return JsonUtility.ToJson(snapshot, true);
        }

        private static NexUIAIElementSnapshot Element(DesignerElementMetadata element)
        {
            var snapshot = new NexUIAIElementSnapshot
            {
                elementId = element.elementId,
                parentId = element.parentId,
                displayName = element.displayName,
                elementType = element.elementType,
                x = element.rect.x,
                y = element.rect.y,
                width = element.rect.width,
                height = element.rect.height,
                text = element.text,
                tint = "#" + ColorUtility.ToHtmlStringRGBA(element.tint),
                textColor = "#" + ColorUtility.ToHtmlStringRGBA(element.textColor),
                fontSize = element.fontSize,
                locked = element.locked,
                hiddenInDesigner = element.hiddenInDesigner,
                anchorPreset = element.anchorPreset.ToString(),
                shape = element.shape.ToString(),
                accessibilityLabel = element.accessibilityLabel,
                binding = new NexUIAIBindingSnapshot
                {
                    textKey = element.binding?.textKey,
                    valueKey = element.binding?.valueKey,
                    visibilityKey = element.binding?.visibilityKey,
                    classKey = element.binding?.classKey,
                    commandKey = element.binding?.commandKey,
                    interactableKey = element.binding?.interactableKey
                }
            };
            if (element.classes != null) snapshot.classes.AddRange(element.classes);
            return snapshot;
        }
    }

    [Serializable]
    public sealed class NexUIAIContextSnapshot
    {
        public string screenId;
        public string metadataScreenId;
        public string backend;
        public int resolutionWidth;
        public int resolutionHeight;
        public List<string> selectedElementIds = new List<string>();
        public List<NexUIAIElementSnapshot> elements = new List<NexUIAIElementSnapshot>();
        public List<string> validation = new List<string>();
        public DesignerAgentHandoffMetadata project;
    }

    [Serializable]
    public sealed class NexUIAIElementSnapshot
    {
        public string elementId;
        public string parentId;
        public string displayName;
        public string elementType;
        public float x;
        public float y;
        public float width;
        public float height;
        public string text;
        public string tint;
        public string textColor;
        public int fontSize;
        public bool locked;
        public bool hiddenInDesigner;
        public string anchorPreset;
        public string shape;
        public string accessibilityLabel;
        public List<string> classes = new List<string>();
        public NexUIAIBindingSnapshot binding;
    }

    [Serializable]
    public sealed class NexUIAIBindingSnapshot
    {
        public string textKey;
        public string valueKey;
        public string visibilityKey;
        public string classKey;
        public string commandKey;
        public string interactableKey;
    }
}

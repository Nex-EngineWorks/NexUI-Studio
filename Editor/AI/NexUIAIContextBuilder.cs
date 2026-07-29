using System;
using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.AgentHandoff;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Components.Definitions;
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

        public static string BuildInstructions(NexUIAIScopePolicy policy)
        {
            policy ??= NexUIAIScopePolicy.ForPreset(NexUIAIScopePreset.SelectedSafe);
            return "You are NexUI Designer's approval-first UI agent. Never emit code, commands, file operations, or fields outside this schema. " +
                   "Return exactly one JSON object without markdown. The user-selected policy is authoritative; do not request or emit disallowed actions. " +
                   "Schema: {\"message\":\"short explanation in the user's language\",\"actions\":[{\"type\":\"create|set|set_rect|reparent|add_class|remove_class|select|delete|set_motion|apply_transition|create_motion_clip|instantiate_component|attach_component|detach_component|set_component_variant|set_component_property\",\"targetId\":\"existing id\",\"elementId\":\"new id\",\"elementType\":\"registered type\",\"parentId\":\"parent id\",\"property\":\"property name\",\"value\":\"value\",\"componentId\":\"library id\",\"componentType\":\"assembly-qualified MonoBehaviour\",\"preset\":\"Fade|SlideLeft|SlideRight|SlideUp|SlideDown|ScalePop|Modal|Dropdown|Tooltip|Toast|StaggerList\",\"hasRect\":true,\"x\":0,\"y\":0,\"width\":100,\"height\":40,\"duration\":0.25,\"delay\":0,\"distance\":48,\"startAlpha\":0,\"startScale\":0.9,\"overshoot\":1.04,\"staggerInterval\":0.05,\"includeChildren\":false,\"reverseOrder\":false,\"clipName\":\"SafeAssetName\",\"assignTo\":\"entry|exit|preview\",\"loop\":false,\"fps\":30,\"motionTracks\":[{\"targetId\":\"existing id\",\"property\":\"AnchoredPosition|LocalPosition|LocalRotationZ|LocalScale|SizeDelta|CanvasGroupAlpha\",\"keyframes\":[{\"time\":0,\"value\":\"0,0\",\"easing\":\"Linear\"}]}]}]}. " +
                   "set supports content, binding, autoLayout, layoutStyle, visualStyle and typography properties listed in the snapshot. " +
                   "set_motion supports motionId, initialVariant, animateVariant, exitVariant, hoverVariant, pressedVariant and focusVariant. " +
                   "apply_transition creates a reviewable preset pair and assigns screen entry/exit clips. create_motion_clip creates an exact validated keyframe asset; use x,y for Vector2, x,y,z for scale, and a decimal for rotation/alpha. instantiate_component uses an available componentId. " +
                   "set_component_variant selects a declared variant axis; set_component_property edits a declared exposed property. attach_component only accepts a resolvable MonoBehaviour type and never configures arbitrary serialized fields. " +
                   "Use colors as #RRGGBB or #RRGGBBAA and invariant decimal numbers. Keep plans under 64 actions. " +
                   "If ambiguous or impossible, explain and return an empty actions array.\nPOLICY:\n" + JsonUtility.ToJson(policy, true);
        }

        public static string Build(NexUIDesignerContext context, bool includeProjectManifest)
            => Build(context, includeProjectManifest, NexUIAIScopePolicy.ForPreset(NexUIAIScopePreset.ScreenDesign));

        public static string Build(NexUIDesignerContext context, bool includeProjectManifest, NexUIAIScopePolicy policy)
        {
            policy ??= NexUIAIScopePolicy.ForPreset(NexUIAIScopePreset.SelectedSafe);
            var snapshot = new NexUIAIContextSnapshot
            {
                screenId = context?.CurrentScreen != null ? context.CurrentScreen.ScreenId : string.Empty,
                backend = context != null ? context.Backend.ToString() : string.Empty,
                resolutionWidth = context != null ? context.Resolution.x : 0,
                resolutionHeight = context != null ? context.Resolution.y : 0,
                project = includeProjectManifest ? AgentHandoffService.Collect() : null,
                policy = policy,
                setProperties = new List<string>(NexUIAIActionService.SettableProperties)
            };
            snapshot.setProperties.RemoveAll(property => !policy.Allows(NexUIAIActionService.RequiredCapability(
                new NexUIAIAction { type = NexUIAIActionTypes.Set, property = property })));
            snapshot.setProperties.Sort(System.StringComparer.OrdinalIgnoreCase);

            if (context == null) return JsonUtility.ToJson(snapshot, true);

            foreach (var selected in context.SelectedElements)
                if (selected != null) snapshot.selectedElementIds.Add(selected.elementId);

            if (context.Metadata != null)
            {
                snapshot.metadataScreenId = context.Metadata.screenId;
                var visibleIds = VisibleElementIds(context, policy);
                foreach (var element in context.Metadata.elements)
                {
                    if (element == null || !visibleIds.Contains(element.elementId)) continue;
                    snapshot.elements.Add(Element(element));
                }
            }

            if (policy.Allows(NexUIAICapability.Components))
            {
                foreach (var recipe in DesignerBuiltInComponentCatalog.All)
                {
                    if (recipe == null) continue;
                    snapshot.availableComponents.Add(new NexUIAIComponentSnapshot
                    {
                        componentId = recipe.Id,
                        displayName = recipe.DisplayName,
                        folder = recipe.CategoryPath,
                        description = string.Empty,
                        variants = VariantNames(recipe.Definition),
                        exposedProperties = ExposedPropertyNames(recipe.Definition)
                    });
                }
                foreach (var definition in DesignerComponentLibrary.All)
                {
                    if (definition == null) continue;
                    snapshot.availableComponents.Add(new NexUIAIComponentSnapshot
                    {
                        componentId = definition.componentId,
                        displayName = definition.EffectiveDisplayName,
                        folder = DesignerComponentLibrary.EffectiveFolder(definition),
                        description = definition.description,
                        variants = VariantNames(definition),
                        exposedProperties = ExposedPropertyNames(definition)
                    });
                }
            }
            if (policy.Allows(NexUIAICapability.CreateElements))
                foreach (var descriptor in DesignerComponentRegistry.All)
                {
                    if (descriptor == null) continue;
                    snapshot.availableElementTypes.Add(new NexUIAIElementTypeSnapshot
                    {
                        typeId = descriptor.TypeId,
                        displayName = descriptor.DisplayName,
                        family = descriptor.Family.ToString(),
                        description = descriptor.Description,
                        canHaveChildren = descriptor.CanHaveChildren
                    });
                }

            if (policy.targetScope == NexUIAITargetScope.CurrentScreen)
                foreach (var issue in context.ValidationIssues)
                    if (issue != null) snapshot.validation.Add(issue.ToString());

            var json = JsonUtility.ToJson(snapshot, true);
            return includeProjectManifest ? json : RemoveObjectProperty(json, "project");
        }

        private static string RemoveObjectProperty(string json, string propertyName)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(propertyName)) return json;
            var marker = "\"" + propertyName + "\":";
            var property = json.IndexOf(marker, StringComparison.Ordinal);
            if (property < 0) return json;
            var valueStart = property + marker.Length;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart])) valueStart++;
            if (valueStart >= json.Length || json[valueStart] != '{') return json;

            var depth = 0;
            var inString = false;
            var escaped = false;
            var valueEnd = valueStart;
            for (; valueEnd < json.Length; valueEnd++)
            {
                var c = json[valueEnd];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') { inString = true; continue; }
                if (c == '{') depth++;
                else if (c == '}' && --depth == 0) { valueEnd++; break; }
            }

            var removeStart = property;
            while (removeStart > 0 && char.IsWhiteSpace(json[removeStart - 1])) removeStart--;
            if (removeStart > 0 && json[removeStart - 1] == ',') removeStart--;
            else
            {
                while (valueEnd < json.Length && char.IsWhiteSpace(json[valueEnd])) valueEnd++;
                if (valueEnd < json.Length && json[valueEnd] == ',') valueEnd++;
            }
            return json.Remove(removeStart, valueEnd - removeStart);
        }

        private static List<string> VariantNames(DesignerComponentDefinitionAsset definition)
        {
            var result = new List<string>();
            if (definition?.variantProperties == null) return result;
            foreach (var item in definition.variantProperties)
                if (item != null && !string.IsNullOrEmpty(item.propertyName))
                    result.Add(item.propertyName + "=" + string.Join("|", item.options ?? new List<string>()));
            return result;
        }

        private static List<string> ExposedPropertyNames(DesignerComponentDefinitionAsset definition)
        {
            var result = new List<string>();
            if (definition?.exposedProperties == null) return result;
            foreach (var item in definition.exposedProperties)
                if (item != null && !string.IsNullOrEmpty(item.propertyName)) result.Add(item.propertyName);
            return result;
        }

        private static HashSet<string> VisibleElementIds(NexUIDesignerContext context, NexUIAIScopePolicy policy)
        {
            var result = new HashSet<string>(System.StringComparer.Ordinal);
            if (context?.Metadata == null) return result;
            if (policy.targetScope == NexUIAITargetScope.CurrentScreen)
            {
                foreach (var element in context.Metadata.elements)
                    if (element != null) result.Add(element.elementId);
                return result;
            }
            foreach (var selected in context.SelectedElements)
            {
                if (selected == null) continue;
                result.Add(selected.elementId);
                if (policy.targetScope == NexUIAITargetScope.SelectedSubtree)
                    foreach (var child in DesignerHierarchyUtility.GetDescendants(context.Metadata, selected))
                        if (child != null) result.Add(child.elementId);
            }
            return result;
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
                motionId = element.motion?.motionId,
                animateVariant = element.motion?.animateVariant,
                componentId = element.componentInstance?.definitionId,
                attachedComponentCount = element.attachedComponents?.Count ?? 0,
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
        public NexUIAIScopePolicy policy;
        public List<string> setProperties = new List<string>();
        public List<NexUIAIComponentSnapshot> availableComponents = new List<NexUIAIComponentSnapshot>();
        public List<NexUIAIElementTypeSnapshot> availableElementTypes = new List<NexUIAIElementTypeSnapshot>();
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
        public string motionId;
        public string animateVariant;
        public string componentId;
        public int attachedComponentCount;
    }

    [Serializable]
    public sealed class NexUIAIComponentSnapshot
    {
        public string componentId;
        public string displayName;
        public string folder;
        public string description;
        public List<string> variants = new List<string>();
        public List<string> exposedProperties = new List<string>();
    }

    [Serializable]
    public sealed class NexUIAIElementTypeSnapshot
    {
        public string typeId;
        public string displayName;
        public string family;
        public string description;
        public bool canHaveChildren;
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

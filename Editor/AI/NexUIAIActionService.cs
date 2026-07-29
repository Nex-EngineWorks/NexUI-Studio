using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Components.Definitions;
using emiteat.NexUI.Designer.Editor.Inspectors;
using emiteat.NexUI.Designer.Editor.Productivity;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.MotionClip;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.AI
{
    /// <summary>
    /// Safety boundary between an AI response and Designer state. Only this small, validated
    /// command vocabulary can mutate an open screen; model output is never executed as code.
    /// </summary>
    public static class NexUIAIActionService
    {
        public const int MaxActions = 64;

        private static readonly Regex SafeId = new Regex("^[A-Za-z_][A-Za-z0-9_.-]*$", RegexOptions.Compiled);
        private static readonly HashSet<string> ActionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NexUIAIActionTypes.Create, NexUIAIActionTypes.Set, NexUIAIActionTypes.SetRect,
            NexUIAIActionTypes.Reparent, NexUIAIActionTypes.AddClass, NexUIAIActionTypes.RemoveClass,
            NexUIAIActionTypes.Select, NexUIAIActionTypes.Delete, NexUIAIActionTypes.SetMotion,
            NexUIAIActionTypes.ApplyTransition, NexUIAIActionTypes.CreateMotionClip, NexUIAIActionTypes.InstantiateComponent,
            NexUIAIActionTypes.AttachComponent, NexUIAIActionTypes.DetachComponent,
            NexUIAIActionTypes.SetComponentVariant, NexUIAIActionTypes.SetComponentProperty
        };

        private static readonly HashSet<string> SetProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "displayName", "text", "tint", "textColor", "fontSize", "locked", "hiddenInDesigner",
            "anchorPreset", "shape", "previewValue", "previewItemCount", "clipChildren", "accessibilityLabel",
            "binding.textKey", "binding.valueKey", "binding.visibilityKey", "binding.classKey",
            "binding.commandKey", "binding.interactableKey", "autoLayout.enabled", "autoLayout.direction",
            "autoLayout.spacing", "autoLayout.paddingLeft", "autoLayout.paddingTop",
            "autoLayout.paddingRight", "autoLayout.paddingBottom",
            "runtimeVisible", "parentSlotId", "accessibilityRole",
            "layoutStyle.minWidth", "layoutStyle.minHeight", "layoutStyle.maxWidth", "layoutStyle.maxHeight",
            "layoutStyle.pivotX", "layoutStyle.pivotY", "layoutStyle.rotation", "layoutStyle.scaleX", "layoutStyle.scaleY",
            "layoutStyle.marginLeft", "layoutStyle.marginTop", "layoutStyle.marginRight", "layoutStyle.marginBottom",
            "layoutStyle.aspectRatio", "layoutStyle.wrap", "layoutStyle.align", "layoutStyle.justify", "layoutStyle.overflow",
            "visualStyle.backgroundColor", "visualStyle.opacity", "visualStyle.borderWidth", "visualStyle.borderColor",
            "visualStyle.cornerRadius", "visualStyle.dropShadow", "visualStyle.shadowColor", "visualStyle.shadowOffsetX",
            "visualStyle.shadowOffsetY", "visualStyle.shadowBlur", "visualStyle.innerShadow", "visualStyle.outlineWidth",
            "visualStyle.outlineColor", "visualStyle.blur", "visualStyle.mask", "visualStyle.imageSlice",
            "visualStyle.imageFit", "visualStyle.crop", "typography.fontFamily", "typography.fontWeight",
            "typography.fontStyle", "typography.fontSize", "typography.autoSize", "typography.minFontSize",
            "typography.maxFontSize", "typography.alignment", "typography.wrapping", "typography.overflow",
            "typography.ellipsis", "typography.lineHeight", "typography.letterSpacing", "typography.paragraphSpacing",
            "typography.richText", "typography.localizationKey", "typography.rightToLeft", "typography.color",
            "typography.textShadow", "typography.shadowColor", "typography.shadowOffsetX", "typography.shadowOffsetY",
            "typography.outlineWidth", "typography.outlineColor"
        };

        public static IReadOnlyCollection<string> SettableProperties => SetProperties;

        public static NexUIAIPlanValidation Validate(NexUIDesignerContext context, NexUIAIActionPlan plan)
            => Validate(context, plan, NexUIAIScopePolicy.ForPreset(NexUIAIScopePreset.FullDesigner));

        public static NexUIAIPlanValidation Validate(NexUIDesignerContext context, NexUIAIActionPlan plan, NexUIAIScopePolicy policy)
        {
            policy ??= NexUIAIScopePolicy.ForPreset(NexUIAIScopePreset.SelectedSafe);
            var result = new NexUIAIPlanValidation();
            if (context == null || context.IsDisposed || context.Metadata == null)
            {
                result.Errors.Add("Open a NexUI screen with Designer metadata before applying a plan.");
                return result;
            }

            if (plan == null)
            {
                result.Errors.Add("The response did not contain an action plan.");
                return result;
            }

            var actions = plan.actions ?? new List<NexUIAIAction>();
            if (actions.Count > MaxActions)
            {
                result.Errors.Add($"The plan has {actions.Count} actions; the safety limit is {MaxActions}.");
                return result;
            }

            var known = new Dictionary<string, string>(StringComparer.Ordinal);
            var parents = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var element in context.Metadata.elements)
            {
                if (element == null || string.IsNullOrEmpty(element.elementId)) continue;
                known[element.elementId] = element.elementType;
                parents[element.elementId] = element.parentId ?? string.Empty;
            }
            var scopedTargets = BuildScopedTargets(context, policy);
            var createdInPlan = new HashSet<string>(StringComparer.Ordinal);
            var plannedComponents = new Dictionary<string, DesignerComponentDefinitionAsset>(StringComparer.Ordinal);

            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                var label = $"Action {i + 1}";
                if (action == null)
                {
                    result.Errors.Add(label + " is empty.");
                    continue;
                }

                action.type = (action.type ?? string.Empty).Trim().ToLowerInvariant();
                if (!ActionTypes.Contains(action.type))
                {
                    result.Errors.Add($"{label} uses unsupported type '{action.type}'.");
                    continue;
                }

                ValidatePermission(action, label, policy, result);

                if (action.type == NexUIAIActionTypes.Create)
                {
                    if (policy.targetScope != NexUIAITargetScope.CurrentScreen && string.IsNullOrWhiteSpace(action.parentId))
                        result.Errors.Add($"{label} must create under an element inside the selected AI target scope.");
                    if (!string.IsNullOrEmpty(action.parentId) && !scopedTargets.Contains(action.parentId) && !createdInPlan.Contains(action.parentId))
                        result.Errors.Add($"{label} creates outside the selected AI target scope.");
                    ValidateCreate(action, label, known, parents, result);
                    if (!result.Errors.Exists(error => error.StartsWith(label, StringComparison.Ordinal)))
                    {
                        createdInPlan.Add(action.elementId);
                        scopedTargets.Add(action.elementId);
                    }
                    continue;
                }

                if (action.type == NexUIAIActionTypes.InstantiateComponent)
                {
                    if (policy.targetScope != NexUIAITargetScope.CurrentScreen && string.IsNullOrWhiteSpace(action.parentId))
                        result.Errors.Add($"{label} must place the component under an element inside the selected AI target scope.");
                    ValidateInstantiateComponent(action, label, known, scopedTargets, createdInPlan, plannedComponents, result);
                    continue;
                }

                if (action.type == NexUIAIActionTypes.CreateMotionClip)
                {
                    ValidateMotionClip(action, label, known, scopedTargets, result);
                    continue;
                }

                var target = (action.targetId ?? string.Empty).Trim();
                if (!known.ContainsKey(target))
                {
                    result.Errors.Add($"{label} targets unknown element '{target}'.");
                    continue;
                }
                if (!scopedTargets.Contains(target) && !createdInPlan.Contains(target))
                {
                    result.Errors.Add($"{label} targets '{target}' outside the selected AI target scope.");
                    continue;
                }

                switch (action.type)
                {
                    case NexUIAIActionTypes.Set:
                        ValidateSet(action, label, result);
                        break;
                    case NexUIAIActionTypes.SetRect:
                        ValidateRect(action, label, result);
                        break;
                    case NexUIAIActionTypes.Reparent:
                        if (policy.targetScope != NexUIAITargetScope.CurrentScreen && string.IsNullOrWhiteSpace(action.parentId))
                            result.Errors.Add(label + " cannot move an element to root outside the selected AI target scope.");
                        else if (!string.IsNullOrWhiteSpace(action.parentId) && !scopedTargets.Contains(action.parentId) && !createdInPlan.Contains(action.parentId))
                            result.Errors.Add($"{label} reparents outside the selected AI target scope.");
                        ValidateReparent(action, label, target, known, parents, result);
                        break;
                    case NexUIAIActionTypes.AddClass:
                    case NexUIAIActionTypes.RemoveClass:
                        if (string.IsNullOrWhiteSpace(action.value))
                            result.Errors.Add(label + " requires a non-empty class name in value.");
                        break;
                    case NexUIAIActionTypes.Delete:
                    {
                        var deleteTarget = context.Metadata.Find(target);
                        if (deleteTarget != null)
                            foreach (var descendant in DesignerHierarchyUtility.GetDescendants(context.Metadata, deleteTarget))
                                if (descendant != null && !scopedTargets.Contains(descendant.elementId))
                                    result.Errors.Add($"{label} would delete '{descendant.elementId}' outside the selected AI target scope.");
                        RemoveKnownSubtree(target, known, parents);
                        break;
                    }
                    case NexUIAIActionTypes.SetMotion:
                        ValidateMotion(action, label, result);
                        break;
                    case NexUIAIActionTypes.ApplyTransition:
                        if (action.includeChildren && policy.targetScope == NexUIAITargetScope.SelectedElements)
                            result.Errors.Add(label + " cannot include children outside the Selected Elements scope.");
                        ValidateTransition(action, label, result);
                        break;
                    case NexUIAIActionTypes.AttachComponent:
                        ValidateAttachedComponent(action, label, false, result);
                        break;
                    case NexUIAIActionTypes.DetachComponent:
                        ValidateAttachedComponent(action, label, true, result);
                        break;
                    case NexUIAIActionTypes.SetComponentVariant:
                    case NexUIAIActionTypes.SetComponentProperty:
                        ValidateComponentEdit(context, action, label, plannedComponents, result);
                        break;
                }
            }

            return result;
        }

        public static IReadOnlyList<string> Describe(NexUIAIActionPlan plan)
        {
            var descriptions = new List<string>();
            if (plan?.actions == null) return descriptions;
            foreach (var a in plan.actions)
            {
                if (a == null) continue;
                switch ((a.type ?? string.Empty).ToLowerInvariant())
                {
                    case NexUIAIActionTypes.Create:
                        descriptions.Add($"Create {a.elementType} '{a.elementId}'" + (string.IsNullOrEmpty(a.parentId) ? string.Empty : $" under '{a.parentId}'"));
                        break;
                    case NexUIAIActionTypes.Set:
                        descriptions.Add($"Set {a.targetId}.{a.property} = {a.value}");
                        break;
                    case NexUIAIActionTypes.SetRect:
                        descriptions.Add($"Move/resize '{a.targetId}' to ({a.x:0.#}, {a.y:0.#}, {a.width:0.#}, {a.height:0.#})");
                        break;
                    case NexUIAIActionTypes.Reparent:
                        descriptions.Add(string.IsNullOrEmpty(a.parentId) ? $"Move '{a.targetId}' to root" : $"Move '{a.targetId}' under '{a.parentId}'");
                        break;
                    case NexUIAIActionTypes.AddClass:
                        descriptions.Add($"Add class '{a.value}' to '{a.targetId}'");
                        break;
                    case NexUIAIActionTypes.RemoveClass:
                        descriptions.Add($"Remove class '{a.value}' from '{a.targetId}'");
                        break;
                    case NexUIAIActionTypes.Select:
                        descriptions.Add($"Select '{a.targetId}'");
                        break;
                    case NexUIAIActionTypes.Delete:
                        descriptions.Add($"Delete '{a.targetId}' and its children");
                        break;
                    case NexUIAIActionTypes.SetMotion:
                        descriptions.Add($"Set motion {a.targetId}.{a.property} = {a.value}");
                        break;
                    case NexUIAIActionTypes.ApplyTransition:
                        descriptions.Add($"Create and apply {a.preset} transition for '{a.targetId}' ({a.duration:0.##}s)");
                        break;
                    case NexUIAIActionTypes.CreateMotionClip:
                        descriptions.Add($"Create motion clip '{a.clipName}' with {a.motionTracks?.Count ?? 0} track(s) and assign it to {a.assignTo}");
                        break;
                    case NexUIAIActionTypes.InstantiateComponent:
                        descriptions.Add($"Place component '{a.componentId}' as '{a.elementId}'");
                        break;
                    case NexUIAIActionTypes.AttachComponent:
                        descriptions.Add($"Attach MonoBehaviour '{a.componentType}' to '{a.targetId}'");
                        break;
                    case NexUIAIActionTypes.DetachComponent:
                        descriptions.Add($"Detach MonoBehaviour '{a.componentType}' from '{a.targetId}'");
                        break;
                    case NexUIAIActionTypes.SetComponentVariant:
                        descriptions.Add($"Set component variant {a.targetId}.{a.property} = {a.value}");
                        break;
                    case NexUIAIActionTypes.SetComponentProperty:
                        descriptions.Add($"Set exposed component property {a.targetId}.{a.property} = {a.value}");
                        break;
                    default:
                        descriptions.Add("Unsupported action");
                        break;
                }
            }
            return descriptions;
        }

        public static void Apply(NexUIDesignerContext context, NexUIAIActionPlan plan)
            => Apply(context, plan, NexUIAIScopePolicy.ForPreset(NexUIAIScopePreset.FullDesigner));

        public static void Apply(NexUIDesignerContext context, NexUIAIActionPlan plan, NexUIAIScopePolicy policy)
        {
            var validation = Validate(context, plan, policy);
            if (!validation.IsValid)
                throw new InvalidOperationException(string.Join("\n", validation.Errors));

            NexUIDesignerUndo.Group("Apply NexUI AI Plan", () =>
            {
                foreach (var action in plan.actions)
                    ApplyAction(context, action);
            });
            context.RebuildPreview();
            context.Validate();
        }

        private static HashSet<string> BuildScopedTargets(NexUIDesignerContext context, NexUIAIScopePolicy policy)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
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

        private static void ValidatePermission(NexUIAIAction action, string label, NexUIAIScopePolicy policy, NexUIAIPlanValidation result)
        {
            var required = RequiredCapability(action);
            if (required != NexUIAICapability.None && !policy.Allows(required))
                result.Errors.Add($"{label} requires AI permission '{required}'.");
            if (action.type == NexUIAIActionTypes.Delete && !policy.allowDestructiveActions)
                result.Errors.Add(label + " is destructive and destructive AI actions are disabled.");
        }

        public static NexUIAICapability RequiredCapability(NexUIAIAction action)
        {
            if (action == null) return NexUIAICapability.None;
            switch ((action.type ?? string.Empty).Trim().ToLowerInvariant())
            {
                case NexUIAIActionTypes.Create: return NexUIAICapability.CreateElements;
                case NexUIAIActionTypes.SetRect: return NexUIAICapability.Layout;
                case NexUIAIActionTypes.Reparent: return NexUIAICapability.Hierarchy;
                case NexUIAIActionTypes.AddClass:
                case NexUIAIActionTypes.RemoveClass: return NexUIAICapability.VisualStyle;
                case NexUIAIActionTypes.Select: return NexUIAICapability.Selection;
                case NexUIAIActionTypes.Delete: return NexUIAICapability.DeleteElements;
                case NexUIAIActionTypes.SetMotion: return NexUIAICapability.Motion;
                case NexUIAIActionTypes.ApplyTransition: return NexUIAICapability.Motion | NexUIAICapability.AssetCreation;
                case NexUIAIActionTypes.CreateMotionClip: return NexUIAICapability.Motion | NexUIAICapability.AssetCreation;
                case NexUIAIActionTypes.InstantiateComponent: return NexUIAICapability.Components | NexUIAICapability.CreateElements;
                case NexUIAIActionTypes.AttachComponent:
                case NexUIAIActionTypes.DetachComponent:
                case NexUIAIActionTypes.SetComponentVariant:
                case NexUIAIActionTypes.SetComponentProperty: return NexUIAICapability.Components;
                case NexUIAIActionTypes.Set: return PropertyCapability(action.property);
                default: return NexUIAICapability.None;
            }
        }

        private static NexUIAICapability PropertyCapability(string property)
        {
            property = (property ?? string.Empty).ToLowerInvariant();
            if (property.StartsWith("binding.")) return NexUIAICapability.Binding;
            if (property.StartsWith("autolayout.") || property.StartsWith("layoutstyle.") || property == "anchorpreset" || property == "parentslotid") return NexUIAICapability.Layout;
            if (property.StartsWith("visualstyle.") || property.StartsWith("typography.") || property == "tint" || property == "textcolor" || property == "shape") return NexUIAICapability.VisualStyle;
            return NexUIAICapability.Content;
        }

        private static void ValidateInstantiateComponent(NexUIAIAction action, string label,
            IDictionary<string, string> known, ISet<string> scopedTargets, ISet<string> createdInPlan,
            IDictionary<string, DesignerComponentDefinitionAsset> plannedComponents, NexUIAIPlanValidation result)
        {
            action.componentId = (action.componentId ?? string.Empty).Trim();
            action.elementId = (action.elementId ?? string.Empty).Trim();
            action.parentId = (action.parentId ?? string.Empty).Trim();
            var definition = DesignerComponentLibrary.Resolve(string.Empty, action.componentId);
            if (definition == null)
                result.Errors.Add($"{label} uses unknown componentId '{action.componentId}'.");
            if (!SafeId.IsMatch(action.elementId) || known.ContainsKey(action.elementId))
                result.Errors.Add($"{label} needs a unique, safe elementId.");
            if (!string.IsNullOrEmpty(action.parentId))
            {
                if (!known.TryGetValue(action.parentId, out var parentType))
                    result.Errors.Add($"{label} uses unknown parent '{action.parentId}'.");
                else if (!DesignerComponentRegistry.CanHaveChildren(parentType))
                    result.Errors.Add($"{label} parent '{action.parentId}' cannot contain children.");
            }
            if (!string.IsNullOrEmpty(action.parentId) && !scopedTargets.Contains(action.parentId) && !createdInPlan.Contains(action.parentId))
                result.Errors.Add($"{label} creates outside the selected AI target scope.");
            if (action.hasRect) ValidateRect(action, label, result);
            if (!result.Errors.Exists(error => error.StartsWith(label, StringComparison.Ordinal)))
            {
                known[action.elementId] = DesignerComponentService.InstanceTypeId;
                scopedTargets.Add(action.elementId);
                createdInPlan.Add(action.elementId);
                plannedComponents[action.elementId] = definition;
            }
        }

        private static void ValidateMotion(NexUIAIAction action, string label, NexUIAIPlanValidation result)
        {
            switch ((action.property ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "motionid": case "initialvariant": case "animatevariant": case "exitvariant":
                case "hovervariant": case "pressedvariant": case "focusvariant": return;
                default: result.Errors.Add($"{label} cannot set motion property '{action.property}'."); return;
            }
        }

        private static void ValidateTransition(NexUIAIAction action, string label, NexUIAIPlanValidation result)
        {
            if (action.duration == 0f) action.duration = .25f;
            if (action.startScale == 0f) action.startScale = .9f;
            if (action.overshoot == 0f) action.overshoot = 1.04f;
            if (!Enum.TryParse(action.preset, true, out DesignerTransitionPreset _))
                result.Errors.Add($"{label} uses unknown transition preset '{action.preset}'.");
            if (!IsFinite(action.duration) || action.duration < .05f || action.duration > 10f)
                result.Errors.Add(label + " duration must be between 0.05 and 10 seconds.");
            if (!IsFinite(action.delay) || action.delay < 0f || action.delay > 10f)
                result.Errors.Add(label + " delay must be between 0 and 10 seconds.");
        }

        private static void ValidateMotionClip(NexUIAIAction action, string label,
            IReadOnlyDictionary<string, string> known, ISet<string> scopedTargets, NexUIAIPlanValidation result)
        {
            action.clipName = (action.clipName ?? string.Empty).Trim();
            action.assignTo = string.IsNullOrWhiteSpace(action.assignTo) ? "preview" : action.assignTo.Trim().ToLowerInvariant();
            if (!SafeId.IsMatch(action.clipName))
                result.Errors.Add(label + " clipName must be a safe asset name beginning with a letter or underscore.");
            if (action.assignTo != "entry" && action.assignTo != "exit" && action.assignTo != "preview")
                result.Errors.Add(label + " assignTo must be entry, exit, or preview.");
            if (!IsFinite(action.duration) || action.duration < .05f || action.duration > 30f)
                result.Errors.Add(label + " duration must be between 0.05 and 30 seconds.");
            if (action.fps == 0) action.fps = 30;
            if (action.fps < 1 || action.fps > 240)
                result.Errors.Add(label + " fps must be between 1 and 240.");

            var tracks = action.motionTracks ?? new List<NexUIAIMotionTrack>();
            if (tracks.Count == 0 || tracks.Count > 64)
                result.Errors.Add(label + " requires between 1 and 64 motion tracks.");
            var totalKeys = 0;
            for (var trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
            {
                var track = tracks[trackIndex];
                var trackLabel = $"{label} track {trackIndex + 1}";
                if (track == null)
                {
                    result.Errors.Add(trackLabel + " is empty.");
                    continue;
                }
                track.targetId = (track.targetId ?? string.Empty).Trim();
                if (!known.ContainsKey(track.targetId))
                    result.Errors.Add($"{trackLabel} targets unknown element '{track.targetId}'.");
                else if (!scopedTargets.Contains(track.targetId))
                    result.Errors.Add($"{trackLabel} targets '{track.targetId}' outside the selected AI target scope.");
                if (!Enum.TryParse(track.property, true, out UIMotionClipPropertyType propertyType))
                {
                    result.Errors.Add($"{trackLabel} uses unknown property '{track.property}'.");
                    continue;
                }
                var keys = track.keyframes ?? new List<NexUIAIMotionKeyframe>();
                if (keys.Count < 2 || keys.Count > 64)
                    result.Errors.Add(trackLabel + " requires between 2 and 64 keyframes.");
                totalKeys += keys.Count;
                var previousTime = -1f;
                for (var keyIndex = 0; keyIndex < keys.Count; keyIndex++)
                {
                    var key = keys[keyIndex];
                    var keyLabel = $"{trackLabel} keyframe {keyIndex + 1}";
                    if (key == null)
                    {
                        result.Errors.Add(keyLabel + " is empty.");
                        continue;
                    }
                    if (!IsFinite(key.time) || key.time < 0f || key.time > action.duration || key.time <= previousTime)
                        result.Errors.Add(keyLabel + " time must be finite, strictly increasing, and inside the clip duration.");
                    previousTime = key.time;
                    if (!string.IsNullOrWhiteSpace(key.easing) && !Enum.TryParse(key.easing, true, out UIMotionEasing _))
                        result.Errors.Add($"{keyLabel} uses unknown easing '{key.easing}'.");
                    try { ParseMotionValue(propertyType, key.value); }
                    catch (Exception exception) { result.Errors.Add($"{keyLabel} has invalid value: {exception.Message}"); }
                }
            }
            if (totalKeys > 512) result.Errors.Add(label + " exceeds the 512-keyframe safety limit.");
        }

        private static void ValidateAttachedComponent(NexUIAIAction action, string label, bool removing, NexUIAIPlanValidation result)
        {
            var type = DesignerMonoBehaviourTypes.Resolve(action.componentType);
            if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters)
                result.Errors.Add($"{label} references unavailable MonoBehaviour '{action.componentType}'.");
            if (removing && string.IsNullOrWhiteSpace(action.componentType))
                result.Errors.Add(label + " requires componentType.");
        }

        private static void ValidateComponentEdit(NexUIDesignerContext context, NexUIAIAction action, string label,
            IReadOnlyDictionary<string, DesignerComponentDefinitionAsset> plannedComponents, NexUIAIPlanValidation result)
        {
            action.property = (action.property ?? string.Empty).Trim();
            var element = context.Metadata.Find(action.targetId);
            var reference = element?.componentInstance;
            var definition = DesignerComponentLibrary.Resolve(reference?.definitionGuid, reference?.definitionId);
            DesignerComponentDefinitionAsset plannedDefinition = null;
            var planned = element == null && plannedComponents.TryGetValue(action.targetId, out plannedDefinition);
            if (planned) definition = plannedDefinition;
            if ((!planned && (reference == null || !reference.IsInstance)) || definition == null)
            {
                result.Errors.Add(label + " requires an existing reusable component instance.");
                return;
            }
            if (action.type == NexUIAIActionTypes.SetComponentVariant)
            {
                var variant = definition.variantProperties?.Find(item => item != null && string.Equals(item.propertyName, action.property, StringComparison.Ordinal));
                if (variant == null) result.Errors.Add($"{label} uses unknown component variant '{action.property}'.");
                else if (variant.type == DesignerComponentVariantPropertyType.Boolean && !bool.TryParse(action.value, out _))
                    result.Errors.Add(label + " boolean variant expects true or false.");
                else if (variant.options != null && variant.options.Count > 0 && !variant.options.Exists(option => string.Equals(option, action.value, StringComparison.Ordinal)))
                    result.Errors.Add($"{label} value '{action.value}' is not an option for variant '{action.property}'.");
                return;
            }
            var exposed = definition.exposedProperties?.Find(item => item != null && string.Equals(item.propertyName, action.property, StringComparison.Ordinal));
            if (exposed == null)
            {
                result.Errors.Add($"{label} uses unknown exposed component property '{action.property}'.");
                return;
            }
            try { ParseComponentValue(exposed.defaultValue?.type ?? DesignerPropertyValueType.String, action.value); }
            catch (Exception exception) { result.Errors.Add($"{label} has invalid component value: {exception.Message}"); }
        }

        private static DesignerPropertyValue ParseComponentValue(DesignerPropertyValueType type, string value)
        {
            var parsed = new DesignerPropertyValue { type = type };
            switch (type)
            {
                case DesignerPropertyValueType.Float: parsed.floatValue = ParseFloat(value); break;
                case DesignerPropertyValueType.Integer: parsed.intValue = ParseInt(value, -1000000, 1000000); break;
                case DesignerPropertyValueType.Boolean: parsed.boolValue = ParseBool(value); break;
                case DesignerPropertyValueType.Color: parsed.colorValue = ParseColor(value); break;
                case DesignerPropertyValueType.Vector2:
                {
                    var parts = (value ?? string.Empty).Split(',');
                    if (parts.Length != 2) throw new FormatException("expected x,y");
                    parsed.vector2Value = new Vector2(ParseFloat(parts[0]), ParseFloat(parts[1]));
                    break;
                }
                case DesignerPropertyValueType.AssetReference:
                    throw new FormatException("asset reference overrides require manual asset selection");
                case DesignerPropertyValueType.None:
                    parsed.type = DesignerPropertyValueType.String;
                    parsed.stringValue = value ?? string.Empty;
                    break;
                default: parsed.stringValue = value ?? string.Empty; break;
            }
            return parsed;
        }

        private static void ValidateCreate(NexUIAIAction action, string label,
            IDictionary<string, string> known, IDictionary<string, string> parents, NexUIAIPlanValidation result)
        {
            action.elementId = (action.elementId ?? string.Empty).Trim();
            action.elementType = (action.elementType ?? string.Empty).Trim();
            action.parentId = (action.parentId ?? string.Empty).Trim();

            if (!SafeId.IsMatch(action.elementId))
                result.Errors.Add($"{label} has invalid elementId '{action.elementId}'. Use letters, numbers, '_', '-' or '.'.");
            else if (known.ContainsKey(action.elementId))
                result.Errors.Add($"{label} duplicates elementId '{action.elementId}'.");

            if (!DesignerComponentRegistry.IsRegistered(action.elementType))
                result.Errors.Add($"{label} uses unknown elementType '{action.elementType}'.");

            if (!string.IsNullOrEmpty(action.parentId))
            {
                if (!known.TryGetValue(action.parentId, out var parentType))
                    result.Errors.Add($"{label} uses unknown parent '{action.parentId}'.");
                else if (!DesignerComponentRegistry.CanHaveChildren(parentType))
                    result.Errors.Add($"{label} parent '{action.parentId}' cannot contain children.");
            }

            if (action.hasRect) ValidateRect(action, label, result);
            if (result.Errors.Exists(e => e.StartsWith(label, StringComparison.Ordinal))) return;
            known[action.elementId] = action.elementType;
            parents[action.elementId] = action.parentId;
        }

        private static void ValidateSet(NexUIAIAction action, string label, NexUIAIPlanValidation result)
        {
            action.property = (action.property ?? string.Empty).Trim();
            if (!SetProperties.Contains(action.property))
            {
                result.Errors.Add($"{label} cannot set property '{action.property}'.");
                return;
            }

            try
            {
                ValidatePropertyValue(action.property, action.value);
            }
            catch (Exception exception)
            {
                result.Errors.Add($"{label} has invalid value for {action.property}: {exception.Message}");
            }
        }

        private static void ValidateRect(NexUIAIAction action, string label, NexUIAIPlanValidation result)
        {
            if (!IsFinite(action.x) || !IsFinite(action.y) || !IsFinite(action.width) || !IsFinite(action.height))
                result.Errors.Add(label + " rect contains a non-finite number.");
            else if (action.width <= 0f || action.height <= 0f)
                result.Errors.Add(label + " rect width and height must be greater than zero.");
        }

        private static void ValidateReparent(NexUIAIAction action, string label, string target,
            IReadOnlyDictionary<string, string> known, IDictionary<string, string> parents, NexUIAIPlanValidation result)
        {
            action.parentId = (action.parentId ?? string.Empty).Trim();
            if (target == action.parentId)
            {
                result.Errors.Add(label + " cannot parent an element to itself.");
                return;
            }

            if (!string.IsNullOrEmpty(action.parentId))
            {
                if (!known.TryGetValue(action.parentId, out var parentType))
                {
                    result.Errors.Add($"{label} uses unknown parent '{action.parentId}'.");
                    return;
                }
                if (!DesignerComponentRegistry.CanHaveChildren(parentType))
                {
                    result.Errors.Add($"{label} parent '{action.parentId}' cannot contain children.");
                    return;
                }
            }

            var cursor = action.parentId;
            while (!string.IsNullOrEmpty(cursor) && parents.TryGetValue(cursor, out var next))
            {
                if (cursor == target)
                {
                    result.Errors.Add(label + " would create a hierarchy cycle.");
                    return;
                }
                cursor = next;
            }
            parents[target] = action.parentId;
        }

        private static void RemoveKnownSubtree(string target, IDictionary<string, string> known,
            IDictionary<string, string> parents)
        {
            var pending = new Queue<string>();
            pending.Enqueue(target);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                foreach (var candidate in new List<string>(parents.Keys))
                    if (parents.TryGetValue(candidate, out var parent) && parent == current)
                        pending.Enqueue(candidate);
                known.Remove(current);
                parents.Remove(current);
            }
        }

        private static void ApplyAction(NexUIDesignerContext context, NexUIAIAction action)
        {
            switch (action.type)
            {
                case NexUIAIActionTypes.Create:
                {
                    var created = context.CreateMetadataElement(action.elementType);
                    context.RenameElementId(created, action.elementId);
                    if (action.hasRect)
                        context.UpdateElementRect(created, new Rect(action.x, action.y, action.width, action.height));
                    if (!string.IsNullOrEmpty(action.parentId))
                        context.ReparentElement(created, context.Metadata.Find(action.parentId));
                    break;
                }
                case NexUIAIActionTypes.Set:
                {
                    var target = context.Metadata.Find(action.targetId);
                    context.UpdateElement(target, element => SetProperty(element, action.property, action.value), "NexUI AI Set Property");
                    break;
                }
                case NexUIAIActionTypes.SetRect:
                    context.UpdateElementRect(context.Metadata.Find(action.targetId), new Rect(action.x, action.y, action.width, action.height));
                    break;
                case NexUIAIActionTypes.Reparent:
                    context.ReparentElement(context.Metadata.Find(action.targetId),
                        string.IsNullOrEmpty(action.parentId) ? null : context.Metadata.Find(action.parentId));
                    break;
                case NexUIAIActionTypes.AddClass:
                    context.UpdateElement(context.Metadata.Find(action.targetId), element =>
                    {
                        element.classes ??= new List<string>();
                        if (!element.classes.Contains(action.value)) element.classes.Add(action.value);
                    }, "NexUI AI Add Class");
                    break;
                case NexUIAIActionTypes.RemoveClass:
                    context.UpdateElement(context.Metadata.Find(action.targetId), element => element.classes?.Remove(action.value), "NexUI AI Remove Class");
                    break;
                case NexUIAIActionTypes.Select:
                    context.SelectMetadata(action.targetId);
                    break;
                case NexUIAIActionTypes.Delete:
                    context.SelectMetadata(action.targetId);
                    context.DeleteSelectedMetadata(true);
                    break;
                case NexUIAIActionTypes.SetMotion:
                    context.UpdateElement(context.Metadata.Find(action.targetId), element =>
                    {
                        element.motion ??= new DesignerMotionMetadata();
                        SetMotionProperty(element.motion, action.property, action.value);
                    }, "NexUI AI Set Motion");
                    break;
                case NexUIAIActionTypes.ApplyTransition:
                    ApplyTransition(context, action);
                    break;
                case NexUIAIActionTypes.CreateMotionClip:
                    ApplyMotionClip(context, action);
                    break;
                case NexUIAIActionTypes.InstantiateComponent:
                {
                    var definition = DesignerComponentLibrary.Resolve(string.Empty, action.componentId);
                    var position = action.hasRect ? new Vector2(action.x, action.y) : new Vector2(64f, 64f);
                    var placed = DesignerComponentService.Instantiate(context.Metadata, definition, position, action.parentId);
                    if (!placed.Success) throw new InvalidOperationException(placed.Message);
                    if (!string.IsNullOrEmpty(action.elementId) && placed.Element.elementId != action.elementId)
                        context.RenameElementId(placed.Element, action.elementId);
                    if (action.hasRect)
                        context.UpdateElementRect(placed.Element, new Rect(action.x, action.y, action.width, action.height));
                    break;
                }
                case NexUIAIActionTypes.AttachComponent:
                    context.UpdateElement(context.Metadata.Find(action.targetId), element =>
                    {
                        element.attachedComponents ??= new List<DesignerAttachedComponentMetadata>();
                        var type = DesignerMonoBehaviourTypes.Resolve(action.componentType);
                        var identity = DesignerMonoBehaviourTypes.Identity(type);
                        if (!element.attachedComponents.Exists(item => DesignerMonoBehaviourTypes.Resolve(item?.typeName) == type))
                            element.attachedComponents.Add(new DesignerAttachedComponentMetadata { typeName = identity });
                    }, "NexUI AI Attach Component");
                    break;
                case NexUIAIActionTypes.DetachComponent:
                    context.UpdateElement(context.Metadata.Find(action.targetId), element =>
                    {
                        var type = DesignerMonoBehaviourTypes.Resolve(action.componentType);
                        element.attachedComponents?.RemoveAll(item => DesignerMonoBehaviourTypes.Resolve(item?.typeName) == type);
                    }, "NexUI AI Detach Component");
                    break;
                case NexUIAIActionTypes.SetComponentVariant:
                    context.UpdateElement(context.Metadata.Find(action.targetId), element =>
                    {
                        element.componentInstance.SetVariantSelection(action.property, action.value ?? string.Empty);
                    }, "NexUI AI Set Component Variant");
                    break;
                case NexUIAIActionTypes.SetComponentProperty:
                {
                    var element = context.Metadata.Find(action.targetId);
                    var reference = element.componentInstance;
                    var definition = DesignerComponentLibrary.Resolve(reference.definitionGuid, reference.definitionId);
                    var exposed = definition.exposedProperties.Find(item => item != null && item.propertyName == action.property);
                    var item = new DesignerComponentPropertyOverride
                    {
                        exposedPropertyName = exposed.propertyName,
                        targetElementId = exposed.targetElementId,
                        propertyId = exposed.propertyId,
                        value = ParseComponentValue(exposed.defaultValue?.type ?? DesignerPropertyValueType.String, action.value)
                    };
                    if (!DesignerComponentService.SetOverride(context.Metadata, element, item))
                        throw new InvalidOperationException("Could not set the component override.");
                    break;
                }
            }
        }

        private static void SetMotionProperty(DesignerMotionMetadata motion, string property, string value)
        {
            switch ((property ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "motionid": motion.motionId = value ?? string.Empty; break;
                case "initialvariant": motion.initialVariant = value ?? string.Empty; break;
                case "animatevariant": motion.animateVariant = value ?? string.Empty; break;
                case "exitvariant": motion.exitVariant = value ?? string.Empty; break;
                case "hovervariant": motion.hoverVariant = value ?? string.Empty; break;
                case "pressedvariant": motion.pressedVariant = value ?? string.Empty; break;
                case "focusvariant": motion.focusVariant = value ?? string.Empty; break;
                default: throw new FormatException("unsupported motion property");
            }
        }

        private static void ApplyTransition(NexUIDesignerContext context, NexUIAIAction action)
        {
            Enum.TryParse(action.preset, true, out DesignerTransitionPreset preset);
            var settings = new DesignerTransitionSettings
            {
                Duration = action.duration,
                Delay = action.delay,
                Distance = action.distance == 0f ? 48f : action.distance,
                StartAlpha = action.startAlpha,
                StartScale = action.startScale == 0f ? .9f : action.startScale,
                Overshoot = action.overshoot == 0f ? 1.04f : action.overshoot,
                IncludeChildren = action.includeChildren,
                StaggerInterval = action.staggerInterval,
                ReverseOrder = action.reverseOrder
            };
            var metadataPath = AssetDatabase.GetAssetPath(context.Metadata);
            var folder = string.IsNullOrEmpty(metadataPath) ? "Assets" : System.IO.Path.GetDirectoryName(metadataPath)?.Replace('\\', '/');
            folder = string.IsNullOrEmpty(folder) ? "Assets" : folder;
            EnsureAssetFolder(folder + "/Motions");
            var baseName = string.IsNullOrEmpty(context.Metadata.screenId) ? "Screen" : context.Metadata.screenId;
            var pair = DesignerTransitionPresetService.CreateAssetPair(context.Metadata, action.targetId, preset,
                $"{folder}/Motions/{baseName}.{action.targetId}.{preset}", settings);
            context.UpdateScreenMotion(screen => { screen.entryClip = pair.Open; screen.exitClip = pair.Close; }, "NexUI AI Apply Transition");
            AssetDatabase.SaveAssets();
            context.SetActiveMotionClip(pair.Open, 0f);
        }

        private static void ApplyMotionClip(NexUIDesignerContext context, NexUIAIAction action)
        {
            var clip = ScriptableObject.CreateInstance<UIMotionClip>();
            clip.name = action.clipName;
            clip.clipName = action.clipName;
            clip.duration = action.duration;
            clip.loop = action.loop;
            clip.fps = action.fps == 0 ? 30 : action.fps;
            clip.tracks = (action.motionTracks ?? new List<NexUIAIMotionTrack>()).ConvertAll(track =>
            {
                Enum.TryParse(track.property, true, out UIMotionClipPropertyType propertyType);
                return new UIMotionClipTrack
                {
                    targetElementId = track.targetId,
                    propertyTracks = new[]
                    {
                        new UIMotionClipPropertyTrack
                        {
                            propertyType = propertyType,
                            keyframes = (track.keyframes ?? new List<NexUIAIMotionKeyframe>()).ConvertAll(key =>
                            {
                                var easing = UIMotionEasing.Linear;
                                if (!string.IsNullOrWhiteSpace(key.easing)) Enum.TryParse(key.easing, true, out easing);
                                return new UIMotionClipKeyframe(key.time, ParseMotionValue(propertyType, key.value), easing);
                            }).ToArray()
                        }
                    }
                };
            }).ToArray();

            var metadataPath = AssetDatabase.GetAssetPath(context.Metadata);
            var folder = string.IsNullOrEmpty(metadataPath) ? "Assets" : System.IO.Path.GetDirectoryName(metadataPath)?.Replace('\\', '/');
            folder = string.IsNullOrEmpty(folder) ? "Assets" : folder;
            EnsureAssetFolder(folder + "/Motions");
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/Motions/{action.clipName}.asset");
            AssetDatabase.CreateAsset(clip, path);
            Undo.RegisterCreatedObjectUndo(clip, "Create NexUI AI Motion Clip");
            if (action.assignTo == "entry")
                context.UpdateScreenMotion(screen => screen.entryClip = clip, "Assign NexUI AI Entry Motion");
            else if (action.assignTo == "exit")
                context.UpdateScreenMotion(screen => screen.exitClip = clip, "Assign NexUI AI Exit Motion");
            context.SetActiveMotionClip(clip, 0f);
            AssetDatabase.SaveAssetIfDirty(clip);
        }

        private static UIMotionClipValue ParseMotionValue(UIMotionClipPropertyType propertyType, string value)
        {
            var parts = (value ?? string.Empty).Split(',');
            switch (propertyType)
            {
                case UIMotionClipPropertyType.AnchoredPosition:
                case UIMotionClipPropertyType.LocalPosition:
                case UIMotionClipPropertyType.SizeDelta:
                    if (parts.Length != 2) throw new FormatException("expected x,y");
                    return UIMotionClipValue.FromVector2(new Vector2(ParseFloat(parts[0]), ParseFloat(parts[1])));
                case UIMotionClipPropertyType.LocalScale:
                    if (parts.Length != 3) throw new FormatException("expected x,y,z");
                    return UIMotionClipValue.FromVector3(new Vector3(ParseFloat(parts[0]), ParseFloat(parts[1]), ParseFloat(parts[2])));
                case UIMotionClipPropertyType.LocalRotationZ:
                case UIMotionClipPropertyType.CanvasGroupAlpha:
                    if (parts.Length != 1) throw new FormatException("expected one decimal value");
                    var number = ParseFloat(parts[0]);
                    if (propertyType == UIMotionClipPropertyType.CanvasGroupAlpha && (number < 0f || number > 1f))
                        throw new FormatException("alpha must be between 0 and 1");
                    return UIMotionClipValue.Float(number);
                default:
                    throw new FormatException("unsupported motion property");
            }
        }

        private static void EnsureAssetFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void ValidatePropertyValue(string property, string value)
        {
            var dummy = new DesignerElementMetadata();
            SetProperty(dummy, property, value);
        }

        private static void SetProperty(DesignerElementMetadata element, string property, string value)
        {
            element.binding ??= new DesignerBindingMetadata();
            element.autoLayout ??= new DesignerAutoLayoutMetadata();
            element.layoutStyle ??= new DesignerLayoutStyleMetadata();
            element.visualStyle ??= new DesignerVisualStyleMetadata();
            element.typography ??= new DesignerTypographyMetadata();
            switch (property.ToLowerInvariant())
            {
                case "displayname": element.displayName = value ?? string.Empty; break;
                case "text": element.text = value ?? string.Empty; break;
                case "tint": element.tint = ParseColor(value); break;
                case "textcolor": element.textColor = ParseColor(value); break;
                case "fontsize": element.fontSize = ParseInt(value, 1, 512); break;
                case "locked": element.locked = ParseBool(value); break;
                case "hiddenindesigner": element.hiddenInDesigner = ParseBool(value); break;
                case "anchorpreset": element.anchorPreset = ParseEnum<DesignerAnchorPreset>(value); break;
                case "shape": element.shape = ParseEnum<DesignerElementShape>(value); break;
                case "previewvalue": element.previewValue = ParseFloat(value); break;
                case "previewitemcount": element.previewItemCount = ParseInt(value, 0, 1000); break;
                case "clipchildren": element.clipChildren = ParseBool(value); break;
                case "accessibilitylabel": element.accessibilityLabel = value ?? string.Empty; break;
                case "binding.textkey": element.binding.textKey = value ?? string.Empty; break;
                case "binding.valuekey": element.binding.valueKey = value ?? string.Empty; break;
                case "binding.visibilitykey": element.binding.visibilityKey = value ?? string.Empty; break;
                case "binding.classkey": element.binding.classKey = value ?? string.Empty; break;
                case "binding.commandkey": element.binding.commandKey = value ?? string.Empty; break;
                case "binding.interactablekey": element.binding.interactableKey = value ?? string.Empty; break;
                case "autolayout.enabled": element.autoLayout.enabled = ParseBool(value); break;
                case "autolayout.direction": element.autoLayout.direction = ParseEnum<DesignerAutoLayoutDirection>(value); break;
                case "autolayout.spacing": element.autoLayout.spacing = ParseFloat(value); break;
                case "autolayout.paddingleft": element.autoLayout.paddingLeft = ParseFloat(value); break;
                case "autolayout.paddingtop": element.autoLayout.paddingTop = ParseFloat(value); break;
                case "autolayout.paddingright": element.autoLayout.paddingRight = ParseFloat(value); break;
                case "autolayout.paddingbottom": element.autoLayout.paddingBottom = ParseFloat(value); break;
                case "runtimevisible": element.runtimeVisible = ParseBool(value); break;
                case "parentslotid": element.parentSlotId = value ?? string.Empty; break;
                case "accessibilityrole": element.accessibilityRole = ParseEnum<emiteat.NexUI.Accessibility.AccessibilityRole>(value); break;
                case "layoutstyle.minwidth": element.layoutStyle.hasOverrides = true; element.layoutStyle.minSize.x = ParseFloat(value); break;
                case "layoutstyle.minheight": element.layoutStyle.hasOverrides = true; element.layoutStyle.minSize.y = ParseFloat(value); break;
                case "layoutstyle.maxwidth": element.layoutStyle.hasOverrides = true; element.layoutStyle.maxSize.x = ParseFloat(value); break;
                case "layoutstyle.maxheight": element.layoutStyle.hasOverrides = true; element.layoutStyle.maxSize.y = ParseFloat(value); break;
                case "layoutstyle.pivotx": element.layoutStyle.hasOverrides = true; element.layoutStyle.pivot.x = ParseFloat(value); break;
                case "layoutstyle.pivoty": element.layoutStyle.hasOverrides = true; element.layoutStyle.pivot.y = ParseFloat(value); break;
                case "layoutstyle.rotation": element.layoutStyle.hasOverrides = true; element.layoutStyle.rotation = ParseFloat(value); break;
                case "layoutstyle.scalex": element.layoutStyle.hasOverrides = true; element.layoutStyle.scale.x = ParseFloat(value); break;
                case "layoutstyle.scaley": element.layoutStyle.hasOverrides = true; element.layoutStyle.scale.y = ParseFloat(value); break;
                case "layoutstyle.marginleft": element.layoutStyle.hasOverrides = true; element.layoutStyle.marginLeft = ParseFloat(value); break;
                case "layoutstyle.margintop": element.layoutStyle.hasOverrides = true; element.layoutStyle.marginTop = ParseFloat(value); break;
                case "layoutstyle.marginright": element.layoutStyle.hasOverrides = true; element.layoutStyle.marginRight = ParseFloat(value); break;
                case "layoutstyle.marginbottom": element.layoutStyle.hasOverrides = true; element.layoutStyle.marginBottom = ParseFloat(value); break;
                case "layoutstyle.aspectratio": element.layoutStyle.hasOverrides = true; element.layoutStyle.aspectRatio = ParseFloat(value); break;
                case "layoutstyle.wrap": element.layoutStyle.hasOverrides = true; element.layoutStyle.wrap = ParseEnum<DesignerLayoutWrap>(value); break;
                case "layoutstyle.align": element.layoutStyle.hasOverrides = true; element.layoutStyle.align = ParseEnum<DesignerLayoutAlignment>(value); break;
                case "layoutstyle.justify": element.layoutStyle.hasOverrides = true; element.layoutStyle.justify = ParseEnum<DesignerJustifyContent>(value); break;
                case "layoutstyle.overflow": element.layoutStyle.hasOverrides = true; element.layoutStyle.overflow = ParseEnum<DesignerOverflowMode>(value); break;
                case "visualstyle.backgroundcolor": element.visualStyle.hasOverrides = true; element.visualStyle.backgroundColor = ParseColor(value); break;
                case "visualstyle.opacity": element.visualStyle.hasOverrides = true; element.visualStyle.opacity = ParseRange(value, 0f, 1f); break;
                case "visualstyle.borderwidth": element.visualStyle.hasOverrides = true; element.visualStyle.borderWidth = ParseNonNegative(value); break;
                case "visualstyle.bordercolor": element.visualStyle.hasOverrides = true; element.visualStyle.borderColor = ParseColor(value); break;
                case "visualstyle.cornerradius": element.visualStyle.hasOverrides = true; element.visualStyle.cornerRadius = ParseNonNegative(value); break;
                case "visualstyle.dropshadow": element.visualStyle.hasOverrides = true; element.visualStyle.dropShadow = ParseBool(value); break;
                case "visualstyle.shadowcolor": element.visualStyle.hasOverrides = true; element.visualStyle.shadowColor = ParseColor(value); break;
                case "visualstyle.shadowoffsetx": element.visualStyle.hasOverrides = true; element.visualStyle.shadowOffset.x = ParseFloat(value); break;
                case "visualstyle.shadowoffsety": element.visualStyle.hasOverrides = true; element.visualStyle.shadowOffset.y = ParseFloat(value); break;
                case "visualstyle.shadowblur": element.visualStyle.hasOverrides = true; element.visualStyle.shadowBlur = ParseNonNegative(value); break;
                case "visualstyle.innershadow": element.visualStyle.hasOverrides = true; element.visualStyle.innerShadow = ParseBool(value); break;
                case "visualstyle.outlinewidth": element.visualStyle.hasOverrides = true; element.visualStyle.outlineWidth = ParseNonNegative(value); break;
                case "visualstyle.outlinecolor": element.visualStyle.hasOverrides = true; element.visualStyle.outlineColor = ParseColor(value); break;
                case "visualstyle.blur": element.visualStyle.hasOverrides = true; element.visualStyle.blur = ParseNonNegative(value); break;
                case "visualstyle.mask": element.visualStyle.hasOverrides = true; element.visualStyle.mask = ParseBool(value); break;
                case "visualstyle.imageslice": element.visualStyle.hasOverrides = true; element.visualStyle.imageSlice = ParseBool(value); break;
                case "visualstyle.imagefit": element.visualStyle.hasOverrides = true; element.visualStyle.imageFit = ParseEnum<DesignerImageFit>(value); break;
                case "visualstyle.crop": element.visualStyle.hasOverrides = true; element.visualStyle.crop = ParseBool(value); break;
                case "typography.fontfamily": element.typography.hasOverrides = true; element.typography.fontFamily = value ?? string.Empty; break;
                case "typography.fontweight": element.typography.hasOverrides = true; element.typography.fontWeight = ParseEnum<DesignerFontWeight>(value); break;
                case "typography.fontstyle": element.typography.hasOverrides = true; element.typography.fontStyle = ParseEnum<DesignerFontStyle>(value); break;
                case "typography.fontsize": element.typography.hasOverrides = true; element.typography.fontSize = ParseRange(value, 1f, 512f); break;
                case "typography.autosize": element.typography.hasOverrides = true; element.typography.autoSize = ParseBool(value); break;
                case "typography.minfontsize": element.typography.hasOverrides = true; element.typography.minFontSize = ParseRange(value, 1f, 512f); break;
                case "typography.maxfontsize": element.typography.hasOverrides = true; element.typography.maxFontSize = ParseRange(value, 1f, 512f); break;
                case "typography.alignment": element.typography.hasOverrides = true; element.typography.alignment = ParseEnum<DesignerTextAlignment>(value); break;
                case "typography.wrapping": element.typography.hasOverrides = true; element.typography.wrapping = ParseBool(value); break;
                case "typography.overflow": element.typography.hasOverrides = true; element.typography.overflow = ParseEnum<DesignerTextOverflow>(value); break;
                case "typography.ellipsis": element.typography.hasOverrides = true; element.typography.ellipsis = ParseBool(value); break;
                case "typography.lineheight": element.typography.hasOverrides = true; element.typography.lineHeight = ParseNonNegative(value); break;
                case "typography.letterspacing": element.typography.hasOverrides = true; element.typography.letterSpacing = ParseFloat(value); break;
                case "typography.paragraphspacing": element.typography.hasOverrides = true; element.typography.paragraphSpacing = ParseFloat(value); break;
                case "typography.richtext": element.typography.hasOverrides = true; element.typography.richText = ParseBool(value); break;
                case "typography.localizationkey": element.typography.hasOverrides = true; element.typography.localizationKey = value ?? string.Empty; break;
                case "typography.righttoleft": element.typography.hasOverrides = true; element.typography.rightToLeft = ParseBool(value); break;
                case "typography.color": element.typography.hasOverrides = true; element.typography.color = ParseColor(value); break;
                case "typography.textshadow": element.typography.hasOverrides = true; element.typography.textShadow = ParseBool(value); break;
                case "typography.shadowcolor": element.typography.hasOverrides = true; element.typography.shadowColor = ParseColor(value); break;
                case "typography.shadowoffsetx": element.typography.hasOverrides = true; element.typography.shadowOffset.x = ParseFloat(value); break;
                case "typography.shadowoffsety": element.typography.hasOverrides = true; element.typography.shadowOffset.y = ParseFloat(value); break;
                case "typography.outlinewidth": element.typography.hasOverrides = true; element.typography.outlineWidth = ParseNonNegative(value); break;
                case "typography.outlinecolor": element.typography.hasOverrides = true; element.typography.outlineColor = ParseColor(value); break;
                default: throw new FormatException("unsupported property");
            }
        }

        private static bool ParseBool(string value)
        {
            if (bool.TryParse(value, out var parsed)) return parsed;
            throw new FormatException("expected true or false");
        }

        private static int ParseInt(string value, int min, int max)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < min || parsed > max)
                throw new FormatException($"expected an integer from {min} to {max}");
            return parsed;
        }

        private static float ParseFloat(string value)
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || !IsFinite(parsed))
                throw new FormatException("expected a finite number using '.' as the decimal separator");
            return parsed;
        }

        private static float ParseRange(string value, float min, float max)
        {
            var parsed = ParseFloat(value);
            if (parsed < min || parsed > max) throw new FormatException($"expected a number from {min} to {max}");
            return parsed;
        }

        private static float ParseNonNegative(string value) => ParseRange(value, 0f, 100000f);

        private static T ParseEnum<T>(string value) where T : struct
        {
            if (Enum.TryParse(value, true, out T parsed) && Enum.IsDefined(typeof(T), parsed)) return parsed;
            throw new FormatException($"expected a valid {typeof(T).Name} value");
        }

        private static Color ParseColor(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && ColorUtility.TryParseHtmlString(value.Trim(), out var parsed)) return parsed;
            throw new FormatException("expected a hex color such as #2F80ED or #2F80EDFF");
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

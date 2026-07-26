using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Components;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.AI
{
    /// <summary>
    /// Safety boundary between an AI response and Designer state. Only this small, validated
    /// command vocabulary can mutate an open screen; model output is never executed as code.
    /// </summary>
    public static class NexUIAIActionService
    {
        public const int MaxActions = 32;

        private static readonly Regex SafeId = new Regex("^[A-Za-z_][A-Za-z0-9_.-]*$", RegexOptions.Compiled);
        private static readonly HashSet<string> ActionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NexUIAIActionTypes.Create, NexUIAIActionTypes.Set, NexUIAIActionTypes.SetRect,
            NexUIAIActionTypes.Reparent, NexUIAIActionTypes.AddClass, NexUIAIActionTypes.RemoveClass,
            NexUIAIActionTypes.Select, NexUIAIActionTypes.Delete
        };

        private static readonly HashSet<string> SetProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "displayName", "text", "tint", "textColor", "fontSize", "locked", "hiddenInDesigner",
            "anchorPreset", "shape", "previewValue", "previewItemCount", "clipChildren", "accessibilityLabel",
            "binding.textKey", "binding.valueKey", "binding.visibilityKey", "binding.classKey",
            "binding.commandKey", "binding.interactableKey", "autoLayout.enabled", "autoLayout.direction",
            "autoLayout.spacing", "autoLayout.paddingLeft", "autoLayout.paddingTop",
            "autoLayout.paddingRight", "autoLayout.paddingBottom"
        };

        public static NexUIAIPlanValidation Validate(NexUIDesignerContext context, NexUIAIActionPlan plan)
        {
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

                if (action.type == NexUIAIActionTypes.Create)
                {
                    ValidateCreate(action, label, known, parents, result);
                    continue;
                }

                var target = (action.targetId ?? string.Empty).Trim();
                if (!known.ContainsKey(target))
                {
                    result.Errors.Add($"{label} targets unknown element '{target}'.");
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
                        ValidateReparent(action, label, target, known, parents, result);
                        break;
                    case NexUIAIActionTypes.AddClass:
                    case NexUIAIActionTypes.RemoveClass:
                        if (string.IsNullOrWhiteSpace(action.value))
                            result.Errors.Add(label + " requires a non-empty class name in value.");
                        break;
                    case NexUIAIActionTypes.Delete:
                        RemoveKnownSubtree(target, known, parents);
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
                    default:
                        descriptions.Add("Unsupported action");
                        break;
                }
            }
            return descriptions;
        }

        public static void Apply(NexUIDesignerContext context, NexUIAIActionPlan plan)
        {
            var validation = Validate(context, plan);
            if (!validation.IsValid)
                throw new InvalidOperationException(string.Join("\n", validation.Errors));

            NexUIDesignerUndo.Group("Apply NexUI AI Plan", () =>
            {
                foreach (var action in plan.actions)
                    ApplyAction(context, action);
            });
            context.Validate();
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

            if (!Enum.TryParse(action.elementType, true, out DesignerElementType parsedType) ||
                !DesignerComponentRegistry.IsRegistered(parsedType.ToString()))
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
            known[action.elementId] = parsedType.ToString();
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
                    Enum.TryParse(action.elementType, true, out DesignerElementType type);
                    var created = context.CreateMetadataElement(type);
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

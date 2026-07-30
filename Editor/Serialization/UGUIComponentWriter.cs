using System;
using System.Collections.Generic;
using System.Reflection;
using emiteat.NexUI.Designer.Editor.Components;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Serialization
{
    /// <summary>
    /// Materializes an element's authored components onto the generated uGUI GameObject: adds the real
    /// MonoBehaviour and writes the values the user set, straight onto its serialized fields.
    /// </summary>
    /// <remarks>
    /// Because the schema was reflected from the same type, writing back is the exact inverse - the
    /// Designer never has to keep a hand-written mapping table per component in sync. Only values the
    /// user actually changed are written, so a component keeps Unity's own defaults for everything
    /// else instead of having the Designer's idea of a default baked into every prefab.
    ///
    /// Components that belong to another backend are reported, not written: a UI Toolkit control
    /// cannot exist on a uGUI prefab, and pretending otherwise would produce a broken object.
    /// </remarks>
    internal static class UGUIComponentWriter
    {
        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static void Apply(GameObject go, DesignerElementMetadata element, DesignerSaveReport report)
        {
            if (go == null || element?.components == null) return;

            foreach (var component in element.components)
            {
                if (component == null || string.IsNullOrEmpty(component.typeId)) continue;
                var type = DesignerUIComponentRegistry.Get(component.typeId);

                if (type == null || type.Family == DesignerUIComponentFamily.Core) continue;
                if (type.Family == DesignerUIComponentFamily.UIToolkit)
                {
                    report.MarkUnsupported(type.DisplayName,
                        $"'{element.elementId}' carries {type.DisplayName}, which is a UI Toolkit control and cannot exist on a uGUI prefab.",
                        element.elementId);
                    continue;
                }

                if (type.BackingType == null)
                {
                    report.MarkSkipped($"'{element.elementId}' component {type.DisplayName} has no uGUI implementation.");
                    continue;
                }

                var behaviour = Ensure(go, type.BackingType, element.elementId, report);
                if (behaviour == null) continue;

                if (behaviour is Behaviour toggleable && toggleable.enabled != component.enabled)
                    toggleable.enabled = component.enabled;

                ApplyValues(behaviour, component, type, element.elementId, report);
            }
        }

        private static Component Ensure(GameObject go, Type type, string elementId, DesignerSaveReport report)
        {
            var existing = go.GetComponent(type);
            if (existing != null) return existing;

            try
            {
                var added = go.AddComponent(type);
                report.MarkChanged($"Added {type.Name} to '{elementId}'");
                return added;
            }
            catch (Exception ex)
            {
                report.Warn($"Could not add {type.Name} to '{elementId}': {ex.Message}");
                return null;
            }
        }

        private static void ApplyValues(Component behaviour, DesignerElementComponent component,
            DesignerUIComponentType type, string elementId, DesignerSaveReport report)
        {
            if (component.properties == null || component.properties.Count == 0) return;

            var fields = FieldsByKey(type.BackingType);
            foreach (var entry in component.properties)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key) || entry.value == null) continue;
                if (!fields.TryGetValue(entry.key, out var field))
                {
                    report.MarkSkipped($"'{elementId}' has a value for {type.DisplayName}.{entry.key}, which this build's component does not define.");
                    continue;
                }

                var value = Convert(entry.value, field.FieldType);
                if (value == null)
                {
                    report.MarkSkipped($"'{elementId}' {type.DisplayName}.{entry.key} could not be converted to {field.FieldType.Name}.");
                    continue;
                }

                try { field.SetValue(behaviour, value); }
                catch (Exception ex) { report.Warn($"'{elementId}' {type.DisplayName}.{entry.key}: {ex.Message}"); }
            }

            // Graphics cache their mesh; without this the prefab keeps the pre-edit visual.
            if (behaviour is UnityEngine.UI.Graphic graphic) graphic.SetAllDirty();
        }

        /// <summary>Same key derivation the schema reflection uses, so writing is the exact inverse of reading.</summary>
        private static Dictionary<string, FieldInfo> FieldsByKey(Type type)
        {
            var result = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
                foreach (var field in current.GetFields(FieldFlags | BindingFlags.DeclaredOnly))
                {
                    var key = Key(field.Name);
                    if (!result.ContainsKey(key)) result[key] = field;
                }
            return result;
        }

        private static string Key(string fieldName)
        {
            var name = fieldName;
            if (name.StartsWith("m_", StringComparison.Ordinal)) name = name.Substring(2);
            if (name.Length > 0 && char.IsUpper(name[0])) name = char.ToLowerInvariant(name[0]) + name.Substring(1);
            return name;
        }

        private static object Convert(DesignerPropertyValue value, Type fieldType)
        {
            if (fieldType == typeof(bool)) return value.boolValue;
            if (fieldType == typeof(int)) return value.intValue;
            if (fieldType == typeof(float)) return value.floatValue;
            if (fieldType == typeof(double)) return (double)value.floatValue;
            if (fieldType == typeof(string)) return value.stringValue ?? string.Empty;
            if (fieldType == typeof(Color)) return value.colorValue;
            if (fieldType == typeof(Color32)) return (Color32)value.colorValue;
            if (fieldType == typeof(Vector2)) return value.vector2Value;
            if (fieldType.IsEnum)
            {
                var names = Enum.GetValues(fieldType);
                var index = Mathf.Clamp(value.intValue, 0, names.Length - 1);
                return names.GetValue(index);
            }
            if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
                return value.assetValue != null && fieldType.IsInstanceOfType(value.assetValue) ? value.assetValue : null;
            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Components
{
    /// <summary>
    /// Builds a component's property schema by reflecting its real serialized fields, the same set
    /// Unity's own Inspector shows.
    /// </summary>
    /// <remarks>
    /// Hand-writing the schema for <c>Image</c>, <c>ScrollRect</c> or <c>TMP_InputField</c> would mean
    /// transcribing dozens of fields per component and re-checking them on every Unity upgrade. Reading
    /// them from the type instead means the Designer shows exactly what the component actually has -
    /// and a field Unity adds tomorrow appears without a code change here.
    ///
    /// Unity's serialization rules are what decide inclusion: public fields, plus private fields marked
    /// <c>[SerializeField]</c>, minus <c>[HideInInspector]</c>, statics, constants and obsolete members.
    /// Field types the Designer cannot edit yet (arrays, UnityEvents, nested structs) are skipped
    /// rather than shown as something they are not.
    /// </remarks>
    internal static class DesignerReflectedSchema
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                                            | BindingFlags.DeclaredOnly;

        /// <summary>Fields Unity itself hides or that the Designer owns through the element's own rect.</summary>
        private static readonly HashSet<string> Skipped = new HashSet<string>
        {
            "m_Script", "m_Navigation", "m_OnClick", "m_OnValueChanged", "m_OnEndEdit", "m_OnSubmit",
            "m_OnSelect", "m_OnDeselect", "onValueChanged", "onClick", "onEndEdit", "onSubmit"
        };

        public static List<DesignerComponentProperty> Build(Type type, string groupHint = null)
        {
            var properties = new List<DesignerComponentProperty>();
            if (type == null) return properties;

            // Walk base types too: Image's colour lives on Graphic, interactable lives on Selectable.
            var chain = new List<Type>();
            for (var current = type; current != null && current != typeof(MonoBehaviour) && current != typeof(object); current = current.BaseType)
                chain.Insert(0, current);

            var seen = new HashSet<string>();
            foreach (var level in chain)
                foreach (var field in level.GetFields(Fields))
                {
                    if (!IsSerialized(field)) continue;
                    var key = Key(field.Name);
                    if (!seen.Add(key)) continue;

                    var property = Describe(field, key, groupHint);
                    if (property != null) properties.Add(property);
                }

            return properties;
        }

        private static bool IsSerialized(FieldInfo field)
        {
            if (field.IsStatic || field.IsLiteral || field.IsInitOnly) return false;
            if (Skipped.Contains(field.Name)) return false;
            if (field.GetCustomAttribute<HideInInspector>() != null) return false;
            if (field.GetCustomAttribute<ObsoleteAttribute>() != null) return false;
            if (field.GetCustomAttribute<NonSerializedAttribute>() != null) return false;
            return field.IsPublic || field.GetCustomAttribute<SerializeField>() != null;
        }

        private static DesignerComponentProperty Describe(FieldInfo field, string key, string groupHint)
        {
            var type = field.FieldType;
            var name = Humanize(field.Name);
            var group = groupHint ?? GroupFor(key, type);
            var exposure = ExposureFor(key);
            var range = field.GetCustomAttribute<RangeAttribute>();
            var tooltip = field.GetCustomAttribute<TooltipAttribute>()?.tooltip;

            DesignerComponentProperty property;
            if (type == typeof(bool))
                property = DesignerComponentPropertyBuilder.Bool(key, name, false, group, exposure, tooltip);
            else if (type == typeof(int))
                property = DesignerComponentPropertyBuilder.Int(key, name, 0, group, exposure,
                    range?.min ?? 0f, range?.max ?? 0f, tooltip);
            else if (type == typeof(float))
                property = DesignerComponentPropertyBuilder.Float(key, name, 0f, group, exposure,
                    range?.min ?? 0f, range?.max ?? 0f, tooltip);
            else if (type == typeof(string))
                property = DesignerComponentPropertyBuilder.Text(key, name, string.Empty, group, exposure, tooltip);
            else if (type == typeof(Color) || type == typeof(Color32))
                property = DesignerComponentPropertyBuilder.Color(key, name, UnityEngine.Color.white, group, exposure, tooltip);
            else if (type == typeof(Vector2))
                property = DesignerComponentPropertyBuilder.Vector2(key, name, UnityEngine.Vector2.zero, group, exposure, tooltip);
            else if (type.IsEnum)
                property = DesignerComponentPropertyBuilder.Enum(key, name, Enum.GetNames(type), 0, group, exposure, tooltip);
            else if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                property = DesignerComponentPropertyBuilder.Asset(key, name, type, group, exposure, tooltip);
            else
                return null; // arrays, UnityEvents, nested structs: not editable here yet

            // Reflected schemas are per component type, so their localization keys live in their own
            // namespace and fall back to the humanized field name when untranslated.
            property.LocalizationKey = "prop.reflected." + key;
            return property;
        }

        /// <summary>"m_PreserveAspect" / "preserveAspect" → "preserveAspect".</summary>
        private static string Key(string fieldName)
        {
            var name = fieldName;
            if (name.StartsWith("m_", StringComparison.Ordinal)) name = name.Substring(2);
            if (name.Length > 0 && char.IsUpper(name[0])) name = char.ToLowerInvariant(name[0]) + name.Substring(1);
            return name;
        }

        /// <summary>"preserveAspect" → "Preserve Aspect", matching how Unity labels the same field.</summary>
        internal static string Humanize(string fieldName)
        {
            var key = Key(fieldName);
            var builder = new System.Text.StringBuilder(key.Length + 8);
            for (var i = 0; i < key.Length; i++)
            {
                var c = key[i];
                if (i == 0) { builder.Append(char.ToUpperInvariant(c)); continue; }
                if (char.IsUpper(c) && !char.IsUpper(key[i - 1])) builder.Append(' ');
                builder.Append(c);
            }
            return builder.ToString();
        }

        private static string GroupFor(string key, Type type)
        {
            var lower = key.ToLowerInvariant();
            if (lower.Contains("interactable") || lower.Contains("navigation") || lower.Contains("transition")
                || lower.Contains("raycast") || lower.Contains("targetgraphic"))
                return DesignerComponentPropertyGroup.Interaction;
            if (lower.Contains("padding") || lower.Contains("spacing") || lower.Contains("alignment")
                || lower.Contains("cellsize") || lower.Contains("constraint") || lower.Contains("childcontrol")
                || lower.Contains("childforce") || lower.Contains("layout") || lower.Contains("aspect"))
                return DesignerComponentPropertyGroup.Layout;
            if (lower.Contains("value") || lower.Contains("min") || lower.Contains("max")
                || lower.Contains("fill") || lower.Contains("wholenumbers"))
                return DesignerComponentPropertyGroup.Value;
            if (lower.Contains("text") || lower.Contains("font") || lower.Contains("caption")
                || lower.Contains("placeholder") || lower.Contains("character"))
                return DesignerComponentPropertyGroup.Content;
            if (lower.Contains("color") || lower.Contains("sprite") || lower.Contains("material")
                || lower.Contains("texture") || lower.Contains("maskable") || lower.Contains("preserve"))
                return DesignerComponentPropertyGroup.Appearance;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return DesignerComponentPropertyGroup.Content;
            return DesignerComponentPropertyGroup.Behavior;
        }

        /// <summary>
        /// A reflected component can expose thirty fields; showing all of them by default is what makes
        /// a Unity inspector unreadable. The handful designers actually touch stay Basic.
        /// </summary>
        private static DesignerComponentPropertyExposure ExposureFor(string key)
        {
            switch (key)
            {
                case "sprite": case "color": case "text": case "value": case "isOn": case "interactable":
                case "minValue": case "maxValue": case "wholeNumbers": case "fillAmount": case "fillMethod":
                case "type": case "preserveAspect": case "texture": case "horizontal": case "vertical":
                case "spacing": case "padding": case "childAlignment": case "cellSize": case "characterLimit":
                case "contentType": case "lineType": case "readOnly": case "direction":
                    return DesignerComponentPropertyExposure.Basic;
                default:
                    return DesignerComponentPropertyExposure.Advanced;
            }
        }
    }
}

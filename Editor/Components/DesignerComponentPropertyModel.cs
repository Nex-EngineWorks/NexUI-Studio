using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Components
{
    /// <summary>
    /// How prominently a component property is shown. Basic properties are the ones a designer
    /// touches on most elements; everything else folds away under Advanced so a component with forty
    /// properties still opens as a short panel.
    /// </summary>
    public enum DesignerComponentPropertyExposure
    {
        Basic,
        Advanced
    }

    /// <summary>Inspector foldout a property belongs to. Values are localization keys.</summary>
    public static class DesignerComponentPropertyGroup
    {
        public const string Content = "prop.group.content";
        public const string Value = "prop.group.value";
        public const string Interaction = "prop.group.interaction";
        public const string Data = "prop.group.data";
        public const string Appearance = "prop.group.appearance";
        public const string Layout = "prop.group.layout";
        public const string Behavior = "prop.group.behavior";

        /// <summary>Foldout order in the Inspector.</summary>
        public static readonly string[] Order =
        {
            Content, Value, Data, Interaction, Appearance, Layout, Behavior
        };
    }

    /// <summary>
    /// One property a component type exposes - the Designer equivalent of a serialized field on a
    /// Unity component. The schema carries everything the Inspector, the validators and the backend
    /// writers need, so a property is declared once and every surface follows.
    /// </summary>
    public sealed class DesignerComponentProperty
    {
        /// <summary>Stable storage key ("slider.wholeNumbers"). Never localized, never renamed.</summary>
        public string Key;
        public string DisplayName;
        /// <summary>Localization key for <see cref="DisplayName"/>. Falls back to DisplayName when missing.</summary>
        public string LocalizationKey;
        public string Description;
        public string Group = DesignerComponentPropertyGroup.Content;
        public DesignerComponentPropertyExposure Exposure = DesignerComponentPropertyExposure.Basic;
        public DesignerPropertyValueType Type = DesignerPropertyValueType.Float;
        public DesignerPropertyValue Default = new DesignerPropertyValue();

        /// <summary>Numeric range. When <see cref="Max"/> &gt; <see cref="Min"/> the Inspector draws a slider.</summary>
        public float Min;
        public float Max;

        /// <summary>Choices for an Enum property; the stored value is the index into this array.</summary>
        public string[] EnumOptions;

        /// <summary>Asset type an AssetReference property accepts (defaults to <see cref="Object"/>).</summary>
        public System.Type AssetType;

        /// <summary>UXML attribute the code generator emits for this property. Null ⇒ not emitted.</summary>
        public string UxmlAttribute;

        public DesignerBackendSupport UGUI = DesignerBackendSupport.Partial;
        public DesignerBackendSupport UIToolkit = DesignerBackendSupport.Partial;

        public bool HasRange => Max > Min;
    }

    /// <summary>Fluent-ish factory helpers so a schema entry stays one readable line.</summary>
    public static class DesignerComponentPropertyBuilder
    {
        public static DesignerComponentProperty Float(string key, string name, float defaultValue,
            string group = null, DesignerComponentPropertyExposure exposure = DesignerComponentPropertyExposure.Basic,
            float min = 0f, float max = 0f, string description = null, string uxml = null)
            => Make(key, name, DesignerPropertyValueType.Float, group, exposure, description, uxml,
                value: new DesignerPropertyValue { type = DesignerPropertyValueType.Float, floatValue = defaultValue },
                min: min, max: max);

        public static DesignerComponentProperty Int(string key, string name, int defaultValue,
            string group = null, DesignerComponentPropertyExposure exposure = DesignerComponentPropertyExposure.Basic,
            float min = 0f, float max = 0f, string description = null, string uxml = null)
            => Make(key, name, DesignerPropertyValueType.Integer, group, exposure, description, uxml,
                value: new DesignerPropertyValue { type = DesignerPropertyValueType.Integer, intValue = defaultValue },
                min: min, max: max);

        public static DesignerComponentProperty Bool(string key, string name, bool defaultValue,
            string group = null, DesignerComponentPropertyExposure exposure = DesignerComponentPropertyExposure.Basic,
            string description = null, string uxml = null)
            => Make(key, name, DesignerPropertyValueType.Boolean, group, exposure, description, uxml,
                value: new DesignerPropertyValue { type = DesignerPropertyValueType.Boolean, boolValue = defaultValue });

        public static DesignerComponentProperty Text(string key, string name, string defaultValue = "",
            string group = null, DesignerComponentPropertyExposure exposure = DesignerComponentPropertyExposure.Basic,
            string description = null, string uxml = null)
            => Make(key, name, DesignerPropertyValueType.String, group, exposure, description, uxml,
                value: new DesignerPropertyValue { type = DesignerPropertyValueType.String, stringValue = defaultValue });

        public static DesignerComponentProperty Color(string key, string name, Color defaultValue,
            string group = null, DesignerComponentPropertyExposure exposure = DesignerComponentPropertyExposure.Basic,
            string description = null)
            => Make(key, name, DesignerPropertyValueType.Color, group, exposure, description, null,
                value: new DesignerPropertyValue { type = DesignerPropertyValueType.Color, colorValue = defaultValue });

        public static DesignerComponentProperty Vector2(string key, string name, Vector2 defaultValue,
            string group = null, DesignerComponentPropertyExposure exposure = DesignerComponentPropertyExposure.Basic,
            string description = null)
            => Make(key, name, DesignerPropertyValueType.Vector2, group, exposure, description, null,
                value: new DesignerPropertyValue { type = DesignerPropertyValueType.Vector2, vector2Value = defaultValue });

        public static DesignerComponentProperty Enum(string key, string name, string[] options, int defaultIndex = 0,
            string group = null, DesignerComponentPropertyExposure exposure = DesignerComponentPropertyExposure.Basic,
            string description = null, string uxml = null)
        {
            var property = Make(key, name, DesignerPropertyValueType.Enum, group, exposure, description, uxml,
                value: new DesignerPropertyValue { type = DesignerPropertyValueType.Enum, intValue = defaultIndex });
            property.EnumOptions = options;
            return property;
        }

        public static DesignerComponentProperty Asset(string key, string name, System.Type assetType,
            string group = null, DesignerComponentPropertyExposure exposure = DesignerComponentPropertyExposure.Basic,
            string description = null)
        {
            var property = Make(key, name, DesignerPropertyValueType.AssetReference, group, exposure, description, null,
                value: new DesignerPropertyValue { type = DesignerPropertyValueType.AssetReference });
            property.AssetType = assetType;
            return property;
        }

        private static DesignerComponentProperty Make(string key, string name, DesignerPropertyValueType type,
            string group, DesignerComponentPropertyExposure exposure, string description, string uxml,
            DesignerPropertyValue value, float min = 0f, float max = 0f)
            => new DesignerComponentProperty
            {
                Key = key,
                DisplayName = name,
                LocalizationKey = "prop." + key,
                Description = description,
                Group = group ?? DesignerComponentPropertyGroup.Content,
                Exposure = exposure,
                Type = type,
                Default = value,
                Min = min,
                Max = max,
                UxmlAttribute = uxml
            };
    }

    /// <summary>
    /// Reads and writes component property values against a schema. Every read falls back to the
    /// schema default, so an element that has never been touched behaves exactly like one whose
    /// values were written out - and "is this overridden?" stays answerable for the Inspector.
    /// </summary>
    public static class DesignerComponentPropertyAccess
    {
        public static IReadOnlyList<DesignerComponentProperty> SchemaFor(DesignerElementMetadata element)
            => element == null ? System.Array.Empty<DesignerComponentProperty>()
                : DesignerComponentRegistry.Get(element.elementType).Properties;

        public static DesignerComponentProperty Find(DesignerElementMetadata element, string key)
        {
            var schema = SchemaFor(element);
            for (var i = 0; i < schema.Count; i++)
                if (schema[i].Key == key) return schema[i];
            return null;
        }

        public static bool IsOverridden(DesignerElementMetadata element, string key)
            => element != null && DesignerComponentPropertyBag.Has(element.componentProperties, key);

        /// <summary>Stored value if present, else the schema default, else null.</summary>
        public static DesignerPropertyValue Value(DesignerElementMetadata element, string key)
        {
            if (element == null) return null;
            var stored = DesignerComponentPropertyBag.Find(element.componentProperties, key);
            if (stored != null) return stored;
            return Find(element, key)?.Default;
        }

        public static float GetFloat(DesignerElementMetadata element, string key, float fallback = 0f)
        {
            var value = Value(element, key);
            return value == null ? fallback : value.floatValue;
        }

        public static int GetInt(DesignerElementMetadata element, string key, int fallback = 0)
        {
            var value = Value(element, key);
            return value == null ? fallback : value.intValue;
        }

        public static bool GetBool(DesignerElementMetadata element, string key, bool fallback = false)
        {
            var value = Value(element, key);
            return value == null ? fallback : value.boolValue;
        }

        public static string GetString(DesignerElementMetadata element, string key, string fallback = "")
        {
            var value = Value(element, key);
            return value == null ? fallback : value.stringValue ?? fallback;
        }

        public static Color GetColor(DesignerElementMetadata element, string key)
        {
            var value = Value(element, key);
            return value == null ? UnityEngine.Color.white : value.colorValue;
        }

        public static Vector2 GetVector2(DesignerElementMetadata element, string key)
        {
            var value = Value(element, key);
            return value == null ? UnityEngine.Vector2.zero : value.vector2Value;
        }

        public static Object GetAsset(DesignerElementMetadata element, string key)
        {
            var value = Value(element, key);
            return value?.assetValue;
        }

        /// <summary>Enum option name for the stored index, or the first option when out of range.</summary>
        public static string GetEnum(DesignerElementMetadata element, string key)
        {
            var property = Find(element, key);
            if (property?.EnumOptions == null || property.EnumOptions.Length == 0) return null;
            var index = GetInt(element, key, property.Default?.intValue ?? 0);
            return index >= 0 && index < property.EnumOptions.Length ? property.EnumOptions[index] : property.EnumOptions[0];
        }

        /// <summary>Writes a value in place (caller owns Undo). Passing null resets to the schema default.</summary>
        public static bool Set(DesignerElementMetadata element, string key, DesignerPropertyValue value)
        {
            if (element == null) return false;
            element.componentProperties ??= new List<DesignerComponentPropertyEntry>();
            return DesignerComponentPropertyBag.Set(element.componentProperties, key, value);
        }

        public static bool Reset(DesignerElementMetadata element, string key) => Set(element, key, null);

        /// <summary>
        /// Drops values whose key is not in the element's current schema. Used when a component is
        /// swapped to another type - the old type's values would otherwise linger invisibly.
        /// </summary>
        public static int PruneUnknown(DesignerElementMetadata element)
        {
            if (element?.componentProperties == null) return 0;
            var schema = SchemaFor(element);
            var removed = 0;
            for (var i = element.componentProperties.Count - 1; i >= 0; i--)
            {
                var entry = element.componentProperties[i];
                var known = false;
                for (var s = 0; s < schema.Count && !known; s++)
                    known = entry != null && schema[s].Key == entry.key;
                if (known) continue;
                element.componentProperties.RemoveAt(i);
                removed++;
            }
            return removed;
        }
    }

    /// <summary>
    /// Central property-level backend contract. Component support alone is too coarse: a stock
    /// Slider can write min/max exactly while its tick marks remain preview-only. Save Preview and
    /// the Inspector use this table so they never imply that every field in a large schema is
    /// serialized equally.
    /// </summary>
    public static class DesignerComponentPropertySupport
    {
        public static DesignerBackendSupport UGUI(DesignerComponentDescriptor component,
            DesignerComponentProperty property)
        {
            if (component == null || property == null) return DesignerBackendSupport.Unsupported;
            if (component.Family == DesignerComponentFamily.UIToolkit) return DesignerBackendSupport.PreviewOnly;

            var control = component.UGUIControl ?? string.Empty;
            var key = property.Key ?? string.Empty;
            if (key == "clipContent") return DesignerBackendSupport.Full;
            if ((key == "interactable" || key == "transition" || key == "navigation.enabled") && IsSelectable(control))
                return DesignerBackendSupport.Full;
            if (key.StartsWith("value.") && (control == "Slider" || IsFilledImage(component.TypeId)))
                return key is "value.segments" or "value.showLabel" or "value.format" or "value.animationDuration"
                    ? DesignerBackendSupport.PreviewOnly
                    : DesignerBackendSupport.Full;
            if (key == "toggle.isOn" && control == "Toggle") return DesignerBackendSupport.Full;
            if (key.StartsWith("toggle.") && control == "Toggle") return DesignerBackendSupport.Partial;
            if ((key is "choice.options" or "choice.value") && (control == "Dropdown" || control == "DropdownTMP"))
                return DesignerBackendSupport.Full;
            if (key.StartsWith("choice.") && (control == "Dropdown" || control == "DropdownTMP"))
                return DesignerBackendSupport.Partial;
            if (key.StartsWith("input.") && (control == "InputField" || control == "InputFieldTMP"))
                return key is "input.placeholder" or "input.maxLength" or "input.contentType" or "input.lineType" or "input.readOnly"
                    ? DesignerBackendSupport.Full
                    : DesignerBackendSupport.Partial;
            if (key.StartsWith("scroll.") && control == "ScrollView") return DesignerBackendSupport.Full;
            if (key.StartsWith("media.") && (control == "Image" || control == "Panel" || control == "RawImage"))
                return key == "media.sprite" && control == "RawImage"
                    ? DesignerBackendSupport.Partial
                    : DesignerBackendSupport.Full;
            if (key.StartsWith("text.") && (control == "Text" || control == "TextTMP"))
                return key == "text.maxLines" && control == "Text"
                    ? DesignerBackendSupport.Partial
                    : key is "text.richText" or "text.autoSize" or "text.maxLines"
                    ? DesignerBackendSupport.Full
                    : DesignerBackendSupport.Partial;
            if (key is "size" or "emphasis" or "slider.showTicks" or "slider.fillHandle")
                return DesignerBackendSupport.PreviewOnly;
            return property.UGUI;
        }

        public static DesignerBackendSupport UIToolkit(DesignerComponentDescriptor component,
            DesignerComponentProperty property)
        {
            if (component == null || property == null) return DesignerBackendSupport.Unsupported;
            if (component.Family == DesignerComponentFamily.UGUI) return DesignerBackendSupport.PreviewOnly;
            if (CanEmitUxmlAttribute(component, property) || property.Key == "clipContent")
                return DesignerBackendSupport.Full;
            if (property.Key is "size" or "emphasis" or "slider.showTicks" or "slider.fillHandle" or "value.segments")
                return DesignerBackendSupport.PreviewOnly;
            return property.UIToolkit;
        }

        public static bool CanEmitUxmlAttribute(DesignerComponentDescriptor component,
            DesignerComponentProperty property)
        {
            if (component == null || property == null || string.IsNullOrEmpty(property.UxmlAttribute)) return false;
            if (component.Family == DesignerComponentFamily.UGUI) return false;
            if (!string.IsNullOrEmpty(component.UxmlTag) && component.UxmlTag != "ui:VisualElement") return true;
            return property.UxmlAttribute == "enabled";
        }

        private static bool IsSelectable(string control)
            => control is "Button" or "ButtonTMP" or "Toggle" or "Slider" or "Scrollbar" or
                "Dropdown" or "DropdownTMP" or "InputField" or "InputFieldTMP";

        private static bool IsFilledImage(string typeId)
            => typeId is "ProgressBar" or "StatBar" or "RadialFill";
    }
}

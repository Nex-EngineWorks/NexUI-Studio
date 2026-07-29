using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Inspectors
{
    /// <summary>
    /// Renders the selected component's own properties - the Designer's equivalent of a Unity
    /// component inspector. The fields come from the type's schema
    /// (<see cref="DesignerComponentProperty"/>), so a component gains an editor by declaring a
    /// property, never by writing UI here.
    ///
    /// Keeping it readable with dozens of properties is the whole design: properties are grouped into
    /// foldouts, only Basic ones are shown by default, and each foldout's expanded state is
    /// remembered per group rather than per element so moving between elements does not reshuffle
    /// the panel.
    /// </summary>
    public sealed class ComponentPropertiesInspector : DesignerInspectorBase
    {
        private const string AdvancedPrefKey = "NexUI.Designer.Inspector.ShowAdvancedProperties";
        private const string FoldoutPrefPrefix = "NexUI.Designer.Inspector.PropertyGroup.";

        private readonly VisualElement _host;
        private string _query = string.Empty;
        private bool _writing;

        public ComponentPropertiesInspector(NexUIDesignerContext context) : base(context, "inspector.componentProperties")
        {
            _host = new VisualElement();
            Add(_host);
            Subscriptions.Add<IReadOnlyList<DesignerElementMetadata>>(
                h => context.MultiSelectionChanged += h, h => context.MultiSelectionChanged -= h, _ => Rebuild());
            Subscriptions.Add<DesignerElementMetadata>(
                h => context.ElementChanged += h, h => context.ElementChanged -= h, _ =>
                {
                    // UpdateElement raises synchronously. Rebuilding here would destroy a Slider
                    // after its first drag tick and make continuous editing impossible.
                    if (!_writing) Rebuild();
                });
            Rebuild();
        }

        private void Rebuild()
        {
            _host.Clear();
            var element = Context.SelectedMetadata;
            if (element == null) { style.display = DisplayStyle.None; return; }

            var descriptor = DesignerComponentRegistry.Get(element.elementType);
            if (descriptor.Properties.Count == 0) { style.display = DisplayStyle.None; return; }
            style.display = DisplayStyle.Flex;

            var heading = new Label($"{descriptor.DisplayName}  ·  {descriptor.Properties.Count} properties")
            {
                tooltip = descriptor.Description
            };
            heading.AddToClassList("nexui-component-property-heading");
            _host.Add(heading);

            if (!string.IsNullOrWhiteSpace(descriptor.Description))
            {
                var description = new Label(descriptor.Description);
                description.AddToClassList("nexui-component-property-description");
                _host.Add(description);
            }

            if (descriptor.Properties.Count >= 10)
            {
                var search = new ToolbarSearchField { tooltip = DesignerLocalization.T("tooltip.inspector.searchProperties") };
                search.SetValueWithoutNotify(_query);
                search.RegisterValueChangedCallback(evt =>
                {
                    _query = evt.newValue ?? string.Empty;
                    Rebuild();
                });
                _host.Add(search);
            }

            var showAdvanced = EditorPrefs.GetBool(AdvancedPrefKey, false);
            var hasAdvanced = false;
            foreach (var property in descriptor.Properties)
                if (property.Exposure == DesignerComponentPropertyExposure.Advanced) { hasAdvanced = true; break; }

            if (hasAdvanced)
            {
                var advancedToggle = new Toggle(DesignerLocalization.T("inspector.showAdvanced")) { value = showAdvanced };
                advancedToggle.AddToClassList("nexui-inspector-advanced-toggle");
                advancedToggle.tooltip = DesignerLocalization.T("tooltip.inspector.showAdvanced");
                advancedToggle.RegisterValueChangedCallback(evt =>
                {
                    EditorPrefs.SetBool(AdvancedPrefKey, evt.newValue);
                    Rebuild();
                });
                _host.Add(advancedToggle);
            }

            foreach (var groupId in OrderedGroups(descriptor))
            {
                var fields = new List<DesignerComponentProperty>();
                foreach (var property in descriptor.Properties)
                {
                    if (property.Group != groupId) continue;
                    if (!showAdvanced && property.Exposure == DesignerComponentPropertyExposure.Advanced) continue;
                    if (!MatchesQuery(property, _query)) continue;
                    fields.Add(property);
                }
                if (fields.Count == 0) continue;

                var prefKey = FoldoutPrefPrefix + groupId;
                var foldout = new Foldout
                {
                    text = $"{DesignerLocalization.T(groupId)}  ({fields.Count})",
                    value = EditorPrefs.GetBool(prefKey, true)
                };
                foldout.AddToClassList("nexui-property-group");
                foldout.RegisterValueChangedCallback(evt =>
                {
                    if (evt.target == foldout) EditorPrefs.SetBool(prefKey, evt.newValue);
                });

                foreach (var property in fields)
                    foldout.Add(BuildField(element, property));

                _host.Add(foldout);
            }
        }

        /// <summary>Groups in the declared display order, then any group a schema invented afterwards.</summary>
        private static List<string> OrderedGroups(DesignerComponentDescriptor descriptor)
        {
            var result = new List<string>();
            foreach (var groupId in DesignerComponentPropertyGroup.Order)
                foreach (var property in descriptor.Properties)
                    if (property.Group == groupId && !result.Contains(groupId)) { result.Add(groupId); break; }
            foreach (var property in descriptor.Properties)
                if (!result.Contains(property.Group)) result.Add(property.Group);
            return result;
        }

        private VisualElement BuildField(DesignerElementMetadata element, DesignerComponentProperty property)
        {
            var label = Label(property);
            var descriptor = DesignerComponentRegistry.Get(element.elementType);
            var ugui = DesignerComponentPropertySupport.UGUI(descriptor, property);
            var uitk = DesignerComponentPropertySupport.UIToolkit(descriptor, property);
            var tooltip = string.IsNullOrEmpty(property.Description) ? label : label + "\n" + property.Description;
            tooltip += $"\n\nuGUI: {ugui}  ·  UI Toolkit: {uitk}";
            VisualElement field;

            switch (property.Type)
            {
                case DesignerPropertyValueType.Boolean:
                {
                    var toggle = new Toggle(label) { value = DesignerComponentPropertyAccess.GetBool(element, property.Key) };
                    toggle.RegisterValueChangedCallback(evt => Write(element, property, v => v.boolValue = evt.newValue));
                    field = toggle;
                    break;
                }
                case DesignerPropertyValueType.Integer when property.HasRange:
                {
                    var slider = new SliderInt(label, Mathf.RoundToInt(property.Min), Mathf.RoundToInt(property.Max))
                    {
                        value = DesignerComponentPropertyAccess.GetInt(element, property.Key),
                        showInputField = true
                    };
                    slider.RegisterValueChangedCallback(evt => Write(element, property, v => v.intValue = evt.newValue));
                    field = slider;
                    break;
                }
                case DesignerPropertyValueType.Integer:
                {
                    var input = new IntegerField(label) { value = DesignerComponentPropertyAccess.GetInt(element, property.Key) };
                    input.RegisterValueChangedCallback(evt => Write(element, property, v => v.intValue = evt.newValue));
                    field = input;
                    break;
                }
                case DesignerPropertyValueType.Float when property.HasRange:
                {
                    var slider = new Slider(label, property.Min, property.Max)
                    {
                        value = DesignerComponentPropertyAccess.GetFloat(element, property.Key),
                        showInputField = true
                    };
                    slider.RegisterValueChangedCallback(evt => Write(element, property, v => v.floatValue = evt.newValue));
                    field = slider;
                    break;
                }
                case DesignerPropertyValueType.Float:
                {
                    var input = new FloatField(label) { value = DesignerComponentPropertyAccess.GetFloat(element, property.Key) };
                    input.RegisterValueChangedCallback(evt => Write(element, property, v => v.floatValue = evt.newValue));
                    field = input;
                    break;
                }
                case DesignerPropertyValueType.Color:
                {
                    var input = new ColorField(label) { value = DesignerComponentPropertyAccess.GetColor(element, property.Key) };
                    input.RegisterValueChangedCallback(evt => Write(element, property, v => v.colorValue = evt.newValue));
                    field = input;
                    break;
                }
                case DesignerPropertyValueType.Vector2:
                {
                    var input = new Vector2Field(label) { value = DesignerComponentPropertyAccess.GetVector2(element, property.Key) };
                    input.RegisterValueChangedCallback(evt => Write(element, property, v => v.vector2Value = evt.newValue));
                    field = input;
                    break;
                }
                case DesignerPropertyValueType.Enum:
                {
                    var options = new List<string>(property.EnumOptions ?? System.Array.Empty<string>());
                    if (options.Count == 0) options.Add("Default");
                    var index = Mathf.Clamp(DesignerComponentPropertyAccess.GetInt(element, property.Key), 0, options.Count - 1);
                    var popup = new PopupField<string>(label, options, index);
                    popup.RegisterValueChangedCallback(evt =>
                        Write(element, property, v => v.intValue = options.IndexOf(evt.newValue)));
                    field = popup;
                    break;
                }
                case DesignerPropertyValueType.AssetReference:
                {
                    var input = new ObjectField(label)
                    {
                        objectType = property.AssetType ?? typeof(Object),
                        allowSceneObjects = false,
                        value = DesignerComponentPropertyAccess.GetAsset(element, property.Key)
                    };
                    input.RegisterValueChangedCallback(evt => Write(element, property, v => v.assetValue = evt.newValue));
                    field = input;
                    break;
                }
                default:
                {
                    var input = new TextField(label) { value = DesignerComponentPropertyAccess.GetString(element, property.Key) };
                    input.RegisterValueChangedCallback(evt => Write(element, property, v => v.stringValue = evt.newValue));
                    field = input;
                    break;
                }
            }

            field.tooltip = tooltip;
            field.AddToClassList("nexui-property-field");
            return WithOverrideAffordance(field, element, property);
        }

        /// <summary>
        /// A row that shows whether the value differs from the component default and offers a reset,
        /// the same contract Unity's prefab overrides use. Without it, a schema-driven panel gives no
        /// way to tell an authored value from an inherited one.
        /// </summary>
        private VisualElement WithOverrideAffordance(VisualElement field, DesignerElementMetadata element,
            DesignerComponentProperty property)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            field.style.flexGrow = 1;
            row.Add(field);

            var overridden = DesignerComponentPropertyAccess.IsOverridden(element, property.Key);
            if (!overridden) return row;

            field.AddToClassList("is-overridden");
            var reset = new Button(() =>
            {
                _writing = true;
                try
                {
                    Context.UpdateElement(element, e => DesignerComponentPropertyAccess.Reset(e, property.Key),
                        "Reset " + property.DisplayName);
                }
                finally { _writing = false; }
                Rebuild();
            })
            {
                text = "↺",
                tooltip = DesignerLocalization.T("tooltip.inspector.resetProperty")
            };
            reset.AddToClassList("nexui-property-reset");
            row.Add(reset);
            return row;
        }

        private void Write(DesignerElementMetadata element, DesignerComponentProperty property,
            System.Action<DesignerPropertyValue> mutate)
        {
            _writing = true;
            try
            {
                Context.UpdateElement(element, e =>
                {
                    var value = DesignerComponentPropertyAccess.Value(e, property.Key)?.Clone()
                                ?? new DesignerPropertyValue { type = property.Type };
                    value.type = property.Type;
                    mutate(value);
                    DesignerComponentPropertyAccess.Set(e, property.Key, value);
                }, "Set " + property.DisplayName);
            }
            finally { _writing = false; }
        }

        private static bool MatchesQuery(DesignerComponentProperty property, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            query = query.Trim();
            return (property.DisplayName?.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                   (property.Key?.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                   (property.Description?.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
        }

        private static string Label(DesignerComponentProperty property)
        {
            if (string.IsNullOrEmpty(property.LocalizationKey)) return property.DisplayName;
            var localized = DesignerLocalization.T(property.LocalizationKey);
            return localized == property.LocalizationKey ? property.DisplayName : localized;
        }
    }
}

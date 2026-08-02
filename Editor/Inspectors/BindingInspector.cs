using System;
using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.State;
using UnityEditor;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Inspectors
{
    public sealed class BindingInspector : DesignerInspectorBase
    {
        private readonly TextField _textKey;
        private readonly TextField _valueKey;
        private readonly TextField _visibilityKey;
        private readonly TextField _classKey;
        private readonly TextField _commandKey;
        private readonly TextField _interactableKey;
        private readonly EnumField _textMode;
        private readonly EnumField _valueMode;
        private readonly TextField _textConverterKey;
        private readonly TextField _valueConverterKey;
        private bool _refreshing;

        public BindingInspector(NexUIDesignerContext context) : base(context, "inspector.binding")
        {
            _textKey = new TextField("Text Key") { tooltip = DesignerLocalization.T("tooltip.binding.textKey") };
            _valueKey = new TextField("Value Key") { tooltip = DesignerLocalization.T("tooltip.binding.valueKey") };
            _visibilityKey = new TextField("Visibility Key") { tooltip = DesignerLocalization.T("tooltip.binding.visibilityKey") };
            _classKey = new TextField("Class Key") { tooltip = DesignerLocalization.T("tooltip.binding.classKey") };
            _commandKey = new TextField("Command Key") { tooltip = DesignerLocalization.T("tooltip.binding.commandKey") };
            _interactableKey = new TextField("Interactable Key") { tooltip = DesignerLocalization.T("tooltip.binding.interactableKey") };
            _textMode = new EnumField("Text Mode", UIBindingMode.OneWay);
            _valueMode = new EnumField("Value Mode", UIBindingMode.OneWay);
            _textConverterKey = new TextField("Text Converter") { tooltip = "Optional project converter registry key." };
            _valueConverterKey = new TextField("Value Converter") { tooltip = "Optional project converter registry key." };
            // C3 (Simple/Advanced mode): Command Key is the plain-language "event -> action"
            // wiring every screen needs; the raw UIStateStore key fields below it are advanced-
            // only (they require knowing what state keys the game code publishes).
            Add(WithCommandPicker(_commandKey));
            AddAdvancedOnly(WithPicker(_textKey, null));
            AddAdvancedOnly(_textMode);
            AddAdvancedOnly(_textConverterKey);
            AddAdvancedOnly(WithPicker(_valueKey, typeof(float)));
            AddAdvancedOnly(_valueMode);
            AddAdvancedOnly(_valueConverterKey);
            AddAdvancedOnly(WithPicker(_visibilityKey, typeof(bool)));
            AddAdvancedOnly(_classKey);
            AddAdvancedOnly(WithPicker(_interactableKey, typeof(bool)));

            _textKey.RegisterValueChangedCallback(evt => Change(e => e.binding.textKey = evt.newValue));
            _valueKey.RegisterValueChangedCallback(evt => Change(e => e.binding.valueKey = evt.newValue));
            _visibilityKey.RegisterValueChangedCallback(evt => Change(e => e.binding.visibilityKey = evt.newValue));
            _classKey.RegisterValueChangedCallback(evt => Change(e => e.binding.classKey = evt.newValue));
            _commandKey.RegisterValueChangedCallback(evt => Change(e => e.binding.commandKey = evt.newValue));
            _interactableKey.RegisterValueChangedCallback(evt => Change(e => e.binding.interactableKey = evt.newValue));
            _textMode.RegisterValueChangedCallback(evt => Change(e => e.binding.textMode = (UIBindingMode)evt.newValue));
            _valueMode.RegisterValueChangedCallback(evt => Change(e => e.binding.valueMode = (UIBindingMode)evt.newValue));
            _textConverterKey.RegisterValueChangedCallback(evt => Change(e => e.binding.textConverterKey = evt.newValue));
            _valueConverterKey.RegisterValueChangedCallback(evt => Change(e => e.binding.valueConverterKey = evt.newValue));

            Subscriptions.Add<DesignerElementMetadata>(h => context.MetadataSelectionChanged += h, h => context.MetadataSelectionChanged -= h, _ => Refresh());
            Subscriptions.Add(h => context.CanvasChanged += h, h => context.CanvasChanged -= h, Refresh);
            Refresh();
        }

        /// <summary>
        /// Wraps a binding-key <see cref="TextField"/> with a "Pick..." button that lists
        /// <see cref="IBindableProperty{T}"/> data-source candidates (via
        /// <see cref="DesignerBindingSourceScanner"/>) discovered by reflection, so the key can
        /// be connected visually instead of hand-typed. <paramref name="valueTypeFilter"/>
        /// narrows the list to sources compatible with the field (e.g. float for Value Key);
        /// pass null to allow any value type (e.g. Text Key, which converts via ToString()).
        /// </summary>
        private static VisualElement WithPicker(TextField field, Type valueTypeFilter)
        {
            field.style.flexGrow = 1;

            var pick = new Button(() => ShowPicker(field, valueTypeFilter)) { text = "Pick..." };
            pick.tooltip = DesignerLocalization.T("tooltip.binding.pickData");

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row.Add(field);
            row.Add(pick);
            return row;
        }

        private static void ShowPicker(TextField field, Type valueTypeFilter)
        {
            var candidates = DesignerBindingSourceScanner.Find(valueTypeFilter);
            var menu = new GenericMenu();
            if (candidates.Count == 0)
            {
                menu.AddDisabledItem(new UnityEngine.GUIContent("No IBindableProperty<T> sources found"));
            }
            else
            {
                foreach (var candidate in candidates)
                {
                    var key = candidate.Key;
                    menu.AddItem(new UnityEngine.GUIContent(candidate.DisplayPath), field.value == key,
                        () => field.value = key);
                }
            }
            menu.ShowAsContext();
        }

        /// <summary>
        /// C1: drag-and-drop-first wiring for the Command Key field - lists built-in pipeline
        /// keys plus every commandKey already used across the project's metadata assets, so
        /// wiring an event to an action never requires typing/knowing a class name.
        /// </summary>
        private static VisualElement WithCommandPicker(TextField field)
        {
            field.style.flexGrow = 1;

            var pick = new Button(() => ShowCommandPicker(field))
            {
                text = "Pick...",
                tooltip = DesignerLocalization.T("tooltip.binding.pickCommand")
            };

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row.Add(field);
            row.Add(pick);
            return row;
        }

        private static void ShowCommandPicker(TextField field)
        {
            var keys = DesignerCommandKeyScanner.Find();
            var menu = new GenericMenu();
            foreach (var key in keys)
            {
                var captured = key;
                menu.AddItem(new UnityEngine.GUIContent(captured), field.value == captured, () => field.value = captured);
            }
            menu.ShowAsContext();
        }

        private void AddAdvancedOnly(VisualElement field)
        {
            Add(field);
            void Refresh() => field.style.display = DesignerEditMode.IsAdvanced ? DisplayStyle.Flex : DisplayStyle.None;
            Refresh();
            Subscriptions.Add<DesignerMode>(h => DesignerEditMode.Changed += h,
                h => DesignerEditMode.Changed -= h, _ => Refresh());
        }

        private void Change(System.Action<DesignerElementMetadata> change)
        {
            if (_refreshing) return;
            Context.UpdateSelectedElement(change, "Edit NexUI Element Binding");
        }

        private void Refresh()
        {
            _refreshing = true;
            var selected = Context.SelectedMetadata;
            SetEnabled(selected != null);
            if (selected != null)
            {
                _textKey.SetValueWithoutNotify(selected.binding.textKey);
                _valueKey.SetValueWithoutNotify(selected.binding.valueKey);
                _visibilityKey.SetValueWithoutNotify(selected.binding.visibilityKey);
                _classKey.SetValueWithoutNotify(selected.binding.classKey);
                _commandKey.SetValueWithoutNotify(selected.binding.commandKey);
                _interactableKey.SetValueWithoutNotify(selected.binding.interactableKey);
                _textMode.SetValueWithoutNotify(selected.binding.textMode);
                _valueMode.SetValueWithoutNotify(selected.binding.valueMode);
                _textConverterKey.SetValueWithoutNotify(selected.binding.textConverterKey);
                _valueConverterKey.SetValueWithoutNotify(selected.binding.valueConverterKey);
            }
            _refreshing = false;
        }
    }
}

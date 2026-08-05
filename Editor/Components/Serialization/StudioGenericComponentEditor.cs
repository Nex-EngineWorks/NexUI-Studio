using System;
using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Components.Serialization
{
    /// <summary>
    /// The body of a component card for any MonoBehaviour: Unity's own fields, drawn from a real
    /// <see cref="SerializedObject"/>, with object references replaced by a row that can also point at
    /// another element on the screen.
    /// </summary>
    /// <remarks>
    /// Nothing here is written per component type. Adding a field to a user script makes it appear
    /// with the right control, label, range and tooltip, and a <c>PropertyDrawer</c> the user wrote is
    /// used as-is - which is the difference between "the Studio supports these components" and "the
    /// Studio supports your components".
    /// </remarks>
    public sealed class StudioGenericComponentEditor : VisualElement
    {
        private readonly NexUIDesignerContext _context;
        private readonly DesignerElementMetadata _element;
        private readonly string _instanceId;
        private readonly Type _type;
        private readonly SerializedObject _serializedObject;

        /// <summary>Guards the capture pass from reacting to the values it just wrote.</summary>
        private bool _applying;

        public StudioGenericComponentEditor(NexUIDesignerContext context, DesignerElementMetadata element,
            DesignerElementComponent component, Type type)
        {
            _context = context;
            _element = element;
            _instanceId = component?.instanceId;
            _type = type;

            AddToClassList("nexui-generic-component");

            var unsupported = new List<string>();
            _serializedObject = StudioSerializedComponentBridge.Load(component, type, unsupported);
            if (_serializedObject == null)
            {
                Add(Note(string.Format(DesignerLocalization.T("inspector.components.scratchFailed"), type?.Name ?? "?")));
                return;
            }

            BuildFields();
            if (unsupported.Count > 0) Add(UnsupportedNote(unsupported));

            // Values are captured from the object rather than from each control, so a change made by a
            // custom drawer or a multi-field struct is picked up the same way a plain float is.
            this.TrackSerializedObjectValue(_serializedObject, _ => Capture());
            this.Bind(_serializedObject);
        }

        private void BuildFields()
        {
            var iterator = _serializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false; // top level only; PropertyField draws the nesting itself
                if (iterator.propertyPath == StudioSerializedComponentBridge.ScriptProperty) continue;

                var property = iterator.Copy();
                var fieldType = StudioPropertyReflection.FieldTypeOf(property);

                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    StudioReferenceUtility.CanTargetElement(fieldType))
                {
                    Add(new StudioReferenceRow(_context, _element, _instanceId, property, fieldType));
                    continue;
                }

                // UnityEvents are drawn from metadata, not from the scratch object: their call targets
                // are usually elements, which no scratch instance can hold.
                if (StudioUnityEventModel.IsUnityEvent(fieldType))
                {
                    if (StudioUnityEventModel.IsAuthorableEvent(property.propertyPath))
                        Add(new StudioUnityEventRow(_context, _element, _instanceId,
                            property.propertyPath, property.displayName, property.tooltip));

                    continue;
                }

                Add(new PropertyField(property) { name = "prop:" + property.propertyPath });
            }
        }

        /// <summary>Writes everything that differs from the type's defaults back into metadata.</summary>
        private void Capture()
        {
            if (_applying || _context == null || _element == null) return;
            _applying = true;
            try
            {
                _serializedObject.ApplyModifiedPropertiesWithoutUndo();
                _context.UpdateElement(_element, e =>
                {
                    var target = DesignerElementComponentAccess.Find(e, _instanceId);
                    if (target == null) return;
                    StudioSerializedComponentBridge.Capture(_serializedObject, _type, target);
                }, "Edit " + (_type?.Name ?? "Component"));
            }
            finally
            {
                _applying = false;
            }
        }

        private static Label Note(string text)
        {
            var label = new Label(text) { style = { whiteSpace = WhiteSpace.Normal, opacity = 0.7f, fontSize = 11 } };
            label.AddToClassList("nexui-generic-component-note");
            return label;
        }

        private static VisualElement UnsupportedNote(List<string> keys)
            => Note(string.Format(DesignerLocalization.T("inspector.components.valuesPreserved"),
                string.Join(", ", keys)));
    }

    /// <summary>
    /// One object-reference field, able to hold either a project asset or another element on this
    /// screen.
    /// </summary>
    /// <remarks>
    /// Unity's own ObjectField cannot express the element case: at authoring time the target element
    /// has no GameObject yet, so there is nothing to drag in. The reference is stored by the element's
    /// stable id and resolved to a real component only when the prefab is written - which is also what
    /// lets duplication and Definition instancing re-point a copy at its own child.
    /// </remarks>
    internal sealed class StudioReferenceRow : VisualElement
    {
        private readonly NexUIDesignerContext _context;
        private readonly DesignerElementMetadata _element;
        private readonly string _instanceId;
        private readonly string _key;
        private readonly Type _fieldType;
        private readonly Label _value;

        public StudioReferenceRow(NexUIDesignerContext context, DesignerElementMetadata element,
            string instanceId, SerializedProperty property, Type fieldType)
        {
            _context = context;
            _element = element;
            _instanceId = instanceId;
            _key = property.propertyPath;
            _fieldType = fieldType;

            AddToClassList("nexui-reference-row");
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;

            var label = new Label(property.displayName) { tooltip = property.tooltip };
            label.AddToClassList("nexui-reference-label");
            label.style.width = 120;
            Add(label);

            _value = new Label { style = { flexGrow = 1, overflow = Overflow.Hidden } };
            _value.AddToClassList("nexui-reference-value");
            Add(_value);

            var pick = new Button(ShowMenu)
            {
                text = "◉",
                tooltip = string.Format(DesignerLocalization.T("inspector.reference.pickTooltip"),
                    fieldType?.Name ?? "Object")
            };
            pick.AddToClassList("nexui-reference-pick");
            Add(pick);

            Refresh();
        }

        private DesignerObjectReference Current
        {
            get
            {
                var component = DesignerElementComponentAccess.Find(_element, _instanceId);
                return DesignerComponentPropertyBag.Find(component?.properties, _key)?.reference;
            }
        }

        private void Refresh()
        {
            var reference = Current;
            _value.text = StudioReferenceUtility.Describe(reference, _context?.Metadata);

            var missing = reference != null && reference.IsAssigned &&
                          (reference.kind == DesignerReferenceKind.Element
                              ? StudioReferenceUtility.FindElement(_context?.Metadata, reference.stableElementId) == null
                              : StudioReferenceUtility.ResolveAsset(reference) == null);
            _value.EnableInClassList("is-missing", missing);
            _value.EnableInClassList("is-empty", reference == null || !reference.IsAssigned);
        }

        private void ShowMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(DesignerLocalization.T("inspector.reference.none")), false, () => Write(null));
            menu.AddSeparator("");

            var compatible = StudioReferenceUtility.CompatibleElements(_context?.Metadata, _fieldType);
            if (compatible.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent(string.Format(
                    DesignerLocalization.T("inspector.reference.noElements"), _fieldType?.Name ?? "Object")));
            }
            else
            {
                foreach (var (element, componentType) in compatible)
                {
                    var name = string.IsNullOrWhiteSpace(element.displayName) ? element.elementId : element.displayName;
                    var captured = element;
                    var capturedType = componentType;
                    menu.AddItem(
                        new GUIContent(DesignerLocalization.T("inspector.reference.element") + "/" + name),
                        false, () => Write(StudioReferenceUtility.ToElement(captured, capturedType)));
                }
            }

            // Assets are picked through Unity's own picker so the search, previews and favourites all
            // behave the way they do everywhere else in the editor.
            menu.AddSeparator("");
            menu.AddItem(new GUIContent(DesignerLocalization.T("inspector.reference.asset")), false,
                () => StudioAssetPickerBridge.Pick(_fieldType, asset => Write(StudioReferenceUtility.FromAsset(asset))));

            menu.ShowAsContext();
        }

        private void Write(DesignerObjectReference reference)
        {
            _context.UpdateElement(_element, e =>
            {
                var component = DesignerElementComponentAccess.Find(e, _instanceId);
                if (component == null) return;
                component.properties ??= new List<DesignerComponentPropertyEntry>();

                if (reference == null || !reference.IsAssigned)
                {
                    DesignerComponentPropertyBag.Set(component.properties, _key, null);
                    return;
                }

                DesignerComponentPropertyBag.Set(component.properties, _key, new DesignerPropertyValue
                {
                    type = reference.kind == DesignerReferenceKind.Element
                        ? DesignerPropertyValueType.ElementReference
                        : DesignerPropertyValueType.AssetReference,
                    assetValue = reference.kind == DesignerReferenceKind.Asset
                        ? StudioReferenceUtility.ResolveAsset(reference)
                        : null,
                    reference = reference
                });
            }, "Set " + _key);

            Refresh();
        }
    }

    /// <summary>
    /// Runs Unity's object picker from a UIElements callback.
    /// </summary>
    /// <remarks>
    /// <c>EditorGUIUtility.ShowObjectPicker</c> reports its result through IMGUI commands, which never
    /// reach a UIElements button. A one-shot hidden IMGUIContainer bridges the two and removes itself
    /// as soon as the picker closes.
    /// </remarks>
    internal static class StudioAssetPickerBridge
    {
        // GetControlID is only valid inside OnGUI, so the picker gets its own id from a counter that
        // starts well clear of anything IMGUI hands out.
        private static int _nextControlId = 0x5E1EC700;

        public static void Pick(Type fieldType, Action<UnityEngine.Object> onPicked)
        {
            var window = EditorWindow.focusedWindow;
            if (window == null) return;

            var controlId = ++_nextControlId;
            var container = new IMGUIContainer();
            container.style.height = 0;
            container.onGUIHandler = () =>
            {
                var command = Event.current?.commandName;
                if (command != "ObjectSelectorClosed" && command != "ObjectSelectorUpdated") return;
                if (EditorGUIUtility.GetObjectPickerControlID() != controlId) return;
                if (command != "ObjectSelectorClosed") return;

                var picked = EditorGUIUtility.GetObjectPickerObject();
                container.RemoveFromHierarchy();
                onPicked?.Invoke(picked);
            };
            window.rootVisualElement.Add(container);

            EditorGUIUtility.ShowObjectPicker<UnityEngine.Object>(null, false,
                fieldType == null ? string.Empty : "t:" + fieldType.Name, controlId);
        }
    }
}

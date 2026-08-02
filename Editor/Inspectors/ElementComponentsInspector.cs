using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Components.Serialization;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Inspectors
{
    /// <summary>
    /// The element's components, presented the way Unity presents a GameObject's: a stack of cards
    /// with an enable checkbox, a context menu (remove / move / reset) and an Add Component button.
    /// </summary>
    /// <remarks>
    /// Everything a palette preset stamped is shown here and can be removed - that is the point of the
    /// component model. Fields inside each card come from the component type's schema, which for uGUI
    /// and NexUI components is reflected from the real runtime type, so the card shows what Unity's
    /// own inspector would show.
    /// </remarks>
    public sealed class ElementComponentsInspector : DesignerInspectorBase
    {
        private const string CardPrefPrefix = "NexUI.Designer.Inspector.ComponentCard.";

        private readonly VisualElement _host;
        private readonly bool _includeAddButton;
        private bool _writing;

        /// <param name="includeAddButton">
        /// False when the Inspector host draws its own Add Component button under the whole stack,
        /// the way Unity does. True keeps the button attached to this section for standalone use.
        /// </param>
        public ElementComponentsInspector(NexUIDesignerContext context, bool includeAddButton = true)
            : base(context, "inspector.elementComponents")
        {
            _includeAddButton = includeAddButton;
            _host = new VisualElement();
            Add(_host);
            Subscriptions.Add<IReadOnlyList<DesignerElementMetadata>>(
                h => context.MultiSelectionChanged += h, h => context.MultiSelectionChanged -= h, _ => Rebuild());
            Subscriptions.Add<DesignerElementMetadata>(
                h => context.ElementChanged += h, h => context.ElementChanged -= h, _ =>
                {
                    // UpdateElement raises synchronously; rebuilding mid-edit would destroy the field
                    // the user is dragging.
                    if (!_writing) Rebuild();
                });
            Rebuild();
        }

        private DesignerUIComponentFamily Backend =>
            Context.CurrentBackend != null && Context.CurrentBackend.Backend == emiteat.NexUI.Abstractions.UIRenderBackend.UIToolkit
                ? DesignerUIComponentFamily.UIToolkit
                : DesignerUIComponentFamily.UGUI;

        private void Rebuild()
        {
            _host.Clear();
            var element = Context.SelectedMetadata;
            if (element == null) { style.display = DisplayStyle.None; return; }
            style.display = DisplayStyle.Flex;

            // An element authored before the component model (or created by a path that has not been
            // converted yet) is composed on demand rather than shown as empty. UpdateElement raises
            // ElementChanged synchronously, so the guard is what stops that re-entering this method
            // and rebuilding the panel underneath itself.
            if (element.components == null || element.components.Count == 0)
            {
                _writing = true;
                try
                {
                    Context.UpdateElement(element,
                        e => DesignerComponentPresetComposer.Stamp(e, e.elementType, Backend), "Compose Components");
                }
                finally
                {
                    _writing = false;
                }
            }

            _host.Add(PresetHeader(element));

            // UpdateElement can decline (element no longer in the open metadata), so never assume the
            // compose above produced a list.
            foreach (var component in new List<DesignerElementComponent>(
                         element.components ?? new List<DesignerElementComponent>()))
                _host.Add(Card(element, component));

            if (_includeAddButton) _host.Add(AddComponentButton(element));
        }

        /// <summary>
        /// Names the preset the element came from and offers to forget it. "Decompose" changes nothing
        /// structural - the components were always the truth - it just stops labelling the element.
        /// </summary>
        private VisualElement PresetHeader(DesignerElementMetadata element)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };
            var fromPreset = false;
            foreach (var component in element.components ?? new List<DesignerElementComponent>())
                if (component != null && component.fromPreset) { fromPreset = true; break; }

            var descriptor = DesignerComponentRegistry.Get(element.elementType);
            var label = new Label(fromPreset
                ? string.Format(DesignerLocalization.T("inspector.components.fromPreset"), DesignerComponentPalette.DisplayName(descriptor))
                : DesignerLocalization.T("inspector.components.custom"))
            {
                style = { flexGrow = 1, opacity = 0.75f, fontSize = 11 }
            };
            row.Add(label);

            if (!fromPreset) return row;

            var decompose = new Button(() => Context.UpdateElement(element, e =>
            {
                foreach (var component in e.components)
                    if (component != null) component.fromPreset = false;
            }, "Decompose Preset"))
            {
                text = DesignerLocalization.T("inspector.components.decompose"),
                tooltip = DesignerLocalization.T("tooltip.inspector.decompose")
            };
            decompose.AddToClassList("nexui-element-component-decompose");
            row.Add(decompose);
            return row;
        }

        private VisualElement Card(DesignerElementMetadata element, DesignerElementComponent component)
        {
            // The inspector follows the writer: whatever writes the component's values is what draws
            // them, so what the user edits and what lands on the prefab can never be different fields.
            var registered = !Serialization.StudioComponentWriter.OwnedByThisWriter(component);
            var type = DesignerUIComponentRegistry.Get(component.typeId);
            var card = new VisualElement();
            card.AddToClassList("nexui-element-component-card");

            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            var enabled = new Toggle { value = component.enabled, tooltip = DesignerLocalization.T("tooltip.inspector.componentEnabled") };
            enabled.RegisterValueChangedCallback(evt =>
                Context.UpdateElement(element, e =>
                {
                    var target = DesignerElementComponentAccess.Find(e, component.instanceId);
                    if (target != null) target.enabled = evt.newValue;
                }, "Toggle Component"));
            header.Add(enabled);

            // A component with no registry entry is a real MonoBehaviour the user chose, so its name
            // comes from the type rather than from the "unknown component" placeholder descriptor.
            var runtimeType = registered ? null : StudioReferenceUtility.ResolveComponentType(component);

            var prefKey = CardPrefPrefix + component.typeId;
            var foldout = new Foldout
            {
                text = runtimeType != null ? ObjectNames.NicifyVariableName(runtimeType.Name) : Name(type),
                value = EditorPrefs.GetBool(prefKey, true),
                style = { flexGrow = 1 }
            };
            foldout.tooltip = type?.Description;
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == foldout) EditorPrefs.SetBool(prefKey, evt.newValue);
            });

            var menu = new Button(() => ShowCardMenu(element, component, type)) { text = "⋮" };
            menu.AddToClassList("nexui-element-component-menu");
            header.Add(foldout);
            header.Add(menu);
            card.Add(header);

            // Anything without a hand-curated schema - a project script, a plain Unity component - is
            // drawn from its real SerializedObject, so it gets Unity's own fields and drawers.
            if (!registered)
            {
                if (runtimeType == null)
                {
                    foldout.Add(MissingScriptNote(component));
                    return card;
                }
                foldout.Add(new StudioGenericComponentEditor(Context, element, component, runtimeType));
                return card;
            }

            if (type.Properties.Count == 0)
            {
                foldout.Add(new Label(DesignerLocalization.T("inspector.components.noProperties"))
                {
                    style = { opacity = 0.6f, fontSize = 11, marginLeft = 18, whiteSpace = WhiteSpace.Normal }
                });
                return card;
            }

            var showAdvanced = EditorPrefs.GetBool("NexUI.Designer.Inspector.ShowAdvancedProperties", false);
            foreach (var property in type.Properties)
            {
                if (!showAdvanced && property.Exposure == DesignerComponentPropertyExposure.Advanced) continue;
                foldout.Add(Field(element, component, property));
            }

            return card;
        }

        /// <summary>
        /// A script that cannot be resolved right now - renamed, moved, or in an assembly that failed
        /// to compile. The stored values stay untouched so fixing the script brings them all back.
        /// </summary>
        private static VisualElement MissingScriptNote(DesignerElementComponent component)
        {
            var note = new Label(string.Format(DesignerLocalization.T("inspector.components.missingScript"),
                component.assemblyQualifiedTypeName ?? component.typeId,
                component.properties?.Count ?? 0))
            {
                style = { opacity = 0.8f, fontSize = 11, marginLeft = 18, whiteSpace = WhiteSpace.Normal }
            };
            note.AddToClassList("nexui-element-component-missing");
            return note;
        }

        private void ShowCardMenu(DesignerElementMetadata element, DesignerElementComponent component,
            DesignerUIComponentType type)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.component.reset")), false, () =>
                Context.UpdateElement(element, e =>
                    DesignerElementComponentAccess.ResetAll(DesignerElementComponentAccess.Find(e, component.instanceId)),
                    "Reset Component"));

            menu.AddSeparator("");
            menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.component.moveUp")), false, () =>
                Context.UpdateElement(element, e => DesignerElementComponentAccess.Move(e, component.instanceId, -1), "Move Component Up"));
            menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.component.moveDown")), false, () =>
                Context.UpdateElement(element, e => DesignerElementComponentAccess.Move(e, component.instanceId, 1), "Move Component Down"));

            menu.AddSeparator("");
            if (type != null && type.IsEssential)
                menu.AddDisabledItem(new GUIContent(DesignerLocalization.T("ctx.component.remove")));
            else
                menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.component.remove")), false, () =>
                {
                    string blocked = null;
                    Context.UpdateElement(element,
                        e => DesignerElementComponentAccess.Detach(e, component.instanceId, out blocked), "Remove Component");
                    if (!string.IsNullOrEmpty(blocked)) Debug.LogWarning($"[NexUI Studio] {blocked}");
                });

            menu.ShowAsContext();
        }

        private VisualElement AddComponentButton(DesignerElementMetadata element)
        {
            var button = new Button(() => ShowAddComponentMenu(element))
            {
                text = DesignerLocalization.T("inspector.components.add"),
                tooltip = DesignerLocalization.T("tooltip.inspector.addComponent")
            };
            button.AddToClassList("nexui-element-component-add");
            return button;
        }

        /// <summary>
        /// Add Component, grouped by category. Only components that can run on this screen's backend
        /// are listed, and anything currently illegal (already present, conflicting) is shown disabled
        /// with the reason rather than hidden - so the menu explains itself.
        /// </summary>
        private void ShowAddComponentMenu(DesignerElementMetadata element)
        {
            var menu = new GenericMenu();
            PopulateAddComponentMenu(menu, element);
            menu.ShowAsContext();
        }

        /// <summary>
        /// Fills <paramref name="menu"/> with everything attachable to <paramref name="element"/>.
        /// Public so the Inspector host can put these entries under its own Add Component button
        /// alongside the NexUI feature sections, keeping one menu instead of two.
        /// </summary>
        public void PopulateAddComponentMenu(GenericMenu menu, DesignerElementMetadata element)
        {
            if (menu == null || element == null) return;
            var backend = Backend;
            var byCategory = new SortedDictionary<string, List<DesignerUIComponentType>>();

            foreach (var type in DesignerUIComponentRegistry.ForBackend(backend))
            {
                if (type.Category == DesignerUIComponentCategory.Core) continue;
                var key = $"{(int)type.Family}{type.Family}/{type.Category}";
                if (!byCategory.TryGetValue(key, out var list))
                    byCategory[key] = list = new List<DesignerUIComponentType>();
                list.Add(type);
            }

            foreach (var pair in byCategory)
            {
                var path = pair.Key.Substring(1).Replace("NexUIBase", "NexUI Base");
                pair.Value.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
                foreach (var type in pair.Value)
                {
                    var label = new GUIContent($"{path}/{Name(type)}");
                    var blocked = DesignerElementComponentAccess.AttachBlockedReason(element, type.TypeId, backend);
                    if (blocked != null) { menu.AddDisabledItem(label); continue; }

                    var typeId = type.TypeId;
                    menu.AddItem(label, false, () => Context.UpdateElement(element,
                        e => DesignerElementComponentAccess.Attach(e, typeId, backend), "Add Component"));
                }
            }

            // Project scripts live in the same menu rather than in a second panel with its own
            // "Add Component" button - two buttons doing the same thing is what made the Inspector
            // confusing in the first place.
            menu.AddSeparator("");
            menu.AddItem(new GUIContent(DesignerLocalization.T("inspector.components.addScript")), false,
                () => StudioAddComponentPicker.Open(AttachScript,
                    type => DesignerElementComponentAccess.ProjectAttachBlockedReason(element, type)));
        }

        /// <summary>
        /// Attaches a project or engine MonoBehaviour to the same stack every other component lives
        /// in. Nothing about a user script makes it a different kind of thing: it gets an
        /// <c>instanceId</c>, an enable state and a property bag like an Image does.
        /// </summary>
        private void AttachScript(System.Type type)
        {
            var element = Context.SelectedMetadata;
            if (element == null || type == null) return;

            string added = null;
            Context.UpdateElement(element, e =>
            {
                var component = DesignerElementComponentAccess.AttachProject(e, type);
                if (component != null) added = component.instanceId;
            }, "Add Component");

            // Unity expands a freshly added component; matching that means the fields the user came
            // for are already visible instead of behind one more click.
            if (added != null)
                EditorPrefs.SetBool(CardPrefPrefix + DesignerProjectComponentIds.FromQualifiedName(
                    StudioComponentTypeIndex.Identity(type)), true);
            Rebuild();
        }

        private VisualElement Field(DesignerElementMetadata element, DesignerElementComponent component,
            DesignerComponentProperty property)
        {
            var label = property.DisplayName;
            var localized = DesignerLocalization.T(property.LocalizationKey);
            if (localized != property.LocalizationKey) label = localized;

            VisualElement field;
            switch (property.Type)
            {
                case DesignerPropertyValueType.Boolean:
                {
                    var input = new Toggle(label) { value = DesignerElementComponentAccess.GetBool(component, property.Key) };
                    input.RegisterValueChangedCallback(evt => Write(element, component, property, v => v.boolValue = evt.newValue));
                    field = input;
                    break;
                }
                case DesignerPropertyValueType.Integer when property.HasRange:
                {
                    var input = new SliderInt(label, Mathf.RoundToInt(property.Min), Mathf.RoundToInt(property.Max))
                    {
                        value = DesignerElementComponentAccess.GetInt(component, property.Key),
                        showInputField = true
                    };
                    input.RegisterValueChangedCallback(evt => Write(element, component, property, v => v.intValue = evt.newValue));
                    field = input;
                    break;
                }
                case DesignerPropertyValueType.Integer:
                {
                    var input = new IntegerField(label) { value = DesignerElementComponentAccess.GetInt(component, property.Key) };
                    input.RegisterValueChangedCallback(evt => Write(element, component, property, v => v.intValue = evt.newValue));
                    field = input;
                    break;
                }
                case DesignerPropertyValueType.Float when property.HasRange:
                {
                    var input = new Slider(label, property.Min, property.Max)
                    {
                        value = DesignerElementComponentAccess.GetFloat(component, property.Key),
                        showInputField = true
                    };
                    input.RegisterValueChangedCallback(evt => Write(element, component, property, v => v.floatValue = evt.newValue));
                    field = input;
                    break;
                }
                case DesignerPropertyValueType.Float:
                {
                    var input = new FloatField(label) { value = DesignerElementComponentAccess.GetFloat(component, property.Key) };
                    input.RegisterValueChangedCallback(evt => Write(element, component, property, v => v.floatValue = evt.newValue));
                    field = input;
                    break;
                }
                case DesignerPropertyValueType.Color:
                {
                    var input = new UnityEditor.UIElements.ColorField(label)
                        { value = DesignerElementComponentAccess.GetColor(component, property.Key) };
                    input.RegisterValueChangedCallback(evt => Write(element, component, property, v => v.colorValue = evt.newValue));
                    field = input;
                    break;
                }
                case DesignerPropertyValueType.Vector2:
                {
                    var input = new Vector2Field(label) { value = DesignerElementComponentAccess.GetVector2(component, property.Key) };
                    input.RegisterValueChangedCallback(evt => Write(element, component, property, v => v.vector2Value = evt.newValue));
                    field = input;
                    break;
                }
                case DesignerPropertyValueType.Enum:
                {
                    var options = new List<string>(property.EnumOptions ?? System.Array.Empty<string>());
                    if (options.Count == 0) options.Add("Default");
                    var index = Mathf.Clamp(DesignerElementComponentAccess.GetInt(component, property.Key), 0, options.Count - 1);
                    var input = new PopupField<string>(label, options, index);
                    input.RegisterValueChangedCallback(evt =>
                        Write(element, component, property, v => v.intValue = options.IndexOf(evt.newValue)));
                    field = input;
                    break;
                }
                case DesignerPropertyValueType.AssetReference:
                {
                    var input = new UnityEditor.UIElements.ObjectField(label)
                    {
                        objectType = property.AssetType ?? typeof(Object),
                        allowSceneObjects = false,
                        value = DesignerElementComponentAccess.GetAsset(component, property.Key)
                    };
                    input.RegisterValueChangedCallback(evt => Write(element, component, property, v => v.assetValue = evt.newValue));
                    field = input;
                    break;
                }
                default:
                {
                    var input = new TextField(label) { value = DesignerElementComponentAccess.GetString(component, property.Key) };
                    input.RegisterValueChangedCallback(evt => Write(element, component, property, v => v.stringValue = evt.newValue));
                    field = input;
                    break;
                }
            }

            field.AddToClassList("nexui-property-field");
            if (DesignerElementComponentAccess.IsOverridden(component, property.Key))
                field.AddToClassList("is-overridden");
            if (!string.IsNullOrEmpty(property.Description)) field.tooltip = property.Description;
            return field;
        }

        private void Write(DesignerElementMetadata element, DesignerElementComponent component,
            DesignerComponentProperty property, System.Action<DesignerPropertyValue> mutate)
        {
            _writing = true;
            try
            {
                Context.UpdateElement(element, e =>
                {
                    var target = DesignerElementComponentAccess.Find(e, component.instanceId);
                    if (target == null) return;
                    var value = DesignerElementComponentAccess.Value(target, property.Key)?.Clone()
                                ?? new DesignerPropertyValue { type = property.Type };
                    value.type = property.Type;
                    mutate(value);
                    DesignerElementComponentAccess.Set(target, property.Key, value);
                }, "Set " + property.DisplayName);
            }
            finally
            {
                _writing = false;
            }
        }

        private static string Name(DesignerUIComponentType type)
        {
            if (type == null) return "Missing Component";
            if (string.IsNullOrEmpty(type.LocalizationKey)) return type.DisplayName;
            var localized = DesignerLocalization.T(type.LocalizationKey);
            return localized == type.LocalizationKey ? type.DisplayName : localized;
        }
    }
}

using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Components.Definitions;
using emiteat.NexUI.Designer.Editor.Properties;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Inspectors
{
    /// <summary>
    /// Instance-side editing for a reusable component: which definition it points at, its variant
    /// selections, its overrides (with per-property Reset), and the lifecycle actions.
    ///
    /// The section hides itself when the selection is not a component instance, so ordinary elements
    /// see no extra noise. Destructive actions (Swap, Detach) always confirm first - per the project's
    /// rule that a user must never lose authored data to a single click.
    /// </summary>
    public sealed class ComponentInstanceInspector : DesignerInspectorBase
    {
        private readonly VisualElement _body = new VisualElement();
        private readonly Label _empty = new Label("Select a component instance to edit its overrides.");

        public ComponentInstanceInspector(NexUIDesignerContext context) : base(context, "inspector.section.componentInstance")
        {
            _empty.AddToClassList("nexui-inspector-hint");
            Add(_empty);
            Add(_body);

            Subscriptions.Add<DesignerElementMetadata>(h => context.MetadataSelectionChanged += h,
                h => context.MetadataSelectionChanged -= h, _ => Refresh());
            Subscriptions.Add(h => context.CanvasChanged += h, h => context.CanvasChanged -= h, Refresh);
            Refresh();
        }

        private void Refresh()
        {
            _body.Clear();

            var element = Context.SelectedMetadata;
            var reference = element?.componentInstance;
            var isInstance = reference != null && reference.HasReference;
            _empty.style.display = isInstance ? DisplayStyle.None : DisplayStyle.Flex;
            style.display = isInstance ? DisplayStyle.Flex : DisplayStyle.None;
            if (!isInstance) return;

            var definition = DesignerComponentLibrary.Resolve(reference.definitionGuid, reference.definitionId);
            if (definition == null)
            {
                _body.Add(new HelpBox(
                    $"The definition for '{element.elementId}' could not be resolved. Nothing was deleted - restore the asset, " +
                    "or detach the instance to keep the current content as ordinary elements.",
                    HelpBoxMessageType.Error));
                _body.Add(Button("Detach (keep current content)", () => Detach(element)));
                return;
            }

            _body.Add(new Label($"{definition.EffectiveDisplayName}  ·  v{definition.version}"));
            if (reference.detached)
            {
                _body.Add(new HelpBox("This element is detached. It keeps the origin reference for traceability but no longer follows the definition.",
                    HelpBoxMessageType.Info));
                return;
            }

            if (reference.definitionVersion != 0 && reference.definitionVersion != definition.version)
            {
                _body.Add(new HelpBox($"Authored against v{reference.definitionVersion}; the definition is now v{definition.version}.",
                    HelpBoxMessageType.Warning));
                _body.Add(Button("Update From Definition", () => UpdateFromDefinition(element)));
            }

            BuildVariants(element, reference, definition);
            BuildOverrides(element, reference, definition);
            BuildActions(element, definition);
        }

        private void BuildVariants(DesignerElementMetadata element, DesignerComponentInstanceMetadata reference,
            DesignerComponentDefinitionAsset definition)
        {
            if (definition.variantProperties.Count == 0) return;
            _body.Add(new Label("Variants") { name = "SectionTitle" });

            foreach (var property in definition.variantProperties)
            {
                if (property == null || string.IsNullOrEmpty(property.propertyName)) continue;
                var current = reference.GetVariantSelection(property.propertyName) ?? property.EffectiveDefault;
                var label = string.IsNullOrEmpty(property.displayName) ? property.propertyName : property.displayName;

                if (property.type == DesignerComponentVariantPropertyType.Boolean)
                {
                    var toggle = new Toggle(label);
                    toggle.SetValueWithoutNotify(current == "true");
                    toggle.RegisterValueChangedCallback(evt =>
                        SetVariant(element, property.propertyName, evt.newValue ? "true" : "false"));
                    _body.Add(toggle);
                }
                else if (property.type == DesignerComponentVariantPropertyType.Enum && property.options.Count > 0)
                {
                    var choices = new List<string>(property.options);
                    var popup = new PopupField<string>(label, choices, Mathf.Max(0, choices.IndexOf(current)));
                    popup.RegisterValueChangedCallback(evt => SetVariant(element, property.propertyName, evt.newValue));
                    _body.Add(popup);
                }
                else
                {
                    var field = new TextField(label);
                    field.SetValueWithoutNotify(current);
                    field.RegisterValueChangedCallback(evt => SetVariant(element, property.propertyName, evt.newValue));
                    _body.Add(field);
                }
            }
        }

        private void BuildOverrides(DesignerElementMetadata element, DesignerComponentInstanceMetadata reference,
            DesignerComponentDefinitionAsset definition)
        {
            if (definition.exposedProperties.Count == 0 && reference.overrides.Count == 0) return;
            _body.Add(new Label("Exposed Properties") { name = "SectionTitle" });

            foreach (var exposed in definition.exposedProperties)
            {
                if (exposed == null || string.IsNullOrEmpty(exposed.propertyName)) continue;
                var key = "exposed:" + exposed.propertyName;
                var existing = reference.FindOverride(key);
                var label = string.IsNullOrEmpty(exposed.displayName) ? exposed.propertyName : exposed.displayName;

                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                var descriptor = DesignerPropertyRegistry.Get(exposed.propertyId);
                var text = existing != null
                    ? DesignerPropertyRegistry.Serialize(existing.value)
                    : DefinitionValue(definition, exposed);

                var field = new TextField(label) { style = { flexGrow = 1f } };
                field.SetValueWithoutNotify(text);
                field.tooltip = descriptor != null
                    ? $"{descriptor.DisplayName} ({descriptor.ValueType}) → {exposed.targetElementId}"
                    : exposed.propertyId.ToString();
                field.RegisterCallback<BlurEvent>(_ => CommitOverride(element, exposed, field.value));
                row.Add(field);

                if (existing != null)
                {
                    row.Add(new Label("●") { tooltip = "Overridden on this instance." });
                    row.Add(Button("Reset", () => ResetOverride(element, key)));
                }
                _body.Add(row);
            }

            if (reference.overrides.Count > 0)
                _body.Add(Button($"Reset All Overrides ({reference.overrides.Count})", () => ResetAll(element)));
        }

        private void BuildActions(DesignerElementMetadata element, DesignerComponentDefinitionAsset definition)
        {
            _body.Add(new Label("Component") { name = "SectionTitle" });
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row.Add(Button("Edit Definition", () => Selection.activeObject = definition));
            row.Add(Button("Detach", () => Detach(element)));
            row.Add(Button("Update From Definition", () => UpdateFromDefinition(element)));
            _body.Add(row);
        }

        private static string DefinitionValue(DesignerComponentDefinitionAsset definition, DesignerComponentExposedProperty exposed)
        {
            var target = definition.Find(exposed.targetElementId);
            var value = target != null ? DesignerPropertyApplier.Read(target, exposed.propertyId) : null;
            return value != null ? DesignerPropertyRegistry.Serialize(value) : DesignerPropertyRegistry.Serialize(exposed.defaultValue);
        }

        private void CommitOverride(DesignerElementMetadata element, DesignerComponentExposedProperty exposed, string raw)
        {
            if (!DesignerPropertyRegistry.TryParse(exposed.propertyId, raw, out var value, out var error))
            {
                UnityEngine.Debug.LogWarning($"[NexUI Studio] '{raw}' is not a valid {exposed.propertyId} value: {error}");
                Refresh();
                return;
            }

            DesignerComponentService.SetOverride(Context.Metadata, element, new DesignerComponentPropertyOverride
            {
                exposedPropertyName = exposed.propertyName,
                value = value
            });
            AfterChange();
        }

        private void SetVariant(DesignerElementMetadata element, string propertyName, string value)
        {
            Undo.RecordObject(Context.Metadata, "Set NexUI Component Variant");
            element.componentInstance.SetVariantSelection(propertyName, value);
            EditorUtility.SetDirty(Context.Metadata);
            AfterChange();
        }

        private void ResetOverride(DesignerElementMetadata element, string key)
        {
            DesignerComponentService.ResetOverride(Context.Metadata, element, key);
            AfterChange();
        }

        private void ResetAll(DesignerElementMetadata element)
        {
            var count = DesignerComponentService.ResetAllOverrides(Context.Metadata, element);
            if (count > 0) AfterChange();
        }

        private void UpdateFromDefinition(DesignerElementMetadata element)
        {
            var result = DesignerComponentService.UpdateFromDefinition(Context.Metadata, element);
            Report(result);
            AfterChange();
        }

        private void Detach(DesignerElementMetadata element)
        {
            if (!EditorUtility.DisplayDialog("Detach Component Instance",
                    $"'{element.elementId}' will stop following its definition. Its current content is written into this screen " +
                    "as ordinary elements and can be edited freely.\n\nThis can be undone.",
                    "Detach", "Cancel"))
                return;

            var result = DesignerComponentService.Detach(Context.Metadata, element);
            Report(result);
            AfterChange();
        }

        private static void Report(DesignerComponentOperationResult result)
        {
            var message = "[NexUI Studio] " + result.Message;
            foreach (var warning in result.Warnings) message += "\n  • " + warning;
            if (!result.Success) UnityEngine.Debug.LogError(message);
            else if (result.Warnings.Count > 0) UnityEngine.Debug.LogWarning(message);
            else UnityEngine.Debug.Log(message);
        }

        private void AfterChange()
        {
            Context.InvalidateComponentExpansion();
            Context.Validate();
            Refresh();
        }

        private static Button Button(string text, System.Action action)
            => new Button(action) { text = text };
    }
}

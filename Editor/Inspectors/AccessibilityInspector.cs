using emiteat.NexUI.Accessibility;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Inspectors
{
    public sealed class AccessibilityInspector : DesignerInspectorBase
    {
        private readonly TextField _label;
        private readonly EnumField _role;
        private readonly TextField _automationId;
        private bool _refreshing;

        public AccessibilityInspector(NexUIDesignerContext context) : base(context, "inspector.accessibility")
        {
            _label = new TextField("Accessibility Label") { tooltip = DesignerLocalization.T("tooltip.accessibility.label") };
            _role = new EnumField("Role", AccessibilityRole.None) { tooltip = DesignerLocalization.T("tooltip.accessibility.role") };

            // Automation id sits with Role rather than in a section of its own because the two
            // answer the same question - what this element *is* - and Role is the field both a
            // screen reader and a test read. A separate "Testing" section would have duplicated it.
            _automationId = new TextField("Automation ID")
            {
                tooltip = "Stable handle automated tests find this element by, e.g. store.item.purchase. " +
                          "Unlike the element id it is only ever changed deliberately, so renaming " +
                          "elements does not break tests. Leave empty for elements no test needs."
            };

            Add(_label);
            Add(_role);
            Add(_automationId);

            _label.RegisterValueChangedCallback(evt =>
                Change(e => e.accessibilityLabel = evt.newValue));
            _role.RegisterValueChangedCallback(evt =>
                Change(e => e.accessibilityRole = (AccessibilityRole)evt.newValue));
            _automationId.RegisterValueChangedCallback(evt =>
                Change(e => e.automationId = evt.newValue));

            Subscriptions.Add<DesignerElementMetadata>(h => context.MetadataSelectionChanged += h, h => context.MetadataSelectionChanged -= h, _ => Refresh());
            Subscriptions.Add(h => context.CanvasChanged += h, h => context.CanvasChanged -= h, Refresh);
            Refresh();
        }

        private void Change(System.Action<DesignerElementMetadata> change)
        {
            if (_refreshing) return;
            Context.UpdateSelectedElement(change, "Edit NexUI Element Accessibility");
        }

        private void Refresh()
        {
            _refreshing = true;
            var selected = Context.SelectedMetadata;
            SetEnabled(selected != null);
            if (selected != null)
            {
                _label.SetValueWithoutNotify(selected.accessibilityLabel);
                _role.SetValueWithoutNotify(selected.accessibilityRole);
                _automationId.SetValueWithoutNotify(selected.automationId ?? string.Empty);
            }
            _refreshing = false;
        }
    }
}

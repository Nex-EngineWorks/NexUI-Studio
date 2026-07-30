using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using emiteat.NexUI.Designer.Editor.FocusNav;
using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.Panels;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Inspectors
{
    public enum DesignerInspectorExposure
    {
        Essential,
        Common,
        Advanced,
        Diagnostic
    }

    public enum DesignerInspectorWorkflow
    {
        All,
        Build,
        Connect,
        Animate,
        Verify,
        Advanced
    }

    public enum DesignerInspectorTarget
    {
        Screen,
        Element,
        SingleElement,
        MultiElement
    }

    /// <summary>
    /// Declarative registration for one Inspector section. Visibility and discoverability live
    /// here instead of being duplicated by every Inspector host.
    /// </summary>
    public sealed class DesignerInspectorSectionDescriptor
    {
        public string Id { get; }
        public string TitleKey { get; }
        public string Title => DesignerLocalization.T(TitleKey);
        public string Keywords { get; }
        public DesignerInspectorWorkflow Workflow { get; }
        public DesignerInspectorExposure Exposure { get; }
        public DesignerInspectorTarget Target { get; }
        public Func<NexUIDesignerContext, VisualElement> Create { get; }

        public DesignerInspectorSectionDescriptor(
            string id,
            string title,
            string keywords,
            DesignerInspectorWorkflow workflow,
            DesignerInspectorExposure exposure,
            DesignerInspectorTarget target,
            Func<NexUIDesignerContext, VisualElement> create)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Inspector section id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Inspector section title is required.", nameof(title));
            Id = id.Trim();
            TitleKey = title.Trim();
            Keywords = keywords ?? string.Empty;
            Workflow = workflow;
            Exposure = exposure;
            Target = target;
            Create = create ?? throw new ArgumentNullException(nameof(create));
        }

        public bool AppliesTo(NexUIDesignerContext context)
        {
            var count = context.SelectedElements.Count;
            return Target switch
            {
                DesignerInspectorTarget.Screen => count == 0,
                DesignerInspectorTarget.Element => count > 0,
                DesignerInspectorTarget.SingleElement => count == 1,
                DesignerInspectorTarget.MultiElement => count > 1,
                _ => false
            };
        }

        public bool Matches(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            return Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                   || Keywords.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                   || Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>Single source of truth for the sections shown by every NexUI Inspector host.</summary>
    public static class DesignerInspectorRegistry
    {
        private static readonly List<DesignerInspectorSectionDescriptor> Sections = new List<DesignerInspectorSectionDescriptor>();
        private static readonly ReadOnlyCollection<DesignerInspectorSectionDescriptor> ReadOnlySections;

        static DesignerInspectorRegistry()
        {
            // Screen authoring.
            Register("screen", "inspector.section.screen", "definition backend layer identity loading", DesignerInspectorWorkflow.Build,
                DesignerInspectorExposure.Essential, DesignerInspectorTarget.Screen, c => new ScreenDefinitionInspector(c));
            Register("screen-validation", "inspector.section.validation", "errors warnings fix readiness", DesignerInspectorWorkflow.Verify,
                DesignerInspectorExposure.Essential, DesignerInspectorTarget.Screen, c => new ValidationInspector(c));
            Register("policy", "inspector.section.policy", "input pause back cursor time focus conflict lifetime", DesignerInspectorWorkflow.Advanced,
                DesignerInspectorExposure.Advanced, DesignerInspectorTarget.Screen, c => new PolicyInspector(c));

            // Element authoring.
            Register("selection", "inspector.section.selection", "multiple mixed batch common values", DesignerInspectorWorkflow.Build,
                DesignerInspectorExposure.Essential, DesignerInspectorTarget.MultiElement, c => new MultiSelectionInspector(c));
            Register("component", "inspector.section.component", "type states events slots backend support", DesignerInspectorWorkflow.Build,
                DesignerInspectorExposure.Common, DesignerInspectorTarget.SingleElement, c => new ComponentInspector(c));
            Register("attached-components", "inspector.section.attachedComponents", "add component monobehaviour script ugui", DesignerInspectorWorkflow.Build,
                DesignerInspectorExposure.Common, DesignerInspectorTarget.SingleElement, c => new AttachedComponentsInspector(c));
            Register("element-components", "inspector.section.elementComponents", "components add component attach detach ugui toolkit nexui base", DesignerInspectorWorkflow.Build,
                DesignerInspectorExposure.Essential, DesignerInspectorTarget.SingleElement, c => new ElementComponentsInspector(c));
            Register("component-properties", "inspector.section.componentProperties", "properties fields options settings value interaction data", DesignerInspectorWorkflow.Build,
                DesignerInspectorExposure.Essential, DesignerInspectorTarget.SingleElement, c => new ComponentPropertiesInspector(c));
            Register("component-parts", "inspector.section.componentParts", "parts internals content children position size rotation scale transform", DesignerInspectorWorkflow.Build,
                DesignerInspectorExposure.Essential, DesignerInspectorTarget.SingleElement, c => new ComponentPartsInspector(c));
            Register("component-instance", "inspector.section.componentInstance", "reusable definition override variant slot detach swap instance", DesignerInspectorWorkflow.Build,
                DesignerInspectorExposure.Common, DesignerInspectorTarget.SingleElement, c => new ComponentInstanceInspector(c));
            Register("layout", "inspector.section.layout", "position size anchor locked transform", DesignerInspectorWorkflow.Build,
                DesignerInspectorExposure.Essential, DesignerInspectorTarget.Element, c => new LayoutInspector(c));
            Register("auto-layout", "inspector.section.autoLayout", "row column grid gap padding hug fill fixed", DesignerInspectorWorkflow.Build,
                DesignerInspectorExposure.Common, DesignerInspectorTarget.Element, c => new AutoLayoutInspector(c));
            Register("constraints", "inspector.section.constraints", "responsive horizontal vertical pin scale", DesignerInspectorWorkflow.Build,
                DesignerInspectorExposure.Advanced, DesignerInspectorTarget.Element, c => new ConstraintsInspector(c));
            Register("style", "inspector.section.visual", "name id text image sprite color font shape class hidden fill", DesignerInspectorWorkflow.Build,
                DesignerInspectorExposure.Essential, DesignerInspectorTarget.Element, c => new StyleInspector(c));
            Register("accessibility", "inspector.section.accessibility", "label role screen reader touch target", DesignerInspectorWorkflow.Verify,
                DesignerInspectorExposure.Common, DesignerInspectorTarget.Element, c => new AccessibilityInspector(c));

            Register("binding", "inspector.section.binding", "state data text value visibility class command interactable key", DesignerInspectorWorkflow.Connect,
                DesignerInspectorExposure.Essential, DesignerInspectorTarget.Element, c => new BindingInspector(c));
            Register("state", "inspector.section.state", "runtime keys store values preview", DesignerInspectorWorkflow.Connect,
                DesignerInspectorExposure.Common, DesignerInspectorTarget.Element, c => new NexUIDesignerStatePanel(c));
            Register("command", "inspector.section.command", "action handler execute runtime", DesignerInspectorWorkflow.Connect,
                DesignerInspectorExposure.Common, DesignerInspectorTarget.Element, c => new NexUIDesignerCommandPanel(c));
            Register("focus", "inspector.section.focus", "up down left right auto generate keyboard gamepad", DesignerInspectorWorkflow.Connect,
                DesignerInspectorExposure.Advanced, DesignerInspectorTarget.Element, c => new FocusNavigationPanel(c));

            Register("motion", "inspector.section.motion", "clip graph trigger transition hover pressed focus easing", DesignerInspectorWorkflow.Animate,
                DesignerInspectorExposure.Common, DesignerInspectorTarget.Element, c => new MotionInspector(c));
            Register("theme", "inspector.section.theme", "tokens overrides colors classes", DesignerInspectorWorkflow.Animate,
                DesignerInspectorExposure.Advanced, DesignerInspectorTarget.Element, c => new ThemeInspector(c));

            Register("validation", "inspector.section.validation", "errors warnings unsupported backend quick fix", DesignerInspectorWorkflow.Verify,
                DesignerInspectorExposure.Essential, DesignerInspectorTarget.Element, c => new ValidationInspector(c));
            Register("capabilities", "inspector.section.capabilities", "runtime interfaces backend support diagnostic", DesignerInspectorWorkflow.Advanced,
                DesignerInspectorExposure.Diagnostic, DesignerInspectorTarget.Element, c => new CapabilityInspector(c));

            ReadOnlySections = Sections.AsReadOnly();
        }

        public static IReadOnlyList<DesignerInspectorSectionDescriptor> All => ReadOnlySections;

        public static void Register(DesignerInspectorSectionDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (Sections.Exists(x => string.Equals(x.Id, descriptor.Id, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Duplicate NexUI Inspector section id: " + descriptor.Id);
            Sections.Add(descriptor);
        }

        private static void Register(
            string id,
            string title,
            string keywords,
            DesignerInspectorWorkflow workflow,
            DesignerInspectorExposure exposure,
            DesignerInspectorTarget target,
            Func<NexUIDesignerContext, VisualElement> create)
            => Register(new DesignerInspectorSectionDescriptor(id, title, keywords, workflow, exposure, target, create));
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using emiteat.NexUI.Designer.Editor.FocusNav;
using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.Panels;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Inspectors
{
    /// <summary>
    /// Where a section sits in the Inspector, mirroring how Unity stacks a GameObject: the
    /// transform first, then the components, then whatever the object actually has.
    /// </summary>
    public enum DesignerInspectorSlot
    {
        /// <summary>Shown when a screen (not an element) is selected.</summary>
        Screen,

        /// <summary>Pinned directly under the header, like Unity's Transform.</summary>
        Transform,

        /// <summary>Always present for the selection - the element's own identity and look.</summary>
        Core,

        /// <summary>The element's component stack, rendered as top-level cards.</summary>
        Components,

        /// <summary>
        /// Shown only once the element actually uses it, or after the user adds it from
        /// Add Component. This is what keeps the Inspector the length of the element rather than
        /// the length of the feature list.
        /// </summary>
        Feature
    }

    /// <summary>
    /// How prominent a section is. Ordered from most to least commonly needed: everything above
    /// <see cref="Common"/> is hidden unless the user turns on advanced edit mode.
    /// </summary>
    public enum DesignerInspectorExposure
    {
        Essential,
        Common,
        Advanced,
        Diagnostic
    }

    /// <summary>What a section inspects, used to decide whether it applies to the selection.</summary>
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
        public DesignerInspectorSlot Slot { get; }
        public DesignerInspectorExposure Exposure { get; }
        public DesignerInspectorTarget Target { get; }
        public Func<NexUIDesignerContext, VisualElement> Create { get; }

        /// <summary>
        /// For <see cref="DesignerInspectorSlot.Feature"/> sections: whether the current selection
        /// actually uses this feature. Null means "always present once it applies", which is how
        /// every non-feature slot behaves.
        /// </summary>
        public Func<NexUIDesignerContext, bool> IsInUse { get; }

        public DesignerInspectorSectionDescriptor(
            string id,
            string title,
            string keywords,
            DesignerInspectorSlot slot,
            DesignerInspectorExposure exposure,
            DesignerInspectorTarget target,
            Func<NexUIDesignerContext, VisualElement> create,
            Func<NexUIDesignerContext, bool> isInUse = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Inspector section id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Inspector section title is required.", nameof(title));
            Id = id.Trim();
            TitleKey = title.Trim();
            Keywords = keywords ?? string.Empty;
            Slot = slot;
            Exposure = exposure;
            Target = target;
            Create = create ?? throw new ArgumentNullException(nameof(create));
            IsInUse = isInUse;
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

        /// <summary>
        /// True when the section belongs on screen without the user asking for it. A feature the
        /// element does not use is reachable through Add Component instead.
        /// </summary>
        public bool IsInUseBy(NexUIDesignerContext context)
        {
            if (Slot != DesignerInspectorSlot.Feature) return true;
            return IsInUse == null || IsInUse(context);
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
            Register("screen", "inspector.section.screen", "definition backend layer identity loading", DesignerInspectorSlot.Screen,
                DesignerInspectorExposure.Essential, DesignerInspectorTarget.Screen, c => new ScreenDefinitionInspector(c));
            Register("screen-validation", "inspector.section.validation", "errors warnings fix readiness", DesignerInspectorSlot.Screen,
                DesignerInspectorExposure.Essential, DesignerInspectorTarget.Screen, c => new ValidationInspector(c));
            Register("policy", "inspector.section.policy", "input pause back cursor time focus conflict lifetime", DesignerInspectorSlot.Screen,
                DesignerInspectorExposure.Advanced, DesignerInspectorTarget.Screen, c => new PolicyInspector(c));

            // Multi-selection.
            Register("selection", "inspector.section.selection", "multiple mixed batch common values", DesignerInspectorSlot.Core,
                DesignerInspectorExposure.Essential, DesignerInspectorTarget.MultiElement, c => new MultiSelectionInspector(c));

            // Element: the always-present trio - transform, look, component stack.
            Register("layout", "inspector.section.layout", "position size anchor locked transform", DesignerInspectorSlot.Transform,
                DesignerInspectorExposure.Essential, DesignerInspectorTarget.Element, c => new LayoutInspector(c));
            Register("style", "inspector.section.visual", "name id text image sprite color font shape class hidden fill", DesignerInspectorSlot.Core,
                DesignerInspectorExposure.Essential, DesignerInspectorTarget.Element, c => new StyleInspector(c));
            Register("element-components", "inspector.section.elementComponents", "components add component attach detach ugui toolkit nexui base",
                DesignerInspectorSlot.Components, DesignerInspectorExposure.Essential, DesignerInspectorTarget.SingleElement,
                c => new ElementComponentsInspector(c));

            // Element features: on screen only once the element uses them.
            Register("attached-components", "inspector.section.attachedComponents", "add component monobehaviour script ugui", DesignerInspectorSlot.Feature,
                DesignerInspectorExposure.Common, DesignerInspectorTarget.SingleElement, c => new AttachedComponentsInspector(c),
                c => Count(Element(c)?.attachedComponents) > 0);
            Register("component-properties", "inspector.section.componentProperties", "properties fields options settings value interaction data",
                DesignerInspectorSlot.Feature, DesignerInspectorExposure.Common, DesignerInspectorTarget.SingleElement,
                c => new ComponentPropertiesInspector(c),
                c => Count(Element(c)?.componentProperties) > 0 || IsComponentInstance(Element(c)));
            Register("component-parts", "inspector.section.componentParts", "parts internals content children position size rotation scale transform",
                DesignerInspectorSlot.Feature, DesignerInspectorExposure.Common, DesignerInspectorTarget.SingleElement,
                c => new ComponentPartsInspector(c),
                c => Count(Element(c)?.componentPartOverrides) > 0 || IsComponentInstance(Element(c)));
            Register("component-instance", "inspector.section.componentInstance", "reusable definition override variant slot detach swap instance",
                DesignerInspectorSlot.Feature, DesignerInspectorExposure.Common, DesignerInspectorTarget.SingleElement,
                c => new ComponentInstanceInspector(c), c => IsComponentInstance(Element(c)));
            Register("component", "inspector.section.component", "type states events slots backend support", DesignerInspectorSlot.Feature,
                DesignerInspectorExposure.Advanced, DesignerInspectorTarget.SingleElement, c => new ComponentInspector(c));

            Register("auto-layout", "inspector.section.autoLayout", "row column grid gap padding hug fill fixed", DesignerInspectorSlot.Feature,
                DesignerInspectorExposure.Common, DesignerInspectorTarget.Element, c => new AutoLayoutInspector(c),
                c => Element(c)?.autoLayout is { enabled: true });
            Register("constraints", "inspector.section.constraints", "responsive horizontal vertical pin scale", DesignerInspectorSlot.Feature,
                DesignerInspectorExposure.Advanced, DesignerInspectorTarget.Element, c => new ConstraintsInspector(c),
                c => HasConstraint(Element(c)));
            Register("accessibility", "inspector.section.accessibility", "label role screen reader touch target", DesignerInspectorSlot.Feature,
                DesignerInspectorExposure.Common, DesignerInspectorTarget.Element, c => new AccessibilityInspector(c),
                c => Element(c) is { } e && (!string.IsNullOrEmpty(e.accessibilityLabel)
                                             || e.accessibilityRole != emiteat.NexUI.Accessibility.AccessibilityRole.None));

            Register("binding", "inspector.section.binding", "state data text value visibility class command interactable key", DesignerInspectorSlot.Feature,
                DesignerInspectorExposure.Essential, DesignerInspectorTarget.Element, c => new BindingInspector(c),
                c => HasAnyBinding(Element(c)));
            Register("state", "inspector.section.state", "runtime keys store values preview", DesignerInspectorSlot.Feature,
                DesignerInspectorExposure.Common, DesignerInspectorTarget.Element, c => new NexUIDesignerStatePanel(c),
                c => Element(c)?.binding is { } b
                     && (!string.IsNullOrEmpty(b.valueKey) || !string.IsNullOrEmpty(b.visibilityKey)));
            Register("command", "inspector.section.command", "action handler execute runtime", DesignerInspectorSlot.Feature,
                DesignerInspectorExposure.Common, DesignerInspectorTarget.Element, c => new NexUIDesignerCommandPanel(c),
                c => !string.IsNullOrEmpty(Element(c)?.binding?.commandKey));
            Register("focus", "inspector.section.focus", "up down left right auto generate keyboard gamepad", DesignerInspectorSlot.Feature,
                DesignerInspectorExposure.Advanced, DesignerInspectorTarget.Element, c => new FocusNavigationPanel(c),
                c => HasFocusLink(Element(c)));

            Register("motion", "inspector.section.motion", "clip graph trigger transition hover pressed focus easing", DesignerInspectorSlot.Feature,
                DesignerInspectorExposure.Common, DesignerInspectorTarget.Element, c => new MotionInspector(c),
                c => HasMotion(Element(c)));
            Register("theme", "inspector.section.theme", "tokens overrides colors classes", DesignerInspectorSlot.Feature,
                DesignerInspectorExposure.Advanced, DesignerInspectorTarget.Element, c => new ThemeInspector(c),
                c => HasTheme(Element(c)));

            Register("validation", "inspector.section.validation", "errors warnings unsupported backend quick fix", DesignerInspectorSlot.Feature,
                DesignerInspectorExposure.Common, DesignerInspectorTarget.Element, c => new ValidationInspector(c), _ => false);
            Register("capabilities", "inspector.section.capabilities", "runtime interfaces backend support diagnostic", DesignerInspectorSlot.Feature,
                DesignerInspectorExposure.Diagnostic, DesignerInspectorTarget.Element, c => new CapabilityInspector(c));

            ReadOnlySections = Sections.AsReadOnly();
        }

        public static IReadOnlyList<DesignerInspectorSectionDescriptor> All => ReadOnlySections;

        public static DesignerInspectorSectionDescriptor Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var section in Sections)
                if (string.Equals(section.Id, id, StringComparison.OrdinalIgnoreCase)) return section;
            return null;
        }

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
            DesignerInspectorSlot slot,
            DesignerInspectorExposure exposure,
            DesignerInspectorTarget target,
            Func<NexUIDesignerContext, VisualElement> create,
            Func<NexUIDesignerContext, bool> isInUse = null)
            => Register(new DesignerInspectorSectionDescriptor(id, title, keywords, slot, exposure, target, create, isInUse));

        // ---- Feature-in-use predicates ------------------------------------------------------
        // Deliberately metadata-only: deciding whether a section belongs on screen must not build
        // the section, or opening the Inspector would instantiate everything it is trying to skip.

        private static DesignerElementMetadata Element(NexUIDesignerContext context)
            => context.SelectedElements.Count == 1 ? context.SelectedElements[0] : null;

        private static int Count<T>(List<T> list) => list?.Count ?? 0;

        private static bool IsComponentInstance(DesignerElementMetadata element)
            => element?.componentInstance is { } instance
               && (!string.IsNullOrEmpty(instance.definitionGuid) || !string.IsNullOrEmpty(instance.definitionId));

        private static bool HasConstraint(DesignerElementMetadata element)
            => element?.constraint is { } constraint
               && (constraint.horizontal != DesignerConstraintMode.Start || constraint.vertical != DesignerConstraintMode.Start);

        private static bool HasAnyBinding(DesignerElementMetadata element)
            => element?.binding is { } b
               && (!string.IsNullOrEmpty(b.textKey) || !string.IsNullOrEmpty(b.valueKey)
                   || !string.IsNullOrEmpty(b.visibilityKey) || !string.IsNullOrEmpty(b.classKey)
                   || !string.IsNullOrEmpty(b.commandKey) || !string.IsNullOrEmpty(b.interactableKey));

        private static bool HasFocusLink(DesignerElementMetadata element)
            => element?.focus is { } f
               && (f.isDefaultFocus || !string.IsNullOrEmpty(f.upElementId) || !string.IsNullOrEmpty(f.downElementId)
                   || !string.IsNullOrEmpty(f.leftElementId) || !string.IsNullOrEmpty(f.rightElementId));

        private static bool HasMotion(DesignerElementMetadata element)
            => element?.motion is { } m
               && (m.motionPreset != null || !string.IsNullOrEmpty(m.motionId)
                   || !string.IsNullOrEmpty(m.initialVariant) || !string.IsNullOrEmpty(m.animateVariant)
                   || !string.IsNullOrEmpty(m.exitVariant) || !string.IsNullOrEmpty(m.hoverVariant)
                   || !string.IsNullOrEmpty(m.pressedVariant) || !string.IsNullOrEmpty(m.focusVariant));

        private static bool HasTheme(DesignerElementMetadata element)
            => element?.theme is { } t
               && (t.themeRef != null || !string.IsNullOrEmpty(t.themeId)
                   || Count(t.classes) > 0 || Count(t.tokenOverrides) > 0);
    }
}

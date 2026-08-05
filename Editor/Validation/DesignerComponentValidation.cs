using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Components.Definitions;

namespace emiteat.NexUI.Designer.Editor.Validation
{
    /// <summary>
    /// Validation for the reusable-component system. Most rules come free: expanding the screen
    /// already surfaces missing definitions, cycles, unresolved overrides and slot violations, so
    /// this maps <see cref="DesignerComponentExpansionIssue"/>s onto validation codes rather than
    /// re-implementing the checks. The remaining rules are the ones expansion cannot see - a
    /// definition asset that is itself malformed, and instances still marked detached.
    /// </summary>
    public static class DesignerComponentValidation
    {
        /// <param name="variantContext">
        /// Canvas resolution / input mode, so a variant rule conditioned on them is judged the same way
        /// here as on the canvas. Omitted from a caller with no canvas.
        /// </param>
        public static void Validate(DesignerMetadataAsset metadata, string screenId, List<DesignerValidationIssue> issues,
            DesignerComponentVariantContext variantContext = default)
        {
            if (metadata == null) return;
            if (!DesignerComponentExpander.HasInstances(metadata))
            {
                ValidateDanglingReferences(metadata, screenId, issues);
                return;
            }

            var expansion = DesignerComponentExpander.Expand(metadata, DesignerComponentLibrary.Resolver, variantContext);
            try
            {
                foreach (var issue in expansion.Issues)
                    issues.Add(new DesignerValidationIssue(SeverityOf(issue.Kind), CodeOf(issue.Kind),
                        issue.Message, issue.Fix, screenId, issue.InstanceElementId));
            }
            finally
            {
                expansion.Dispose();
            }

            ValidateDanglingReferences(metadata, screenId, issues);
            ValidateRecoveredReferences(metadata, screenId, issues);
            ValidateDefinitions(metadata, screenId, issues);
        }

        /// <summary>
        /// Reports instances whose stored GUID no longer resolves but whose <c>componentId</c> does -
        /// the library recovered them silently (a definition moved between projects, or its .meta was
        /// regenerated). Worth surfacing once: re-saving the screen writes the new GUID back, and
        /// until then the recovery repeats on every load.
        /// </summary>
        private static void ValidateRecoveredReferences(DesignerMetadataAsset metadata, string screenId, List<DesignerValidationIssue> issues)
        {
            foreach (var element in metadata.elements)
            {
                var reference = element?.componentInstance;
                if (reference == null || !reference.IsInstance) continue;
                if (string.IsNullOrEmpty(reference.definitionGuid) || string.IsNullOrEmpty(reference.definitionId)) continue;

                if (DesignerComponentLibrary.Resolve(reference.definitionGuid, null) != null) continue;
                var byId = DesignerComponentLibrary.Resolve(null, reference.definitionId);
                if (byId == null) continue;   // genuinely missing - the expander already reported it

                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Info, "component-definition-recovered",
                    $"'{element.elementId}' was recovered by componentId: its stored asset GUID no longer exists but '{byId.EffectiveDisplayName}' matches.",
                    "Save the screen to store the new GUID, or re-pick the component to make the reference explicit.",
                    screenId, element.elementId) { Asset = byId });
            }
        }

        /// <summary>An element marked detached still carries its origin reference; that is intentional, but a broken origin is worth reporting once.</summary>
        private static void ValidateDanglingReferences(DesignerMetadataAsset metadata, string screenId, List<DesignerValidationIssue> issues)
        {
            foreach (var element in metadata.elements)
            {
                var reference = element?.componentInstance;
                if (reference == null || !reference.detached || !reference.HasReference) continue;
                if (DesignerComponentLibrary.Resolve(reference.definitionGuid, reference.definitionId) != null) continue;
                issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Info, "component-origin-missing",
                    $"'{element.elementId}' was detached from a component definition that no longer exists.",
                    "Clear the origin reference if you no longer need the provenance, or restore the definition asset.",
                    screenId, element.elementId));
            }
        }

        /// <summary>Checks each definition an open screen depends on for contract mistakes the author would otherwise only find at runtime.</summary>
        private static void ValidateDefinitions(DesignerMetadataAsset metadata, string screenId, List<DesignerValidationIssue> issues)
        {
            var seen = new HashSet<string>();
            foreach (var element in metadata.elements)
            {
                var reference = element?.componentInstance;
                if (reference == null || !reference.IsInstance) continue;
                var definition = DesignerComponentLibrary.Resolve(reference.definitionGuid, reference.definitionId);
                if (definition == null || !seen.Add(definition.componentId ?? definition.name)) continue;

                var slotIds = new HashSet<string>();
                foreach (var slot in definition.slots)
                {
                    if (slot == null) continue;
                    if (string.IsNullOrEmpty(slot.slotId))
                        issues.Add(Definition(definition, "component-slot-empty-id",
                            "declares a slot with an empty id.", "Give every slot a unique id.", screenId, element.elementId));
                    else if (!slotIds.Add(slot.slotId))
                        issues.Add(Definition(definition, "component-slot-duplicate-id",
                            $"declares slot '{slot.slotId}' more than once.", "Remove the duplicate slot.", screenId, element.elementId));

                    if (!string.IsNullOrEmpty(slot.hostElementId) && definition.Find(slot.hostElementId) == null)
                        issues.Add(Definition(definition, "component-slot-host-missing",
                            $"slot '{slot.slotId}' points at element '{slot.hostElementId}', which does not exist.",
                            "Point the slot at an existing element, or clear the host so it uses the root.", screenId, element.elementId));
                }

                var exposedNames = new HashSet<string>();
                foreach (var exposed in definition.exposedProperties)
                {
                    if (exposed == null) continue;
                    if (string.IsNullOrEmpty(exposed.propertyName))
                        issues.Add(Definition(definition, "component-exposed-empty-name",
                            "exposes a property with no name.", "Name every exposed property.", screenId, element.elementId));
                    else if (!exposedNames.Add(exposed.propertyName))
                        issues.Add(Definition(definition, "component-exposed-duplicate-name",
                            $"exposes '{exposed.propertyName}' more than once.", "Remove the duplicate.", screenId, element.elementId));

                    // Through the stable id, so an element the author merely renamed does not read as
                    // a missing one - that is the whole point of recording the identity.
                    if (definition.ResolveTarget(exposed.targetStableId, exposed.targetElementId) == null)
                        issues.Add(Definition(definition, "component-exposed-target-missing",
                            $"exposed property '{exposed.propertyName}' targets element '{exposed.targetElementId}', which does not exist.",
                            "Retarget the exposed property, or restore the element.", screenId, element.elementId));

                    if (exposed.propertyId == DesignerPropertyId.None)
                        issues.Add(Definition(definition, "component-exposed-no-property",
                            $"exposed property '{exposed.propertyName}' has no property id.",
                            "Pick the property it should write.", screenId, element.elementId));
                }

                foreach (var rule in definition.variantRules)
                {
                    if (rule == null) continue;
                    if (definition.FindVariantProperty(rule.propertyName) == null)
                        issues.Add(Definition(definition, "component-variant-rule-orphan",
                            $"has a variant rule for '{rule.propertyName}', which is not a declared variant property.",
                            "Declare the variant property, or delete the rule.", screenId, element.elementId));
                }

                if (definition.Root == null)
                    issues.Add(Definition(definition, "component-definition-empty",
                        "has no root element.", "Add at least one element to the definition.", screenId, element.elementId));
            }
        }

        private static DesignerValidationIssue Definition(DesignerComponentDefinitionAsset definition, string code,
            string message, string fix, string screenId, string elementId)
            => new DesignerValidationIssue(DesignerValidationSeverity.Warning, code,
                $"Component '{definition.EffectiveDisplayName}' {message}", fix, screenId, elementId)
            { Asset = definition };

        private static DesignerValidationSeverity SeverityOf(DesignerComponentExpansionIssueKind kind)
        {
            switch (kind)
            {
                case DesignerComponentExpansionIssueKind.MissingDefinition:
                case DesignerComponentExpansionIssueKind.CircularReference:
                case DesignerComponentExpansionIssueKind.EmptyDefinition:
                case DesignerComponentExpansionIssueKind.BudgetExceeded:
                    return DesignerValidationSeverity.Error;
                case DesignerComponentExpansionIssueKind.RecoveredByComponentId:
                // Not a mistake in the screen: a headless caller simply has no canvas to judge a
                // resolution-conditioned rule against. The canvas and the save both do.
                case DesignerComponentExpansionIssueKind.MissingVariantContext:
                    return DesignerValidationSeverity.Info;
                default:
                    return DesignerValidationSeverity.Warning;
            }
        }

        private static string CodeOf(DesignerComponentExpansionIssueKind kind)
        {
            switch (kind)
            {
                case DesignerComponentExpansionIssueKind.MissingDefinition:          return "component-definition-missing";
                case DesignerComponentExpansionIssueKind.RecoveredByComponentId:     return "component-definition-recovered";
                case DesignerComponentExpansionIssueKind.CircularReference:          return "component-cycle";
                case DesignerComponentExpansionIssueKind.UnknownSlot:                return "component-slot-unknown";
                case DesignerComponentExpansionIssueKind.SlotRejectedType:           return "component-slot-type-rejected";
                case DesignerComponentExpansionIssueKind.RequiredSlotEmpty:          return "component-slot-required-empty";
                case DesignerComponentExpansionIssueKind.SlotChildCountOutOfRange:   return "component-slot-count";
                case DesignerComponentExpansionIssueKind.UnresolvedOverride:         return "component-override-unresolved";
                case DesignerComponentExpansionIssueKind.UnappliedOverride:          return "component-override-unapplied";
                case DesignerComponentExpansionIssueKind.UnknownVariantProperty:     return "component-variant-unknown";
                case DesignerComponentExpansionIssueKind.UnknownVariantValue:        return "component-variant-value-unknown";
                case DesignerComponentExpansionIssueKind.MissingVariantContext:      return "component-variant-context-missing";
                case DesignerComponentExpansionIssueKind.VersionMismatch:            return "component-version-mismatch";
                case DesignerComponentExpansionIssueKind.EmptyDefinition:            return "component-definition-empty";
                case DesignerComponentExpansionIssueKind.BudgetExceeded:             return "component-expansion-budget";
                default:                                                             return "component-issue";
            }
        }
    }
}

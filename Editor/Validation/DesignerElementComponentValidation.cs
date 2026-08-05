using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Components;

namespace emiteat.NexUI.Designer.Editor.Validation
{
    /// <summary>
    /// Validates the components attached to each element against the screen's backend.
    /// </summary>
    /// <remarks>
    /// Attaching already refuses illegal combinations, so these rules exist for the cases attaching
    /// cannot see: the screen's backend was switched afterwards, a component was removed that another
    /// still requires, or the file was edited outside the Designer. The backend mismatch is the one
    /// that actually bites - switching a screen from uGUI to UI Toolkit silently leaves every uGUI
    /// component behind, and without this the first sign is a Save Report full of "not written".
    /// </remarks>
    public static class DesignerElementComponentValidation
    {
        /// <summary>Components that have a direct counterpart on the other backend, for the auto fix.</summary>
        private static readonly Dictionary<string, string> Equivalents = new Dictionary<string, string>
        {
            { "UGUI.Image", "UITK.Image" }, { "UITK.Image", "UGUI.Image" },
            { "UGUI.TextMeshProUGUI", "UITK.Label" }, { "UGUI.Text", "UITK.Label" },
            { "UITK.Label", "UGUI.TextMeshProUGUI" },
            { "UGUI.Button", "UITK.Button" }, { "UITK.Button", "UGUI.Button" },
            { "UGUI.Toggle", "UITK.Toggle" }, { "UITK.Toggle", "UGUI.Toggle" },
            { "UGUI.Slider", "UITK.Slider" }, { "UITK.Slider", "UGUI.Slider" },
            { "UGUI.Scrollbar", "UITK.Scroller" }, { "UITK.Scroller", "UGUI.Scrollbar" },
            { "UGUI.TMP_Dropdown", "UITK.DropdownField" }, { "UITK.DropdownField", "UGUI.TMP_Dropdown" },
            { "UGUI.TMP_InputField", "UITK.TextField" }, { "UITK.TextField", "UGUI.TMP_InputField" },
            { "UGUI.ScrollRect", "UITK.ScrollView" }, { "UITK.ScrollView", "UGUI.ScrollRect" }
        };

        public static void Validate(DesignerMetadataAsset metadata, string screenId,
            DesignerUIComponentFamily backend, List<DesignerValidationIssue> issues)
        {
            if (metadata == null) return;

            foreach (var element in metadata.elements)
            {
                if (element?.components == null || element.components.Count == 0) continue;
                var present = new HashSet<string>();

                foreach (var component in element.components)
                {
                    if (component == null || string.IsNullOrEmpty(component.typeId)) continue;
                    present.Add(component.typeId);

                    var type = DesignerUIComponentRegistry.Get(component.typeId);
                    if (type == null) continue;

                    if (!type.SupportsBackend(backend))
                        issues.Add(BackendMismatch(element, component, type, backend, screenId));
                }

                ValidateRequirements(element, present, screenId, issues);
                ValidateConflicts(element, present, screenId, issues);
                ValidateReferencesAndEvents(metadata, element, screenId, issues);
            }
        }

        /// <summary>
        /// Checks the two things that fail silently at runtime: a reference pointing at nothing, and a
        /// UnityEvent naming a method that no longer exists.
        /// </summary>
        /// <remarks>
        /// Both survive a save without an error - the prefab is written, the game runs, and the button
        /// simply does nothing. Reporting them at edit time is the only point at which they are cheap
        /// to find.
        /// </remarks>
        private static void ValidateReferencesAndEvents(DesignerMetadataAsset metadata,
            DesignerElementMetadata element, string screenId, List<DesignerValidationIssue> issues)
        {
            foreach (var component in element.components)
            {
                if (component?.properties == null) continue;
                var componentName = ComponentName(component);

                foreach (var entry in component.properties)
                {
                    var reference = entry?.value?.reference;
                    if (reference == null || !reference.IsAssigned) continue;

                    if (reference.kind == DesignerReferenceKind.Element &&
                        emiteat.NexUI.Designer.Editor.Components.Serialization.StudioReferenceUtility.FindElement(metadata, reference.stableElementId) == null)
                    {
                        issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error,
                            "NEXUI-REFERENCE-MISSING",
                            $"'{element.elementId}' {componentName}.{entry.key} points at an element that is not on this screen.",
                            "Assign the field again, or restore the element it used to point at.",
                            screenId, element.elementId));
                    }
                    else if (reference.kind == DesignerReferenceKind.Asset &&
                             emiteat.NexUI.Designer.Editor.Components.Serialization.StudioReferenceUtility.ResolveAsset(reference) == null)
                    {
                        issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error,
                            "NEXUI-REFERENCE-MISSING",
                            $"'{element.elementId}' {componentName}.{entry.key} points at an asset that no longer exists.",
                            "Assign the field again, or restore the deleted asset.", screenId, element.elementId));
                    }
                }

                ValidateEvents(metadata, element, component, componentName, screenId, issues);
            }
        }

        private static void ValidateEvents(DesignerMetadataAsset metadata, DesignerElementMetadata element,
            DesignerElementComponent component, string componentName, string screenId,
            List<DesignerValidationIssue> issues)
        {
            foreach (var eventKey in EventKeys(component))
            {
                var calls = emiteat.NexUI.Designer.Editor.Components.Serialization.StudioUnityEventModel.Read(component, eventKey);
                for (var i = 0; i < calls.Count; i++)
                {
                    var call = calls[i];
                    if (!call.Target.IsAssigned)
                    {
                        issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning,
                            "NEXUI-EVENT-NO-TARGET",
                            $"'{element.elementId}' {componentName}.{eventKey} listener {i + 1} has no target and will never run.",
                            "Pick a target element, or remove the listener.", screenId, element.elementId));
                        continue;
                    }

                    var targetType = call.Target.kind == DesignerReferenceKind.Asset
                        ? emiteat.NexUI.Designer.Editor.Components.Serialization.StudioReferenceUtility.ResolveAsset(call.Target)?.GetType()
                        : Components.StudioComponentTypeIndex.Resolve(call.Target.componentTypeName);

                    if (targetType == null) continue; // the missing reference above already covers it

                    // A listener with no method chosen is unconfigured, not broken. Unity's own
                    // event editor produces exactly this the moment a row is added, so treating it
                    // as an error blocks Save on a half-finished edit - and the message reads
                    // "calls '', which X does not have", which describes nothing the user can act on.
                    // It gets the same severity as the sibling no-target case: it will never run.
                    if (string.IsNullOrEmpty(call.MethodName))
                    {
                        issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning,
                            "NEXUI-EVENT-NO-METHOD",
                            $"'{element.elementId}' {componentName}.{eventKey} listener {i + 1} has no method selected and will never run.",
                            "Pick a method, or remove the listener.", screenId, element.elementId));
                        continue;
                    }

                    if (!emiteat.NexUI.Designer.Editor.Components.Serialization.StudioUnityEventModel.MethodExists(call, targetType))
                        issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Error,
                            "NEXUI-EVENT-MISSING-METHOD",
                            $"'{element.elementId}' {componentName}.{eventKey} listener {i + 1} calls '{call.MethodName}', which {targetType.Name} does not have.",
                            "Pick the method again, or restore the one that was renamed or removed.",
                            screenId, element.elementId));
                }
            }
        }

        /// <summary>
        /// UnityEvent field names on a component, read back out of the stored property paths - the
        /// validator has no scratch object to reflect over.
        /// </summary>
        private static IEnumerable<string> EventKeys(DesignerElementComponent component)
        {
            const string marker = ".m_PersistentCalls.m_Calls.Array.size";
            var keys = new HashSet<string>();
            foreach (var entry in component.properties)
            {
                if (entry?.key == null || !entry.key.EndsWith(marker, System.StringComparison.Ordinal)) continue;

                var key = entry.key.Substring(0, entry.key.Length - marker.Length);

                // Screens authored before engine-internal events were excluded still carry them in
                // their metadata, so the filter has to run here too rather than only at capture.
                if (!emiteat.NexUI.Designer.Editor.Components.Serialization.StudioUnityEventModel.IsAuthorableEvent(key))
                    continue;

                keys.Add(key);
            }
            return keys;
        }

        private static string ComponentName(DesignerElementComponent component)
        {
            var type = DesignerUIComponentRegistry.Get(component.typeId);
            if (type != null && DesignerUIComponentRegistry.IsRegistered(component.typeId)) return type.DisplayName;
            return DesignerProjectComponentIds.ShortName(component.typeId);
        }

        private static DesignerValidationIssue BackendMismatch(DesignerElementMetadata element,
            DesignerElementComponent component, DesignerUIComponentType type,
            DesignerUIComponentFamily backend, string screenId)
        {
            var replacement = Replacement(component.typeId, backend);
            var fix = replacement != null
                ? $"Replace it with {DesignerUIComponentRegistry.Get(replacement)?.DisplayName ?? replacement}, which does the same job on this backend."
                : "Remove it, or move this screen back to the backend it was authored for. No equivalent component exists on this backend.";

            return new DesignerValidationIssue(DesignerValidationSeverity.Warning, "NEXUI-COMPONENT-BACKEND",
                $"'{element.elementId}' has {type.DisplayName}, which does not run on a {backend} screen and will not be written.",
                fix, screenId, element.elementId);
        }

        private static void ValidateRequirements(DesignerElementMetadata element, HashSet<string> present,
            string screenId, List<DesignerValidationIssue> issues)
        {
            foreach (var typeId in present)
            {
                var type = DesignerUIComponentRegistry.Get(typeId);
                if (type == null) continue;
                foreach (var required in type.RequiredComponents)
                {
                    if (present.Contains(required)) continue;
                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "NEXUI-COMPONENT-REQUIRED",
                        $"'{element.elementId}' has {type.DisplayName} but not {DesignerUIComponentRegistry.Get(required)?.DisplayName ?? required}, which it requires.",
                        "Add the required component, or remove the one that needs it.", screenId, element.elementId));
                }
            }
        }

        private static void ValidateConflicts(DesignerElementMetadata element, HashSet<string> present,
            string screenId, List<DesignerValidationIssue> issues)
        {
            var reported = new HashSet<string>();
            foreach (var typeId in present)
            {
                var type = DesignerUIComponentRegistry.Get(typeId);
                if (type == null) continue;

                foreach (var conflict in type.ConflictsWith)
                {
                    if (conflict == typeId || !present.Contains(conflict)) continue;
                    // One issue per pair, not one per direction.
                    var key = string.CompareOrdinal(typeId, conflict) < 0 ? typeId + "|" + conflict : conflict + "|" + typeId;
                    if (!reported.Add(key)) continue;

                    issues.Add(new DesignerValidationIssue(DesignerValidationSeverity.Warning, "NEXUI-COMPONENT-CONFLICT",
                        $"'{element.elementId}' has both {type.DisplayName} and {DesignerUIComponentRegistry.Get(conflict)?.DisplayName ?? conflict}, which cannot work together.",
                        "Remove one of them.", screenId, element.elementId));
                }
            }
        }

        /// <summary>The counterpart of <paramref name="typeId"/> on <paramref name="backend"/>, if one exists.</summary>
        public static string Replacement(string typeId, DesignerUIComponentFamily backend)
        {
            if (!Equivalents.TryGetValue(typeId, out var candidate)) return null;
            var type = DesignerUIComponentRegistry.Get(candidate);
            return type != null && type.SupportsBackend(backend) ? candidate : null;
        }

        /// <summary>
        /// Swaps every component that cannot run on <paramref name="backend"/> for its counterpart,
        /// carrying across the values whose keys both sides share. Components with no counterpart are
        /// left alone and reported - silently deleting a user's work would be worse than a warning.
        /// </summary>
        public static int ReplaceUnsupported(DesignerMetadataAsset metadata, DesignerUIComponentFamily backend,
            out int unresolved)
        {
            var replaced = 0;
            unresolved = 0;
            if (metadata == null) return 0;

            foreach (var element in metadata.elements)
            {
                if (element?.components == null) continue;
                foreach (var component in element.components)
                {
                    if (component == null || string.IsNullOrEmpty(component.typeId)) continue;
                    var type = DesignerUIComponentRegistry.Get(component.typeId);
                    if (type == null || type.SupportsBackend(backend)) continue;

                    var replacement = Replacement(component.typeId, backend);
                    if (replacement == null) { unresolved++; continue; }

                    component.typeId = replacement;
                    AdoptReplacementValueFormat(component, replacement);
                    PruneUnknownValues(component);
                    replaced++;
                }
            }
            return replaced;
        }

        /// <summary>
        /// Puts the entry back on the value path its new type actually has.
        /// </summary>
        /// <remarks>
        /// A uGUI entry is stored by Unity property path because there is a real component behind it. A
        /// UI Toolkit control has no <c>System.Type</c> at all - it is generated into UXML - so leaving
        /// the entry on the property-path writer after a backend swap would make it look like a script
        /// the build cannot resolve, and the save would report a missing component instead of writing
        /// the control.
        /// </remarks>
        private static void AdoptReplacementValueFormat(DesignerElementComponent component, string replacement)
        {
            if (DesignerUIComponentRegistry.Get(replacement)?.BackingType != null)
            {
                component.valueFormat = DesignerComponentValueFormat.PropertyPath;
                component.adoptExistingComponent = true;
                return;
            }
            component.valueFormat = DesignerComponentValueFormat.SchemaKeys;
            component.adoptExistingComponent = false;
            component.assemblyQualifiedTypeName = null;
        }

        /// <summary>Drops values the new component type has no field for, so nothing silently misapplies.</summary>
        private static void PruneUnknownValues(DesignerElementComponent component)
        {
            if (component.properties == null) return;
            for (var i = component.properties.Count - 1; i >= 0; i--)
            {
                var entry = component.properties[i];
                if (entry != null && DesignerElementComponentAccess.Schema(component.typeId, entry.key) != null) continue;
                component.properties.RemoveAt(i);
            }
        }
    }
}

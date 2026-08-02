using System;
using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Components.Serialization;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Serialization
{
    /// <summary>
    /// Materializes the components that have no entry in the Studio's own registry - the user's
    /// project scripts and plain Unity components - onto the generated uGUI GameObject, values and
    /// references included.
    /// </summary>
    /// <remarks>
    /// This replaces the older "attached components" pass, which created the MonoBehaviour and then
    /// stopped: a script attached from the Studio arrived on the prefab with every field at its
    /// default, which made the feature unusable for anything that needs wiring.
    ///
    /// Three rules hold the ownership story together:
    /// <list type="bullet">
    /// <item>Only components this writer created are ever destroyed, and only when their stack entry
    /// is gone. A component the user added to the prefab by hand is never touched.</item>
    /// <item>Ownership is keyed by <see cref="DesignerElementComponent.instanceId"/>, so two
    /// components of the same type on one element stay distinguishable.</item>
    /// <item>Nothing is written when the value already matches, so saving the same metadata twice
    /// leaves the prefab clean instead of dirtying it every time.</item>
    /// </list>
    /// </remarks>
    internal static class StudioComponentWriter
    {
        /// <summary>Resolves an element's stable id to the object being written for it.</summary>
        public delegate GameObject ElementResolver(string stableElementId);

        /// <summary>
        /// Pass one: make the object's Studio-owned component stack match the metadata.
        /// </summary>
        /// <remarks>
        /// Creation is separated from writing values because a reference can point at any element on
        /// the screen. Every component on every element has to exist before the first one is wired, or
        /// a field pointing "forward" in the element list would resolve to nothing.
        /// </remarks>
        public static void EnsureComponents(GameObject go, DesignerElementMetadata element,
            DesignerSaveReport report)
        {
            if (go == null || element == null) return;

            var tracker = go.GetComponent<DesignerAttachedComponentTracker>();
            tracker?.Prune();

            var desired = Desired(element, report);
            tracker = Reconcile(go, element, desired, tracker, report);
            ApplyComponentOrder(go, desired, tracker, report, element.elementId);

            if (tracker != null && tracker.IsEmpty) UnityEngine.Object.DestroyImmediate(tracker);
        }

        /// <summary>Pass two: write the authored values and resolve every reference.</summary>
        public static void ApplyValues(GameObject go, DesignerElementMetadata element,
            DesignerSaveReport report, ElementResolver resolveElement)
        {
            if (go == null || element == null) return;

            var tracker = go.GetComponent<DesignerAttachedComponentTracker>();
            if (tracker == null) return;

            // The unresolvable-type warnings were already reported by EnsureComponents; passing null
            // here keeps the second pass from duplicating every one of them.
            foreach (var (component, type) in Desired(element, report: null))
            {
                var behaviour = tracker.Find(component.instanceId);
                if (behaviour == null) continue;

                if (behaviour is Behaviour toggleable && toggleable.enabled != component.enabled)
                {
                    toggleable.enabled = component.enabled;
                    report.MarkChanged($"Set {type.Name}.enabled on '{element.elementId}'");
                }

                ApplyValues(behaviour, component, element, report, resolveElement);
            }
        }

        /// <summary>
        /// Whether this writer owns <paramref name="component"/>, rather than the registry-backed
        /// <see cref="UGUIComponentWriter"/>.
        /// </summary>
        /// <remarks>
        /// Ownership follows the value format, not the type: an <c>Image</c> brought in by Prefab
        /// Import carries every one of its serialized fields by property path and has to be written
        /// that way, while an <c>Image</c> stamped by a palette preset carries curated schema keys.
        /// A component with no registry entry at all is always ours whatever its format says.
        /// </remarks>
        public static bool OwnedByThisWriter(DesignerElementComponent component)
        {
            if (component == null) return false;
            if (component.valueFormat == DesignerComponentValueFormat.PropertyPath) return true;
            return !DesignerUIComponentRegistry.IsRegistered(component.typeId);
        }

        // ---- What the metadata asks for ---------------------------------------------------------

        private static List<(DesignerElementComponent Component, Type Type)> Desired(
            DesignerElementMetadata element, DesignerSaveReport report)
        {
            var desired = new List<(DesignerElementComponent, Type)>();
            foreach (var component in element.components ?? new List<DesignerElementComponent>())
            {
                if (component == null || string.IsNullOrEmpty(component.typeId)) continue;
                if (!OwnedByThisWriter(component)) continue;

                var type = StudioReferenceUtility.ResolveComponentType(component);
                if (type == null || !typeof(Component).IsAssignableFrom(type) ||
                    type.IsAbstract || type.ContainsGenericParameters)
                {
                    report?.MarkUnsupported("Component",
                        $"'{element.elementId}' references unavailable component '{component.assemblyQualifiedTypeName ?? component.typeId}'. " +
                        $"{component.properties?.Count ?? 0} stored value(s) were preserved.", element.elementId);
                    continue;
                }
                desired.Add((component, type));
            }
            return desired;
        }

        // ---- Ownership ---------------------------------------------------------------------------

        /// <summary>
        /// Brings the object's Studio-owned components in line with the stack: adds what is missing,
        /// adopts pre-identity tracked components, and removes only what the Studio itself created.
        /// </summary>
        private static DesignerAttachedComponentTracker Reconcile(GameObject go,
            DesignerElementMetadata element, List<(DesignerElementComponent Component, Type Type)> desired,
            DesignerAttachedComponentTracker tracker, DesignerSaveReport report)
        {
            var wanted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (component, _) in desired) wanted.Add(component.instanceId);

            if (tracker != null)
            {
                tracker.Prune();
                for (var i = tracker.managedByInstance.Count - 1; i >= 0; i--)
                {
                    var entry = tracker.managedByInstance[i];
                    if (entry?.component == null) { tracker.managedByInstance.RemoveAt(i); continue; }
                    if (wanted.Contains(entry.instanceId)) continue;

                    var removed = entry.component;
                    var name = removed.GetType().Name;
                    var ownership = entry.ownership;
                    tracker.Forget(entry.component);
                    if (ownership == DesignerManagedComponentOwnership.Owned)
                    {
                        UnityEngine.Object.DestroyImmediate(removed);
                        report.MarkChanged($"Removed Studio-owned {name} from '{element.elementId}'");
                    }
                    else
                    {
                        report.MarkUserImpact(name,
                            $"Preserved imported {name} on '{element.elementId}' after its Studio entry was removed.",
                            element.elementId);
                    }
                }
            }

            foreach (var (component, type) in desired)
            {
                var existing = tracker != null ? tracker.Find(component.instanceId) : null;
                if (existing != null) continue;

                // A prefab written before instance ids existed has the component tracked by type only.
                // Adopting it keeps the user's wiring instead of deleting and re-adding the component.
                var adopted = AdoptLegacy(tracker, type);
                if (adopted != null)
                {
                    tracker.Track(component.instanceId, adopted);
                    continue;
                }

                if (component.adoptExistingComponent)
                {
                    adopted = AdoptExisting(go, tracker, type);
                    if (adopted != null)
                    {
                        tracker ??= EnsureTracker(go);
                        tracker.Track(component.instanceId, adopted, DesignerManagedComponentOwnership.Adopted);
                        report.MarkChanged($"Associated imported {type.Name} on '{element.elementId}'");
                        continue;
                    }
                }

                Component added;
                try
                {
                    added = go.AddComponent(type);
                }
                catch (Exception exception)
                {
                    report.Warn($"Could not add {type.FullName} to '{element.elementId}': {exception.Message}");
                    continue;
                }
                if (added == null)
                {
                    report.Warn($"Could not add {type.FullName} to '{element.elementId}'.");
                    continue;
                }

                if (tracker == null)
                {
                    tracker = EnsureTracker(go);
                }
                tracker.Track(component.instanceId, added);
                report.MarkChanged($"Added {type.Name} to '{element.elementId}'");
            }
            return tracker;
        }

        private static DesignerAttachedComponentTracker EnsureTracker(GameObject go)
        {
            var tracker = go.GetComponent<DesignerAttachedComponentTracker>() ??
                          go.AddComponent<DesignerAttachedComponentTracker>();
            tracker.Prune();
            return tracker;
        }

        private static Component AdoptExisting(GameObject go, DesignerAttachedComponentTracker tracker, Type type)
        {
            foreach (var candidate in go.GetComponents(type))
            {
                if (candidate == null || candidate is Transform || candidate is DesignerAttachedComponentTracker)
                    continue;
                var paired = false;
                if (tracker?.managedByInstance != null)
                    foreach (var entry in tracker.managedByInstance)
                        if (entry != null && entry.component == candidate) { paired = true; break; }
                if (!paired) return candidate;
            }
            return null;
        }

        /// <summary>Matches the relative component stack order captured by Prefab Import.</summary>
        private static void ApplyComponentOrder(GameObject go,
            List<(DesignerElementComponent Component, Type Type)> desired,
            DesignerAttachedComponentTracker tracker, DesignerSaveReport report, string elementId)
        {
            if (go == null || tracker == null || desired == null || desired.Count < 2) return;

            var targetIndex = 1; // Transform/RectTransform is always first and cannot move.
            foreach (var (entry, _) in desired)
            {
                var component = tracker.Find(entry.instanceId);
                if (component == null) continue;

                var stack = go.GetComponents<Component>();
                var current = Array.IndexOf(stack, component);
                if (current < 0) continue;

                while (current > targetIndex && ComponentUtility.MoveComponentUp(component)) current--;
                while (current < targetIndex && ComponentUtility.MoveComponentDown(component)) current++;

                stack = go.GetComponents<Component>();
                current = Array.IndexOf(stack, component);
                if (current != targetIndex)
                    report.MarkSkipped($"'{elementId}' {component.GetType().Name} could not be moved to component slot {targetIndex}.");
                targetIndex++;
            }
        }

        private static Component AdoptLegacy(DesignerAttachedComponentTracker tracker, Type type)
        {
            if (tracker?.managedComponents == null) return null;

            foreach (var candidate in tracker.managedComponents)
            {
                if (candidate == null || candidate.GetType() != type) continue;

                var alreadyPaired = false;
                foreach (var entry in tracker.managedByInstance)
                    if (entry != null && entry.component == candidate) { alreadyPaired = true; break; }
                if (!alreadyPaired) return candidate;
            }
            return null;
        }

        // ---- Values ------------------------------------------------------------------------------

        private static void ApplyValues(Component behaviour, DesignerElementComponent component,
            DesignerElementMetadata element, DesignerSaveReport report, ElementResolver resolveElement)
        {
            if (component.properties == null || component.properties.Count == 0) return;

            var serializedObject = new SerializedObject(behaviour);
            var typeName = behaviour.GetType().Name;

            foreach (var entry in component.properties)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key) || entry.value == null) continue;

                var property = serializedObject.FindProperty(entry.key);
                if (property == null)
                {
                    report.MarkSkipped(
                        $"'{element.elementId}' {typeName}.{entry.key} does not exist on this build's script. The value was preserved.");
                    continue;
                }

                if (property.propertyType == SerializedPropertyType.ObjectReference)
                {
                    ApplyReference(property, entry, element, typeName, report, resolveElement);
                    continue;
                }

                if (!StudioPropertyValueCodec.TryDecode(entry.value, property))
                    report.MarkSkipped(
                        $"'{element.elementId}' {typeName}.{entry.key} could not be written as {property.propertyType}. The value was preserved.");
            }

            // Applying only when something actually changed is what keeps a repeated save from
            // marking the prefab dirty and producing an empty diff in source control.
            if (!serializedObject.hasModifiedProperties) return;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            report.MarkChanged($"Applied {typeName} values on '{element.elementId}'");
        }

        private static void ApplyReference(SerializedProperty property, DesignerComponentPropertyEntry entry,
            DesignerElementMetadata element, string typeName, DesignerSaveReport report,
            ElementResolver resolveElement)
        {
            var reference = entry.value.reference;
            if (reference == null || !reference.IsAssigned)
            {
                StudioPropertyValueCodec.SetReference(property, null);
                return;
            }

            if (reference.kind == DesignerReferenceKind.Asset)
            {
                var asset = StudioReferenceUtility.ResolveAsset(reference);
                if (asset == null)
                {
                    report.Warn($"'{element.elementId}' {typeName}.{entry.key}: the referenced asset is missing. " +
                                "The reference was left unchanged and the metadata preserved.");
                    return;
                }
                StudioPropertyValueCodec.SetReference(property, asset);
                return;
            }

            var target = resolveElement?.Invoke(reference.stableElementId);
            if (target == null)
            {
                report.Error($"'{element.elementId}' {typeName}.{entry.key} points at an element that is not on this screen. " +
                             $"Target: {reference.stableElementId}. Assign the field again or restore the element.");
                return;
            }

            var fieldType = StudioPropertyReflection.FieldTypeOf(property);
            var resolved = Resolve(target, reference, fieldType);
            if (resolved == null)
            {
                report.Error($"'{element.elementId}' {typeName}.{entry.key}: '{target.name}' has no " +
                             $"{fieldType?.Name ?? "component"} to reference. " +
                             $"Add the missing component to that element, or point the field somewhere else.");
                return;
            }
            StudioPropertyValueCodec.SetReference(property, resolved);
        }

        private static UnityEngine.Object Resolve(GameObject target, DesignerObjectReference reference, Type fieldType)
        {
            if (fieldType == typeof(GameObject)) return target;

            // The stored component type is the precise answer; the field type is the fallback for a
            // reference authored before the type was recorded.
            var wanted = StudioComponentTypeIndex.Resolve(reference.componentTypeName) ?? fieldType;
            if (wanted == null) return null;

            // A UnityEvent target is declared as UnityEngine.Object, so "point at the GameObject"
            // can only be expressed by the recorded type - not by the field's own.
            if (wanted == typeof(GameObject)) return target;
            if (!typeof(Component).IsAssignableFrom(wanted)) return null;

            var component = target.GetComponent(wanted);
            if (component == null && fieldType != null && fieldType != wanted)
                component = target.GetComponent(fieldType);
            return component;
        }
    }
}

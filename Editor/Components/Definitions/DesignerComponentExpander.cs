using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using emiteat.NexUI.Designer.Editor.Properties;
using emiteat.NexUI.Designer.Editor.Serialization;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Components.Definitions
{
    /// <summary>Resolves a component reference to its definition asset. Abstracted so the expander stays AssetDatabase-free and unit-testable.</summary>
    public interface IDesignerComponentDefinitionResolver
    {
        /// <summary>Returns the definition for a GUID (preferred) or, failing that, a componentId. Null when neither resolves.</summary>
        DesignerComponentDefinitionAsset Resolve(string definitionGuid, string definitionId);
    }

    public enum DesignerComponentExpansionIssueKind
    {
        MissingDefinition,
        RecoveredByComponentId,
        CircularReference,
        UnknownSlot,
        SlotRejectedType,
        RequiredSlotEmpty,
        SlotChildCountOutOfRange,
        UnresolvedOverride,
        UnappliedOverride,
        UnknownVariantProperty,
        UnknownVariantValue,
        VersionMismatch,
        EmptyDefinition,
        BudgetExceeded
    }

    public sealed class DesignerComponentExpansionIssue
    {
        public DesignerComponentExpansionIssueKind Kind;
        /// <summary>Authored element id of the instance the issue belongs to (never an expanded id).</summary>
        public string InstanceElementId;
        public string Message;
        public string Fix;

        public override string ToString() => $"{Kind} ({InstanceElementId}): {Message}";
    }

    /// <summary>
    /// The result of flattening component instances. <see cref="Expanded"/> is the tree Preview,
    /// the serializers and Validation consume; the authored asset is never modified.
    /// </summary>
    public sealed class DesignerComponentExpansion
    {
        /// <summary>Flattened metadata. When <see cref="ContainsInstances"/> is false this is the authored asset itself (no copy was needed).</summary>
        public DesignerMetadataAsset Expanded;
        public bool ContainsInstances;
        public List<DesignerComponentExpansionIssue> Issues = new List<DesignerComponentExpansionIssue>();

        /// <summary>expanded element id → authored instance element id that produced it (roots map to themselves).</summary>
        public Dictionary<string, string> OwnerInstanceByElementId = new Dictionary<string, string>();
        /// <summary>expanded element id → definition-local element id it came from.</summary>
        public Dictionary<string, string> DefinitionElementByElementId = new Dictionary<string, string>();

        public bool IsGenerated(string expandedElementId)
            => DefinitionElementByElementId.ContainsKey(expandedElementId);

        /// <summary>Releases the throw-away ScriptableObject. Safe to call on a pass-through result.</summary>
        public void Dispose()
        {
            if (ContainsInstances && Expanded != null)
                UnityEngine.Object.DestroyImmediate(Expanded);
            Expanded = null;
        }
    }

    /// <summary>
    /// Expands component instances into a flat element tree.
    ///
    /// Instances do <b>not</b> copy their definition's elements into the screen - they store only a
    /// reference plus overrides. That is what makes "edit the definition, every instance updates"
    /// work without a propagation pass, and it is why nothing here ever writes to the authored asset.
    ///
    /// Identity rules (chosen so a re-save reconnects to the same backend objects every time):
    /// <list type="bullet">
    /// <item>The instance element <i>becomes</i> the definition root - it keeps its own elementId,
    /// stableId, placement and parent, and takes the root's type/style. No wrapper object is emitted.</item>
    /// <item>Every other definition element gets id <c>{instanceId}--{definitionElementId}</c> and a
    /// stableId derived deterministically from (instance stableId, definition element stableId), so
    /// ids are identical on every expansion rather than regenerated per save.</item>
    /// </list>
    /// </summary>
    public static class DesignerComponentExpander
    {
        /// <summary>Separator between the instance id and the definition-local id in a generated element id. Hyphens are legal in element ids.</summary>
        public const string IdSeparator = "--";

        /// <summary>Hard ceiling on generated elements, so a pathological definition graph cannot hang the editor.</summary>
        public const int MaxGeneratedElements = 4000;

        /// <summary>Maximum nesting depth of component-in-component before expansion stops.</summary>
        public const int MaxNestingDepth = 16;

        public static bool HasInstances(DesignerMetadataAsset asset)
        {
            if (asset == null) return false;
            for (int i = 0; i < asset.elements.Count; i++)
            {
                var e = asset.elements[i];
                if (e?.componentInstance != null && e.componentInstance.IsInstance) return true;
            }
            return false;
        }

        /// <summary>
        /// Flattens <paramref name="authored"/>. When the screen has no live instances the authored
        /// asset is returned unchanged (no allocation, identical behaviour to pre-Phase-3 callers).
        /// </summary>
        public static DesignerComponentExpansion Expand(DesignerMetadataAsset authored, IDesignerComponentDefinitionResolver resolver)
        {
            var result = new DesignerComponentExpansion();
            if (authored == null)
            {
                result.Expanded = null;
                return result;
            }
            if (!HasInstances(authored))
            {
                result.Expanded = authored;
                result.ContainsInstances = false;
                return result;
            }

            result.ContainsInstances = true;
            var expanded = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            expanded.name = authored.name + " (Expanded)";
            expanded.hideFlags = HideFlags.HideAndDontSave;
            result.Expanded = expanded;
            CopyScreenLevelData(authored, expanded);

            // Deep-clone every authored element first: overrides must never touch authored data.
            foreach (var e in authored.elements)
            {
                if (e == null) continue;
                var clone = DesignerMetadataUtility.Clone(e);
                expanded.elements.Add(clone);
                result.OwnerInstanceByElementId[clone.elementId] = clone.elementId;
            }

            // Expand roots-first so a nested instance is always processed after its host exists.
            var budget = new ExpansionBudget();
            for (int i = 0; i < expanded.elements.Count; i++)
            {
                var host = expanded.elements[i];
                if (host?.componentInstance == null || !host.componentInstance.IsInstance) continue;
                // Skip elements produced by an outer expansion - those are handled by the recursion
                // that created them, and re-entering here would double-expand them.
                if (result.DefinitionElementByElementId.ContainsKey(host.elementId)) continue;
                ExpandHost(expanded, host, host.elementId, resolver, result, new List<string>(), 0, budget);
            }

            DesignerHierarchyUtility.NormalizeSiblingIndices(expanded);
            return result;
        }

        private sealed class ExpansionBudget
        {
            public int Generated;
            public bool Exhausted => Generated >= MaxGeneratedElements;
        }

        /// <summary>
        /// Expands one instance in place. <paramref name="host"/> is already present in
        /// <paramref name="expanded"/>; on success it is replaced by the merged definition root.
        /// </summary>
        private static void ExpandHost(DesignerMetadataAsset expanded, DesignerElementMetadata host, string ownerInstanceId,
            IDesignerComponentDefinitionResolver resolver, DesignerComponentExpansion result,
            List<string> definitionStack, int depth, ExpansionBudget budget)
        {
            var reference = host.componentInstance;
            var definition = resolver?.Resolve(reference.definitionGuid, reference.definitionId);
            if (definition == null)
            {
                // Never delete the instance or its authored slot content - the screen keeps rendering
                // the placeholder box and the user is told exactly which asset is missing.
                result.Issues.Add(new DesignerComponentExpansionIssue
                {
                    Kind = DesignerComponentExpansionIssueKind.MissingDefinition,
                    InstanceElementId = ownerInstanceId,
                    Message = $"Component definition '{reference.definitionId ?? reference.definitionGuid}' could not be resolved.",
                    Fix = "Restore the definition asset, or Detach the instance to keep its current content as ordinary elements."
                });
                return;
            }

            if (!string.IsNullOrEmpty(reference.definitionGuid) && definitionStack.Contains(reference.definitionGuid))
            {
                result.Issues.Add(new DesignerComponentExpansionIssue
                {
                    Kind = DesignerComponentExpansionIssueKind.CircularReference,
                    InstanceElementId = ownerInstanceId,
                    Message = $"Component '{definition.EffectiveDisplayName}' contains itself (cycle: {string.Join(" → ", definitionStack)}).",
                    Fix = "Remove the self-reference inside the definition, or replace the inner instance with a Slot."
                });
                return;
            }
            if (depth >= MaxNestingDepth || budget.Exhausted)
            {
                result.Issues.Add(new DesignerComponentExpansionIssue
                {
                    Kind = DesignerComponentExpansionIssueKind.BudgetExceeded,
                    InstanceElementId = ownerInstanceId,
                    Message = depth >= MaxNestingDepth
                        ? $"Component nesting exceeded {MaxNestingDepth} levels; expansion stopped here."
                        : $"Component expansion exceeded {MaxGeneratedElements} generated elements; expansion stopped here.",
                    Fix = "Flatten deeply nested components, or split the screen."
                });
                return;
            }

            var definitionRoot = definition.Root;
            if (definitionRoot == null)
            {
                result.Issues.Add(new DesignerComponentExpansionIssue
                {
                    Kind = DesignerComponentExpansionIssueKind.EmptyDefinition,
                    InstanceElementId = ownerInstanceId,
                    Message = $"Component '{definition.EffectiveDisplayName}' has no elements.",
                    Fix = "Open the definition and add at least one element, or delete the instance."
                });
                return;
            }

            if (reference.definitionVersion != 0 && reference.definitionVersion != definition.version)
            {
                result.Issues.Add(new DesignerComponentExpansionIssue
                {
                    Kind = DesignerComponentExpansionIssueKind.VersionMismatch,
                    InstanceElementId = ownerInstanceId,
                    Message = $"Instance was authored against '{definition.EffectiveDisplayName}' v{reference.definitionVersion}; the definition is now v{definition.version}.",
                    Fix = "Run Update From Definition on the instance to reconcile overrides and slot content."
                });
            }

            // Authored children captured before expansion: these are the instance's slot content.
            var authoredChildren = DesignerHierarchyUtility.GetOrderedChildren(expanded, host.elementId);

            // ---- Clone the definition sub-tree ------------------------------------------------
            var idMap = new Dictionary<string, string>(StringComparer.Ordinal) { [definitionRoot.elementId] = host.elementId };
            var clones = new List<DesignerElementMetadata>();
            var offset = host.rect.position - definitionRoot.rect.position;

            foreach (var source in definition.elements)
            {
                if (source == null || source.elementId == definitionRoot.elementId) continue;
                idMap[source.elementId] = host.elementId + IdSeparator + source.elementId;
            }

            var mergedRoot = MergeRoot(host, definitionRoot, ownerInstanceId, result);
            clones.Add(mergedRoot);

            foreach (var source in definition.elements)
            {
                if (source == null || source.elementId == definitionRoot.elementId) continue;
                var clone = DesignerMetadataUtility.Clone(source);
                clone.elementId = idMap[source.elementId];
                clone.stableId = DeterministicStableId(host.stableId, source.stableId ?? source.elementId);
                clone.parentId = ResolveParent(source.parentId, idMap, host.elementId);
                clone.rect.position += offset;
                clones.Add(clone);
                budget.Generated++;

                result.OwnerInstanceByElementId[clone.elementId] = ownerInstanceId;
                result.DefinitionElementByElementId[clone.elementId] = source.elementId;
            }
            result.DefinitionElementByElementId[mergedRoot.elementId] = definitionRoot.elementId;
            result.OwnerInstanceByElementId[mergedRoot.elementId] = ownerInstanceId;

            // ---- Variant rules, then instance overrides (instance always wins) ----------------
            ApplyVariantRules(definition, reference, idMap, clones, ownerInstanceId, result);
            ApplyOverrides(definition, reference.overrides, idMap, clones, ownerInstanceId, result, "instance");

            // ---- Splice into the expanded asset ----------------------------------------------
            var hostIndex = expanded.elements.IndexOf(host);
            expanded.elements[hostIndex] = mergedRoot;
            for (int i = 1; i < clones.Count; i++)
                expanded.elements.Insert(hostIndex + i, clones[i]);

            ReparentSlotContent(definition, authoredChildren, idMap, host.elementId, ownerInstanceId, result);

            // ---- Nested components ------------------------------------------------------------
            definitionStack.Add(reference.definitionGuid);
            for (int i = 0; i < clones.Count; i++)
            {
                var clone = clones[i];
                if (i == 0) continue; // the merged root's own reference is the one we just expanded
                if (clone.componentInstance == null || !clone.componentInstance.IsInstance) continue;
                ExpandHost(expanded, clone, ownerInstanceId, resolver, result, definitionStack, depth + 1, budget);
            }
            definitionStack.RemoveAt(definitionStack.Count - 1);
        }

        /// <summary>
        /// Produces the element that replaces the instance: definition-root visuals, instance
        /// identity and placement. Keeping the instance's elementId/stableId is what lets a uGUI
        /// prefab reconnect to the same GameObject across saves.
        /// </summary>
        private static DesignerElementMetadata MergeRoot(DesignerElementMetadata host, DesignerElementMetadata definitionRoot,
            string ownerInstanceId, DesignerComponentExpansion result)
        {
            var merged = DesignerMetadataUtility.Clone(definitionRoot);

            merged.elementId = host.elementId;
            merged.stableId = host.stableId;
            merged.parentId = host.parentId;
            merged.siblingIndex = host.siblingIndex;
            merged.parentSlotId = host.parentSlotId;
            merged.rect = host.rect;
            merged.locked = host.locked;
            merged.hiddenInDesigner = host.hiddenInDesigner;
            // An instance hidden at runtime hides the whole component, but a definition root that is
            // itself runtime-hidden stays hidden regardless of the instance.
            merged.runtimeVisible = host.runtimeVisible && definitionRoot.runtimeVisible;
            // Copies, not shared references: nothing downstream may reach back into authored data
            // through the expansion, even by accident.
            merged.componentInstance = CloneReference(host.componentInstance);

            if (!string.IsNullOrEmpty(host.displayName))
                merged.displayName = host.displayName;

            // Instance-level bindings are authored on the instance element and must survive: they are
            // how a screen wires one card in a list to its own data.
            if (HasAnyBinding(host.binding))
                merged.binding = JsonUtility.FromJson<DesignerBindingMetadata>(JsonUtility.ToJson(host.binding));

            return merged;
        }

        private static DesignerComponentInstanceMetadata CloneReference(DesignerComponentInstanceMetadata source)
            => source == null
                ? new DesignerComponentInstanceMetadata()
                : JsonUtility.FromJson<DesignerComponentInstanceMetadata>(JsonUtility.ToJson(source));

        private static bool HasAnyBinding(DesignerBindingMetadata b)
            => b != null && (!string.IsNullOrEmpty(b.textKey) || !string.IsNullOrEmpty(b.valueKey) ||
                             !string.IsNullOrEmpty(b.visibilityKey) || !string.IsNullOrEmpty(b.classKey) ||
                             !string.IsNullOrEmpty(b.commandKey) || !string.IsNullOrEmpty(b.interactableKey));

        private static string ResolveParent(string definitionParentId, Dictionary<string, string> idMap, string rootId)
        {
            if (string.IsNullOrEmpty(definitionParentId)) return rootId;      // extra definition roots hang off the instance
            return idMap.TryGetValue(definitionParentId, out var mapped) ? mapped : rootId;
        }

        private static void ApplyVariantRules(DesignerComponentDefinitionAsset definition, DesignerComponentInstanceMetadata reference,
            Dictionary<string, string> idMap, List<DesignerElementMetadata> clones, string ownerInstanceId, DesignerComponentExpansion result)
        {
            // Validate the instance's selections against the definition's axes before evaluating.
            foreach (var selection in reference.variantSelections)
            {
                if (selection == null || string.IsNullOrEmpty(selection.propertyName)) continue;
                var property = definition.FindVariantProperty(selection.propertyName);
                if (property == null)
                {
                    result.Issues.Add(new DesignerComponentExpansionIssue
                    {
                        Kind = DesignerComponentExpansionIssueKind.UnknownVariantProperty,
                        InstanceElementId = ownerInstanceId,
                        Message = $"Variant '{selection.propertyName}' no longer exists on '{definition.EffectiveDisplayName}'.",
                        Fix = "Remove the selection, or re-add the variant property to the definition."
                    });
                    continue;
                }
                if (property.type == DesignerComponentVariantPropertyType.Enum &&
                    property.options != null && property.options.Count > 0 &&
                    !string.IsNullOrEmpty(selection.value) && !property.options.Contains(selection.value))
                {
                    result.Issues.Add(new DesignerComponentExpansionIssue
                    {
                        Kind = DesignerComponentExpansionIssueKind.UnknownVariantValue,
                        InstanceElementId = ownerInstanceId,
                        Message = $"Variant '{selection.propertyName}' value '{selection.value}' is not one of the definition's options.",
                        Fix = "Pick a defined option, or add the value to the definition's variant property."
                    });
                }
            }

            foreach (var rule in definition.variantRules)
            {
                if (rule == null || string.IsNullOrEmpty(rule.propertyName)) continue;
                var property = definition.FindVariantProperty(rule.propertyName);
                var selected = reference.GetVariantSelection(rule.propertyName);
                if (string.IsNullOrEmpty(selected))
                    selected = property != null ? property.EffectiveDefault : null;
                if (!string.Equals(selected, rule.equalsValue, StringComparison.Ordinal)) continue;

                ApplyOverrides(definition, rule.overrides, idMap, clones, ownerInstanceId, result, "variant " + rule.propertyName);
                SetVisibility(rule.hiddenElementIds, idMap, clones, false);
                SetVisibility(rule.shownElementIds, idMap, clones, true);
            }
        }

        private static void SetVisibility(List<string> definitionElementIds, Dictionary<string, string> idMap,
            List<DesignerElementMetadata> clones, bool visible)
        {
            if (definitionElementIds == null) return;
            foreach (var definitionElementId in definitionElementIds)
            {
                if (string.IsNullOrEmpty(definitionElementId)) continue;
                if (!idMap.TryGetValue(definitionElementId, out var expandedId)) continue;
                var target = Find(clones, expandedId);
                if (target != null) target.runtimeVisible = visible;
            }
        }

        private static void ApplyOverrides(DesignerComponentDefinitionAsset definition, List<DesignerComponentPropertyOverride> overrides,
            Dictionary<string, string> idMap, List<DesignerElementMetadata> clones, string ownerInstanceId,
            DesignerComponentExpansion result, string source)
        {
            if (overrides == null) return;
            foreach (var item in overrides)
            {
                if (item == null) continue;

                var targetElementId = item.targetElementId;
                var propertyId = item.propertyId;
                if (!string.IsNullOrEmpty(item.exposedPropertyName))
                {
                    var exposed = definition.FindExposed(item.exposedPropertyName);
                    if (exposed == null)
                    {
                        result.Issues.Add(new DesignerComponentExpansionIssue
                        {
                            Kind = DesignerComponentExpansionIssueKind.UnresolvedOverride,
                            InstanceElementId = ownerInstanceId,
                            Message = $"{source} override targets exposed property '{item.exposedPropertyName}', which '{definition.EffectiveDisplayName}' no longer declares.",
                            Fix = "Reset the override, or re-expose the property on the definition."
                        });
                        continue;
                    }
                    targetElementId = exposed.targetElementId;
                    propertyId = exposed.propertyId;
                }

                if (string.IsNullOrEmpty(targetElementId) || !idMap.TryGetValue(targetElementId, out var expandedId))
                {
                    result.Issues.Add(new DesignerComponentExpansionIssue
                    {
                        Kind = DesignerComponentExpansionIssueKind.UnresolvedOverride,
                        InstanceElementId = ownerInstanceId,
                        Message = $"{source} override targets element '{targetElementId}', which does not exist in '{definition.EffectiveDisplayName}'.",
                        Fix = "Reset the override, or rename the definition element back."
                    });
                    continue;
                }

                var target = Find(clones, expandedId);
                if (target == null) continue;

                if (!DesignerPropertyApplier.Apply(target, propertyId, item.value))
                {
                    result.Issues.Add(new DesignerComponentExpansionIssue
                    {
                        Kind = DesignerComponentExpansionIssueKind.UnappliedOverride,
                        InstanceElementId = ownerInstanceId,
                        Message = $"{source} override '{propertyId}' has no authored metadata representation and was not applied.",
                        Fix = "Use a property the Designer can store, or set the value inside the definition."
                    });
                }
            }
        }

        /// <summary>Moves the instance's authored children onto the definition element that hosts their slot.</summary>
        private static void ReparentSlotContent(DesignerComponentDefinitionAsset definition, List<DesignerElementMetadata> authoredChildren,
            Dictionary<string, string> idMap, string rootId, string ownerInstanceId, DesignerComponentExpansion result)
        {
            var perSlotCount = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var child in authoredChildren)
            {
                var slotId = string.IsNullOrEmpty(child.parentSlotId) ? DesignerComponentSlotDefinition.Content : child.parentSlotId;
                var slot = definition.FindSlot(slotId);
                if (slot == null)
                {
                    result.Issues.Add(new DesignerComponentExpansionIssue
                    {
                        Kind = DesignerComponentExpansionIssueKind.UnknownSlot,
                        InstanceElementId = ownerInstanceId,
                        Message = $"'{child.elementId}' targets slot '{slotId}', which '{definition.EffectiveDisplayName}' does not declare. It stays on the component root.",
                        Fix = "Move the child to a declared slot, or add the slot to the definition."
                    });
                    child.parentId = rootId;
                    continue;
                }

                if (!slot.Accepts(child.elementType))
                {
                    result.Issues.Add(new DesignerComponentExpansionIssue
                    {
                        Kind = DesignerComponentExpansionIssueKind.SlotRejectedType,
                        InstanceElementId = ownerInstanceId,
                        Message = $"Slot '{slot.slotId}' accepts {string.Join(", ", slot.acceptedTypes)} but '{child.elementId}' is a {child.elementType}.",
                        Fix = "Use an accepted component type, or widen the slot's accepted types."
                    });
                }

                perSlotCount.TryGetValue(slot.slotId, out var count);
                perSlotCount[slot.slotId] = count + 1;

                var hostId = rootId;
                if (!string.IsNullOrEmpty(slot.hostElementId) && idMap.TryGetValue(slot.hostElementId, out var mapped))
                    hostId = mapped;
                child.parentId = hostId;
            }

            foreach (var slot in definition.slots)
            {
                if (slot == null) continue;
                perSlotCount.TryGetValue(slot.slotId, out var count);
                if (slot.required && count == 0)
                {
                    result.Issues.Add(new DesignerComponentExpansionIssue
                    {
                        Kind = DesignerComponentExpansionIssueKind.RequiredSlotEmpty,
                        InstanceElementId = ownerInstanceId,
                        Message = $"Required slot '{slot.slotId}' of '{definition.EffectiveDisplayName}' is empty.",
                        Fix = "Add content to the slot, or make the slot optional in the definition."
                    });
                }
                else if (count < slot.minimumChildren || (!slot.IsUnbounded && count > slot.maximumChildren))
                {
                    result.Issues.Add(new DesignerComponentExpansionIssue
                    {
                        Kind = DesignerComponentExpansionIssueKind.SlotChildCountOutOfRange,
                        InstanceElementId = ownerInstanceId,
                        Message = $"Slot '{slot.slotId}' holds {count} children; the definition allows " +
                                  $"{slot.minimumChildren}..{(slot.IsUnbounded ? "∞" : slot.maximumChildren.ToString())}.",
                        Fix = "Add or remove slot content, or adjust the slot's bounds."
                    });
                }
            }
        }

        private static DesignerElementMetadata Find(List<DesignerElementMetadata> elements, string elementId)
        {
            for (int i = 0; i < elements.Count; i++)
                if (elements[i] != null && elements[i].elementId == elementId) return elements[i];
            return null;
        }

        private static void CopyScreenLevelData(DesignerMetadataAsset from, DesignerMetadataAsset to)
        {
            to.schemaVersion = from.schemaVersion;
            to.screenId = from.screenId;
            // Screen-level blocks are read-only during expansion, so sharing the references is safe
            // and avoids cloning motion/scenario data on every preview rebuild.
            to.screenMotion = from.screenMotion;
            to.variants = from.variants;
            to.responsiveRules = from.responsiveRules;
            to.contract = from.contract;
            to.snapshots = from.snapshots;
            to.localization = from.localization;
            to.prompts = from.prompts;
            to.recipes = from.recipes;
        }

        /// <summary>
        /// Deterministic 32-hex stable id from the owning instance and the definition element, so
        /// generated backend objects keep the same identity across saves, domain reloads and machines.
        /// </summary>
        public static string DeterministicStableId(string instanceStableId, string definitionStableId)
        {
            using var md5 = MD5.Create();
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes((instanceStableId ?? string.Empty) + "/" + (definitionStableId ?? string.Empty)));
            return new Guid(bytes).ToString("N");
        }
    }
}

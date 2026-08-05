using System;
using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Serialization;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Components.Definitions
{
    /// <summary>Outcome of an authoring operation, so callers can report the truth instead of assuming success.</summary>
    public sealed class DesignerComponentOperationResult
    {
        public bool Success;
        public string Message;
        public DesignerComponentDefinitionAsset Definition;
        public DesignerElementMetadata Element;
        public List<string> Warnings = new List<string>();

        /// <summary>
        /// Things the operation fixed by itself. Separate from <see cref="Warnings"/> because a repair
        /// the user does not have to act on should not read like a problem they do.
        /// </summary>
        public List<string> Notes = new List<string>();

        public static DesignerComponentOperationResult Fail(string message)
            => new DesignerComponentOperationResult { Success = false, Message = message };

        public static DesignerComponentOperationResult Ok(string message)
            => new DesignerComponentOperationResult { Success = true, Message = message };
    }

    /// <summary>
    /// Authoring operations on reusable components: extract a definition from a selection, place an
    /// instance, edit/reset overrides, detach, swap and reconcile against a new definition version.
    ///
    /// Every mutator is Undo-aware and only marks assets dirty when something actually changed. None
    /// of them delete user-authored elements without being told to: Detach materializes, Swap keeps
    /// slot content, and a missing definition is reported rather than cleaned up.
    /// </summary>
    public static class DesignerComponentService
    {
        /// <summary>Element type used for an instance whose definition cannot be resolved, so the palette still shows something meaningful.</summary>
        public const string InstanceTypeId = "ComponentInstance";

        // ---- Creation ---------------------------------------------------------------------

        /// <summary>
        /// Extracts <paramref name="rootElementId"/> and its whole subtree from <paramref name="screen"/>
        /// into a new definition asset at <paramref name="assetPath"/>, and converts the original
        /// subtree into an instance of it.
        ///
        /// The subtree is <b>copied</b>, never moved, until the asset write succeeds - if
        /// <see cref="AssetDatabase.CreateAsset"/> fails, the screen is left untouched.
        /// </summary>
        public static DesignerComponentOperationResult CreateDefinitionFromSubtree(
            DesignerMetadataAsset screen, string rootElementId, string assetPath, string displayName = null)
        {
            if (screen == null) return DesignerComponentOperationResult.Fail("No screen metadata.");
            var root = screen.Find(rootElementId);
            if (root == null) return DesignerComponentOperationResult.Fail($"Element '{rootElementId}' not found.");
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                return DesignerComponentOperationResult.Fail("A component definition needs a '.asset' path.");
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
                return DesignerComponentOperationResult.Fail($"'{assetPath}' already exists. Pick another name.");
            if (root.componentInstance != null && root.componentInstance.IsInstance)
                return DesignerComponentOperationResult.Fail("The selected element is already a component instance. Detach it first if you want to fork it.");

            var subtree = new List<DesignerElementMetadata> { root };
            subtree.AddRange(DesignerHierarchyUtility.GetDescendants(screen, root));

            var definition = ScriptableObject.CreateInstance<DesignerComponentDefinitionAsset>();
            definition.componentId = Guid.NewGuid().ToString("N");
            definition.version = 1;
            definition.displayName = string.IsNullOrEmpty(displayName)
                ? (string.IsNullOrEmpty(root.displayName) ? root.elementId : root.displayName)
                : displayName;
            definition.rootElementId = root.elementId;
            definition.defaultSize = root.rect.size;

            foreach (var source in subtree)
            {
                var clone = DesignerMetadataUtility.Clone(source);
                // The definition owns a fresh identity space: its stableIds are the seed the expander
                // hashes per instance, so reusing the screen's would tie two unrelated things together.
                clone.stableId = Guid.NewGuid().ToString("N");
                if (clone.elementId == root.elementId) clone.parentId = string.Empty;
                clone.componentInstance ??= new DesignerComponentInstanceMetadata();
                definition.elements.Add(clone);
            }
            definition.slots.Add(new DesignerComponentSlotDefinition
            {
                slotId = DesignerComponentSlotDefinition.Content,
                displayName = "Content",
                hostElementId = root.elementId
            });

            AssetDatabase.CreateAsset(definition, assetPath);
            AssetDatabase.SaveAssets();
            DesignerComponentLibrary.Invalidate();

            var guid = DesignerComponentLibrary.GuidOf(definition);
            if (string.IsNullOrEmpty(guid))
                return DesignerComponentOperationResult.Fail($"Created '{assetPath}' but could not resolve its GUID; the screen was not modified.");

            // Only now is it safe to collapse the original subtree into an instance.
            Undo.RecordObject(screen, "Create NexUI Component");
            foreach (var descendant in DesignerHierarchyUtility.GetDescendants(screen, root))
                screen.elements.Remove(descendant);

            root.componentInstance = new DesignerComponentInstanceMetadata
            {
                definitionGuid = guid,
                definitionId = definition.componentId,
                definitionVersion = definition.version
            };
            DesignerMetadataUtility.MarkDirty(screen);

            return new DesignerComponentOperationResult
            {
                Success = true,
                Definition = definition,
                Element = root,
                Message = $"Created component '{definition.EffectiveDisplayName}' from '{root.elementId}' ({subtree.Count} element(s))."
            };
        }

        /// <summary>Places a new instance of <paramref name="definition"/> in <paramref name="screen"/>.</summary>
        public static DesignerComponentOperationResult Instantiate(DesignerMetadataAsset screen,
            DesignerComponentDefinitionAsset definition, Vector2 position, string parentId = null, string parentSlotId = null)
        {
            if (screen == null) return DesignerComponentOperationResult.Fail("No screen metadata.");
            if (definition == null) return DesignerComponentOperationResult.Fail("No component definition.");
            var guid = DesignerComponentLibrary.GuidOf(definition);
            if (string.IsNullOrEmpty(guid))
                return DesignerComponentOperationResult.Fail("The definition is not saved as a project asset yet.");

            var root = definition.Root;
            var size = root != null && root.rect.size.sqrMagnitude > 0f ? root.rect.size : definition.defaultSize;

            Undo.RecordObject(screen, "Add NexUI Component Instance");
            var element = new DesignerElementMetadata
            {
                elementId = DesignerMetadataUtility.MakeUniqueId(screen, BaseId(definition)),
                stableId = Guid.NewGuid().ToString("N"),
                displayName = definition.EffectiveDisplayName,
                elementType = root != null ? root.elementType : InstanceTypeId,
                rect = new Rect(position, size),
                parentId = parentId ?? string.Empty,
                parentSlotId = parentSlotId,
                componentInstance = new DesignerComponentInstanceMetadata
                {
                    definitionGuid = guid,
                    definitionId = definition.componentId,
                    definitionVersion = definition.version
                }
            };
            foreach (var property in definition.variantProperties)
                if (property != null && !string.IsNullOrEmpty(property.propertyName))
                    element.componentInstance.SetVariantSelection(property.propertyName, property.EffectiveDefault);

            screen.elements.Add(element);
            DesignerMetadataUtility.MarkDirty(screen);

            return new DesignerComponentOperationResult
            {
                Success = true,
                Definition = definition,
                Element = element,
                Message = $"Placed '{definition.EffectiveDisplayName}' as '{element.elementId}'."
            };
        }

        private static string BaseId(DesignerComponentDefinitionAsset definition)
        {
            var name = definition.EffectiveDisplayName ?? "component";
            var builder = new System.Text.StringBuilder(name.Length);
            foreach (var c in name)
                if (char.IsLetterOrDigit(c) || c == '_') builder.Append(c);
            if (builder.Length == 0 || !char.IsLetter(builder[0])) builder.Insert(0, 'c');
            return char.ToLowerInvariant(builder[0]) + builder.ToString(1, builder.Length - 1);
        }

        // ---- Overrides --------------------------------------------------------------------

        /// <summary>Sets (or replaces) one override on an instance. Returns false when the element is not an instance.</summary>
        public static bool SetOverride(DesignerMetadataAsset screen, DesignerElementMetadata instance,
            DesignerComponentPropertyOverride item)
        {
            if (screen == null || instance?.componentInstance == null || item == null) return false;
            if (!instance.componentInstance.IsInstance) return false;
            if (item.propertyId == DesignerPropertyId.None && string.IsNullOrEmpty(item.exposedPropertyName)) return false;

            Undo.RecordObject(screen, "Set NexUI Component Override");
            var existing = instance.componentInstance.FindOverride(item.Key);
            if (existing != null) instance.componentInstance.overrides.Remove(existing);

            var stored = item.Clone();
            StampTargetStableId(stored, instance.componentInstance);
            instance.componentInstance.overrides.Add(stored);
            DesignerMetadataUtility.MarkDirty(screen);
            return true;
        }

        /// <summary>Removes one override so the definition value shows through again.</summary>
        public static bool ResetOverride(DesignerMetadataAsset screen, DesignerElementMetadata instance, string overrideKey)
        {
            if (screen == null || instance?.componentInstance == null) return false;
            var existing = instance.componentInstance.FindOverride(overrideKey);
            if (existing == null) return false;
            Undo.RecordObject(screen, "Reset NexUI Component Override");
            instance.componentInstance.overrides.Remove(existing);
            DesignerMetadataUtility.MarkDirty(screen);
            return true;
        }

        /// <summary>Removes every override. Returns the number removed so the caller can report it.</summary>
        public static int ResetAllOverrides(DesignerMetadataAsset screen, DesignerElementMetadata instance)
        {
            if (screen == null || instance?.componentInstance == null) return 0;
            var count = instance.componentInstance.overrides.Count;
            if (count == 0) return 0;
            Undo.RecordObject(screen, "Reset All NexUI Component Overrides");
            instance.componentInstance.overrides.Clear();
            DesignerMetadataUtility.MarkDirty(screen);
            return count;
        }

        // ---- Lifecycle --------------------------------------------------------------------

        /// <summary>
        /// Turns an instance into ordinary authored elements: the expanded subtree (definition +
        /// variants + overrides applied) is materialized into the screen and the reference is marked
        /// detached. Nothing is lost - slot content keeps its own elements and stays parented where
        /// the expansion put it.
        /// </summary>
        /// <param name="variantContext">
        /// The canvas environment. Detach bakes what is currently expanded, so a rule conditioned on
        /// the resolution has to be judged against the same canvas the user is looking at - otherwise
        /// detaching would silently produce a different arrangement than the one on screen.
        /// </param>
        public static DesignerComponentOperationResult Detach(DesignerMetadataAsset screen, DesignerElementMetadata instance,
            DesignerComponentVariantContext variantContext = default)
        {
            if (screen == null || instance?.componentInstance == null || !instance.componentInstance.IsInstance)
                return DesignerComponentOperationResult.Fail("The element is not a live component instance.");

            var expansion = DesignerComponentExpander.Expand(screen, DesignerComponentLibrary.Resolver, variantContext);
            try
            {
                if (!expansion.ContainsInstances || expansion.Expanded == null)
                    return DesignerComponentOperationResult.Fail("Nothing to detach.");

                var generated = new List<DesignerElementMetadata>();
                foreach (var e in expansion.Expanded.elements)
                {
                    if (e == null) continue;
                    if (!expansion.OwnerInstanceByElementId.TryGetValue(e.elementId, out var owner)) continue;
                    if (owner != instance.elementId) continue;
                    if (!expansion.DefinitionElementByElementId.ContainsKey(e.elementId)) continue;
                    if (e.elementId == instance.elementId) continue;   // the root is the instance itself
                    generated.Add(DesignerMetadataUtility.Clone(e));
                }

                var expandedRoot = FindIn(expansion.Expanded, instance.elementId);

                Undo.RecordObject(screen, "Detach NexUI Component");

                // Bake the definition root's resolved look onto the instance element so detaching is
                // visually a no-op, then keep the reference for traceability but mark it detached.
                if (expandedRoot != null)
                {
                    var baked = DesignerMetadataUtility.Clone(expandedRoot);
                    baked.componentInstance = instance.componentInstance;
                    CopyInto(baked, instance);
                }
                instance.componentInstance.detached = true;

                var existingIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var e in screen.elements)
                    if (e != null && !string.IsNullOrEmpty(e.elementId)) existingIds.Add(e.elementId);

                var renames = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var e in generated)
                {
                    var readable = ShortenGeneratedId(e.elementId, instance.elementId);
                    var unique = readable;
                    for (var suffix = 1; existingIds.Contains(unique); suffix++) unique = readable + suffix;
                    existingIds.Add(unique);
                    renames[e.elementId] = unique;
                }
                foreach (var e in generated)
                {
                    e.elementId = renames[e.elementId];
                    if (!string.IsNullOrEmpty(e.parentId) && renames.TryGetValue(e.parentId, out var newParent))
                        e.parentId = newParent;
                    e.componentInstance ??= new DesignerComponentInstanceMetadata();
                    screen.elements.Add(e);
                }

                DesignerHierarchyUtility.NormalizeSiblingIndices(screen);
                DesignerMetadataUtility.MarkDirty(screen);

                return new DesignerComponentOperationResult
                {
                    Success = true,
                    Element = instance,
                    Message = $"Detached '{instance.elementId}'; {generated.Count} element(s) are now authored directly."
                };
            }
            finally
            {
                expansion.Dispose();
            }
        }

        /// <summary>
        /// Points an instance at a different definition. Overrides that do not resolve against the new
        /// definition are <b>reported and dropped</b> (they cannot be applied), and the caller is
        /// expected to confirm before calling - this is a destructive edit.
        /// </summary>
        public static DesignerComponentOperationResult Swap(DesignerMetadataAsset screen, DesignerElementMetadata instance,
            DesignerComponentDefinitionAsset target)
        {
            if (screen == null || instance?.componentInstance == null)
                return DesignerComponentOperationResult.Fail("The element is not a component instance.");
            if (target == null) return DesignerComponentOperationResult.Fail("No target definition.");
            var guid = DesignerComponentLibrary.GuidOf(target);
            if (string.IsNullOrEmpty(guid))
                return DesignerComponentOperationResult.Fail("The target definition is not saved as a project asset.");

            var result = new DesignerComponentOperationResult { Success = true, Definition = target, Element = instance };

            Undo.RecordObject(screen, "Swap NexUI Component");
            var reference = instance.componentInstance;
            var kept = new List<DesignerComponentPropertyOverride>();
            foreach (var item in reference.overrides)
            {
                if (item == null) continue;
                if (!string.IsNullOrEmpty(item.exposedPropertyName))
                {
                    if (target.FindExposed(item.exposedPropertyName) != null) { kept.Add(item); continue; }
                    result.Warnings.Add($"Dropped override '{item.exposedPropertyName}': not exposed by '{target.EffectiveDisplayName}'.");
                }
                else if (target.Find(item.targetElementId) != null)
                {
                    // The stable id belongs to the definition being left behind. Matching by name is
                    // the only thing two unrelated definitions share, so re-record the identity against
                    // the new one instead of carrying a pointer into the old.
                    item.targetStableId = target.Find(item.targetElementId).stableId;
                    kept.Add(item);
                }
                else
                {
                    result.Warnings.Add($"Dropped override '{item.targetElementId}.{item.propertyId}': no such element in '{target.EffectiveDisplayName}'.");
                }
            }
            reference.overrides = kept;

            var keptSelections = new List<DesignerComponentVariantSelection>();
            foreach (var selection in reference.variantSelections)
            {
                if (selection == null) continue;
                if (target.FindVariantProperty(selection.propertyName) != null) keptSelections.Add(selection);
                else result.Warnings.Add($"Dropped variant selection '{selection.propertyName}': not declared by '{target.EffectiveDisplayName}'.");
            }
            reference.variantSelections = keptSelections;

            reference.definitionGuid = guid;
            reference.definitionId = target.componentId;
            reference.definitionVersion = target.version;
            reference.detached = false;
            DesignerMetadataUtility.MarkDirty(screen);

            result.Message = $"Swapped '{instance.elementId}' to '{target.EffectiveDisplayName}'" +
                             (result.Warnings.Count > 0 ? $" ({result.Warnings.Count} override(s) dropped)." : ".");
            return result;
        }

        /// <summary>
        /// Reconciles an instance with the current definition version: re-points overrides whose target
        /// was renamed, records the stable identity of ones that predate it, adds any new variant axis,
        /// and reports what still does not resolve.
        /// </summary>
        /// <param name="resetUnresolved">
        /// Removes the overrides that could not be re-pointed. Off by default: an override that no
        /// longer resolves is still the user's authored intent, and a definition element deleted by
        /// mistake is restorable - the value it carried is not.
        /// </param>
        public static DesignerComponentOperationResult UpdateFromDefinition(DesignerMetadataAsset screen,
            DesignerElementMetadata instance, bool resetUnresolved = false)
        {
            if (screen == null || instance?.componentInstance == null || !instance.componentInstance.HasReference)
                return DesignerComponentOperationResult.Fail("The element is not a component instance.");

            var reference = instance.componentInstance;
            var definition = DesignerComponentLibrary.Resolve(reference.definitionGuid, reference.definitionId);
            if (definition == null)
                return DesignerComponentOperationResult.Fail("The definition could not be resolved; nothing was changed.");

            var result = new DesignerComponentOperationResult { Success = true, Definition = definition, Element = instance };

            Undo.RecordObject(screen, "Update NexUI Component Instance");
            BackfillDefinitionTargets(definition);

            var unresolved = new List<DesignerComponentPropertyOverride>();
            foreach (var item in reference.overrides)
            {
                if (item == null) continue;

                if (!string.IsNullOrEmpty(item.exposedPropertyName))
                {
                    // An exposed property is addressed by name, which only the definition author can
                    // change, and only deliberately - there is nothing to re-point it to.
                    if (definition.FindExposed(item.exposedPropertyName) == null) unresolved.Add(item);
                    continue;
                }

                var target = definition.ResolveTarget(item.targetStableId, item.targetElementId);
                if (target == null) { unresolved.Add(item); continue; }

                if (string.IsNullOrEmpty(item.targetStableId) && !string.IsNullOrEmpty(target.stableId))
                {
                    item.targetStableId = target.stableId;
                    result.Notes.Add($"Recorded the stable identity of '{item.targetElementId}.{item.propertyId}', so renaming it will no longer break this override.");
                }
                else if (!string.Equals(item.targetElementId, target.elementId, StringComparison.Ordinal))
                {
                    result.Notes.Add($"Re-pointed override '{item.targetElementId}.{item.propertyId}' to '{target.elementId}' (renamed in the definition).");
                    item.targetElementId = target.elementId;
                }
            }

            foreach (var item in unresolved)
                result.Warnings.Add(resetUnresolved
                    ? $"Removed override '{item.Key}': its target no longer exists in v{definition.version}."
                    : $"Override '{item.Key}' no longer resolves against v{definition.version}. It is kept but has no effect until you reset it.");
            if (resetUnresolved)
                foreach (var item in unresolved) reference.overrides.Remove(item);

            foreach (var selection in reference.variantSelections)
                if (selection != null && definition.FindVariantProperty(selection.propertyName) == null)
                    result.Warnings.Add($"Variant selection '{selection.propertyName}' is not declared by v{definition.version}.");

            // A definition may have added variant axes since this instance was placed.
            foreach (var property in definition.variantProperties)
                if (property != null && !string.IsNullOrEmpty(property.propertyName) &&
                    reference.GetVariantSelection(property.propertyName) == null)
                {
                    reference.SetVariantSelection(property.propertyName, property.EffectiveDefault);
                    result.Notes.Add($"Added variant '{property.propertyName}' at its default '{property.EffectiveDefault}'.");
                }

            var previousVersion = reference.definitionVersion;
            reference.definitionVersion = definition.version;
            if (string.IsNullOrEmpty(reference.definitionId)) reference.definitionId = definition.componentId;
            DesignerMetadataUtility.MarkDirty(screen);

            result.Message = $"'{instance.elementId}' reconciled v{previousVersion} → v{definition.version}" +
                             (result.Notes.Count > 0 ? $"; {result.Notes.Count} override(s) repaired" : string.Empty) +
                             (result.Warnings.Count > 0 ? $" with {result.Warnings.Count} warning(s)." : ".");
            return result;
        }

        /// <summary>
        /// Records the stable identity of everything inside <paramref name="definition"/> that points at
        /// one of its own elements by id: exposed properties and variant-rule overrides.
        /// </summary>
        /// <remarks>
        /// Purely additive - an id already recorded is never re-pointed - so this is safe to run on a
        /// shared asset from a per-instance reconcile and safe to run twice. Without it, an exposed
        /// property authored before stable ids existed stays vulnerable to the very rename that
        /// <c>Update From Definition</c> is being asked to recover from.
        /// </remarks>
        public static bool BackfillDefinitionTargets(DesignerComponentDefinitionAsset definition)
        {
            if (definition == null) return false;
            var changed = false;

            foreach (var exposed in definition.exposedProperties)
            {
                if (exposed == null || !string.IsNullOrEmpty(exposed.targetStableId)) continue;
                var target = definition.Find(exposed.targetElementId);
                if (target == null || string.IsNullOrEmpty(target.stableId)) continue;
                exposed.targetStableId = target.stableId;
                changed = true;
            }

            foreach (var rule in definition.variantRules)
                foreach (var item in rule?.overrides ?? new List<DesignerComponentPropertyOverride>())
                {
                    if (item == null || !string.IsNullOrEmpty(item.targetStableId)) continue;
                    if (!string.IsNullOrEmpty(item.exposedPropertyName)) continue;
                    var target = definition.Find(item.targetElementId);
                    if (target == null || string.IsNullOrEmpty(target.stableId)) continue;
                    item.targetStableId = target.stableId;
                    changed = true;
                }

            if (changed) EditorUtility.SetDirty(definition);
            return changed;
        }

        /// <summary>
        /// Records the target's stable id on a freshly authored override, so it survives a later rename
        /// of the definition element without needing Update From Definition to repair it.
        /// </summary>
        private static void StampTargetStableId(DesignerComponentPropertyOverride item,
            DesignerComponentInstanceMetadata reference)
        {
            if (item == null || !string.IsNullOrEmpty(item.targetStableId)) return;
            if (!string.IsNullOrEmpty(item.exposedPropertyName) || string.IsNullOrEmpty(item.targetElementId)) return;

            var definition = DesignerComponentLibrary.Resolve(reference.definitionGuid, reference.definitionId);
            var target = definition?.Find(item.targetElementId);
            if (target != null) item.targetStableId = target.stableId;
        }

        // ---- Helpers ----------------------------------------------------------------------

        private static DesignerElementMetadata FindIn(DesignerMetadataAsset asset, string elementId)
            => asset != null ? asset.Find(elementId) : null;

        /// <summary>Turns <c>card--titleLabel</c> back into <c>titleLabel</c> so detached elements read naturally.</summary>
        private static string ShortenGeneratedId(string generatedId, string instanceId)
        {
            var prefix = instanceId + DesignerComponentExpander.IdSeparator;
            if (!string.IsNullOrEmpty(generatedId) && generatedId.StartsWith(prefix, StringComparison.Ordinal))
            {
                var tail = generatedId.Substring(prefix.Length).Replace(DesignerComponentExpander.IdSeparator, "_");
                if (DesignerMetadataUtility.IsValidElementId(tail)) return tail;
            }
            return generatedId;
        }

        /// <summary>Copies every serialized field of <paramref name="source"/> onto <paramref name="target"/> in place, preserving the target instance's identity in the list.</summary>
        private static void CopyInto(DesignerElementMetadata source, DesignerElementMetadata target)
        {
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), target);
        }
    }
}

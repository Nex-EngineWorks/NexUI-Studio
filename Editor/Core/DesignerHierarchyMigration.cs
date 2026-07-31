using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System;
using emiteat.NexUI.Designer.Editor.Properties;

namespace emiteat.NexUI.Designer.Editor
{
    /// <summary>
    /// Forward-only migration of a <see cref="DesignerMetadataAsset"/> to the current schema.
    ///
    /// v0 → v1 (hierarchy): pre-hierarchy assets have no explicit sibling indices. Because element
    /// rects were - and still are - stored in absolute canvas space, no <i>position</i> conversion
    /// is needed: the migration only assigns contiguous sibling indices derived from the existing
    /// <see cref="DesignerMetadataAsset.elements"/> order (the order the viewport already drew in),
    /// so an existing screen is visually identical before and after. The schemaVersion stamp makes
    /// the migration idempotent - it never runs twice on the same asset.
    /// v2 → v3 seeds typed layout/visual/typography blocks from the legacy flat fields and maps
    /// known string override paths to <see cref="DesignerPropertyId"/> without deleting the legacy data.
    /// v3 → v4 normalizes the additive component-instance block; no authored value changes.
    /// </summary>
    public static class DesignerHierarchyMigration
    {
        /// <summary>
        /// Migrates <paramref name="asset"/> in place if needed. When <paramref name="recordUndo"/>
        /// is true the change is Undo-tracked and the asset marked dirty (interactive open path);
        /// pass false from pure/test contexts. Returns true if the asset was changed.
        /// </summary>
        public static bool Migrate(DesignerMetadataAsset asset, bool recordUndo = true)
        {
            if (asset == null) return false;
            if (asset.schemaVersion >= DesignerMetadataAsset.CurrentSchemaVersion)
            {
                // Already current: still normalize defensively (cheap, only records if it changed).
                return NormalizeOnly(asset, recordUndo);
            }

            if (recordUndo) Undo.RecordObject(asset, "Migrate NexUI Metadata");

            var originalVersion = asset.schemaVersion;
            if (asset.schemaVersion < 1)
            {
                // v0 → v1: seed sibling indices from current list order within each parent group.
                var perParentCounter = new System.Collections.Generic.Dictionary<string, int>();
                foreach (var e in asset.elements)
                {
                    if (e == null) continue;
                    var key = e.parentId ?? string.Empty;
                    perParentCounter.TryGetValue(key, out var next);
                    e.siblingIndex = next;
                    perParentCounter[key] = next + 1;
                }
                asset.schemaVersion = 1;
            }

            if (asset.schemaVersion < 2)
            {
                // v1 → v2: preserve the historical backend result. Before v2 the editor-hidden
                // flag was also written as runtime active/display state.
                foreach (var e in asset.elements)
                {
                    if (e == null) continue;
                    if (string.IsNullOrEmpty(e.stableId)) e.stableId = Guid.NewGuid().ToString("N");
                    e.runtimeVisible = !e.hiddenInDesigner;
                }
                asset.schemaVersion = 2;
            }

            if (asset.schemaVersion < 3)
            {
                foreach (var e in asset.elements)
                {
                    if (e == null) continue;
                    e.layoutStyle ??= new DesignerLayoutStyleMetadata();
                    e.visualStyle ??= new DesignerVisualStyleMetadata();
                    e.typography ??= new DesignerTypographyMetadata();
                    e.layoutStyle.hasOverrides = true;
                    e.layoutStyle.overflow = e.clipChildren ? DesignerOverflowMode.Hidden : DesignerOverflowMode.Visible;
                    e.visualStyle.hasOverrides = true;
                    e.visualStyle.backgroundColor = e.tint;
                    e.visualStyle.opacity = 1f;
                    e.visualStyle.cornerRadius = ShapeRadius(e);
                    e.typography.hasOverrides = true;
                    e.typography.fontSize = e.fontSize;
                    e.typography.color = e.textColor;
                }
                foreach (var variant in asset.variants)
                    if (variant?.overrides != null)
                        foreach (var item in variant.overrides) MigrateOverride(item);
                foreach (var responsive in asset.responsiveRules)
                    if (responsive?.overrides != null)
                        foreach (var item in responsive.overrides) MigrateOverride(item);
                asset.schemaVersion = 3;
            }

            if (asset.schemaVersion < 4)
            {
                // v3 → v4 (reusable components): the componentInstance block is purely additive, so
                // no authored value changes. This step only normalizes it - Unity gives a freshly
                // deserialized v3 element a default-constructed block, but metadata built in code
                // (tests, importers, AI apply) can still leave it null. Overrides that carry no
                // resolvable property are dropped here because they can never be applied and would
                // otherwise accumulate silently; everything the user can see is preserved.
                foreach (var e in asset.elements)
                {
                    if (e == null) continue;
                    e.componentInstance ??= new DesignerComponentInstanceMetadata();
                    var overrides = e.componentInstance.overrides;
                    for (int i = overrides.Count - 1; i >= 0; i--)
                    {
                        var o = overrides[i];
                        if (o == null ||
                            (o.propertyId == DesignerPropertyId.None && string.IsNullOrEmpty(o.exposedPropertyName)))
                            overrides.RemoveAt(i);
                    }
                }
                asset.schemaVersion = 4;
            }

            if (asset.schemaVersion < 5)
            {
                foreach (var element in asset.elements)
                    if (element != null && element.attachedComponents == null)
                        element.attachedComponents = new List<DesignerAttachedComponentMetadata>();
                asset.schemaVersion = 5;
            }

            if (asset.schemaVersion < 6)
            {
                foreach (var element in asset.elements)
                    MigrateToUniversalComponents(element);
                asset.schemaVersion = 6;
            }

            DesignerHierarchyUtility.NormalizeSiblingIndices(asset);

            asset.schemaVersion = DesignerMetadataAsset.CurrentSchemaVersion;
            if (recordUndo)
            {
                EditorUtility.SetDirty(asset);
                Debug.Log($"[NexUI Designer] Migrated metadata '{asset.name}' schema v{originalVersion} → v{asset.schemaVersion}. Use Undo to restore the pre-migration state.");
            }
            return true;
        }

        /// <summary>
        /// v6: folds the type-name-only <c>attachedComponents</c> list into the real component stack,
        /// so a project MonoBehaviour and a uGUI Image are the same kind of thing to everything
        /// downstream - one Inspector, one writer, one validator.
        /// </summary>
        /// <remarks>
        /// The old list is <b>not</b> cleared. A screen migrated here still opens in a Designer build
        /// that predates the universal component system, and the components are still on it - the old
        /// build simply ignores the richer entries. The parallel write is removed one release later,
        /// once no supported build reads the old list.
        ///
        /// Entries whose type cannot be resolved are migrated anyway: the qualified name is kept so
        /// the component is reported as missing rather than quietly disappearing when a script is
        /// renamed or an assembly fails to compile.
        /// </remarks>
        private static void MigrateToUniversalComponents(DesignerElementMetadata element)
        {
            if (element == null) return;
            element.components ??= new List<DesignerElementComponent>();

            // Existing stack entries predate `source`; infer it from the registry's id namespace.
            foreach (var component in element.components)
            {
                if (component == null || string.IsNullOrEmpty(component.typeId)) continue;
                component.source = SourceForTypeId(component.typeId);
            }

            if (element.attachedComponents == null || element.attachedComponents.Count == 0) return;

            foreach (var attached in element.attachedComponents)
            {
                if (attached == null || string.IsNullOrWhiteSpace(attached.typeName)) continue;

                var typeId = DesignerProjectComponentIds.FromQualifiedName(attached.typeName);
                if (AlreadyInStack(element.components, typeId)) continue;

                element.components.Add(new DesignerElementComponent
                {
                    typeId = typeId,
                    source = DesignerComponentSource.Project,
                    assemblyQualifiedTypeName = attached.typeName,
                    enabled = true
                });
            }
        }

        private static bool AlreadyInStack(List<DesignerElementComponent> stack, string typeId)
        {
            foreach (var component in stack)
                if (component != null && component.typeId == typeId) return true;
            return false;
        }

        private static DesignerComponentSource SourceForTypeId(string typeId)
        {
            if (typeId.StartsWith("UGUI.", StringComparison.Ordinal)) return DesignerComponentSource.UGUI;
            if (typeId.StartsWith("UITK.", StringComparison.Ordinal)) return DesignerComponentSource.UIToolkit;
            if (typeId.StartsWith(DesignerProjectComponentIds.Prefix, StringComparison.Ordinal))
                return DesignerComponentSource.Project;
            if (typeId.StartsWith("Unity.", StringComparison.Ordinal)) return DesignerComponentSource.Unity;
            return DesignerComponentSource.NexUI;
        }

        private static float ShapeRadius(DesignerElementMetadata element)
        {
            switch (element.shape)
            {
                case DesignerElementShape.Rectangle: return 0f;
                case DesignerElementShape.Pill:
                case DesignerElementShape.Circle: return Mathf.Min(element.rect.width, element.rect.height) * 0.5f;
                default: return 8f;
            }
        }

        private static void MigrateOverride(DesignerVariantOverrideMetadata item)
        {
            if (item == null || item.propertyId != DesignerPropertyId.None) return;
            item.propertyId = DesignerPropertyRegistry.ResolveLegacyPath(item.propertyPath);
            if (item.propertyId != DesignerPropertyId.None)
                item.typedValue = DesignerPropertyRegistry.Parse(item.propertyId, item.value);
        }

        private static void MigrateOverride(DesignerResponsiveOverrideMetadata item)
        {
            if (item == null || item.propertyId != DesignerPropertyId.None) return;
            item.propertyId = DesignerPropertyRegistry.ResolveLegacyPath(item.propertyPath);
            if (item.propertyId != DesignerPropertyId.None)
                item.typedValue = DesignerPropertyRegistry.Parse(item.propertyId, item.value);
        }

        private static bool NormalizeOnly(DesignerMetadataAsset asset, bool recordUndo)
        {
            var changed = DesignerHierarchyUtility.NormalizeSiblingIndices(asset);
            if (changed && recordUndo)
            {
                Undo.RecordObject(asset, "Normalize NexUI Hierarchy");
                EditorUtility.SetDirty(asset);
            }
            return changed;
        }
    }
}

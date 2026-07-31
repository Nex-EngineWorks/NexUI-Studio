using System.Collections.Generic;
using emiteat.NexUI.Components;

namespace emiteat.NexUI.Designer.Editor.Components
{
    /// <summary>
    /// Turns an element's authored <c>items.*</c> properties into the runtime
    /// <see cref="NXCollectionOptions"/> both backends and the canvas preview use.
    /// </summary>
    /// <remarks>
    /// One reader rather than one per writer: the uGUI prefab, the generated UXML, the canvas
    /// preview and Validation must agree on what the settings mean, and they only can if they read
    /// them the same way. This is also where the pre-CollectionView keys are migrated, so no
    /// existing screen has to be rewritten to open in this build.
    /// </remarks>
    public static class DesignerCollectionOptions
    {
        /// <summary>Item template slot ids used across the collection presets.</summary>
        private static readonly string[] TemplateSlotIds =
            { "item", "slot", "cell", "entry", "row", "tab", "result", "offer" };

        /// <summary>Reads the options an element declares. Never returns null.</summary>
        public static NXCollectionOptions Read(DesignerElementMetadata element)
        {
            var options = new NXCollectionOptions();
            if (element == null) return options;

            options.Layout = ReadLayout(element);
            options.Virtualization = ReadVirtualization(element);
            options.Selection = (NXSelectionMode)Clamp(
                DesignerComponentPropertyAccess.GetInt(element, "items.selection", (int)NXSelectionMode.Single), 0, 2);
            options.Paging = (NXPagingMode)Clamp(
                DesignerComponentPropertyAccess.GetInt(element, "items.paging", (int)NXPagingMode.None), 0, 3);

            options.ItemSize = DesignerComponentPropertyAccess.GetFloat(element, "items.itemSize", 64f);
            options.ItemCrossSize = DesignerComponentPropertyAccess.GetFloat(element, "items.itemCrossSize", 64f);
            options.Spacing = DesignerComponentPropertyAccess.GetFloat(element, "items.spacing", 4f);
            options.CrossSpacing = DesignerComponentPropertyAccess.GetFloat(element, "items.crossSpacing", 4f);
            options.ColumnCount = DesignerComponentPropertyAccess.GetInt(element, "items.columns", 4);
            options.AutoColumns = DesignerComponentPropertyAccess.GetBool(element, "items.autoColumns");
            options.Overscan = DesignerComponentPropertyAccess.GetInt(element, "items.overscan", 2);
            options.ScrollSelectionIntoView =
                DesignerComponentPropertyAccess.GetBool(element, "items.scrollSelectionIntoView", true);

            var interactions = NXCollectionInteractions.None;
            if (DesignerComponentPropertyAccess.GetBool(element, "items.activate", true))
                interactions |= NXCollectionInteractions.Activate;
            if (DesignerComponentPropertyAccess.GetBool(element, "items.reorderable"))
                interactions |= NXCollectionInteractions.Reorder;
            if (DesignerComponentPropertyAccess.GetBool(element, "items.dragAndDrop"))
                interactions |= NXCollectionInteractions.DragAndDrop;
            if (DesignerComponentPropertyAccess.GetBool(element, "items.contextRequest"))
                interactions |= NXCollectionInteractions.ContextRequest;
            options.Interactions = interactions;

            // An item size of 0 used to mean "measure every item"; that is Dynamic Size now.
            if (options.ItemSize <= 0f)
            {
                options.ItemSize = 64f;
                if (options.Virtualization == NXVirtualizationMode.FixedSize)
                    options.Virtualization = NXVirtualizationMode.DynamicSize;
            }

            return options;
        }

        /// <summary>The runtime state key that supplies the items, or null when none is bound.</summary>
        public static string SourceKey(DesignerElementMetadata element)
        {
            var value = DesignerComponentPropertyAccess.GetString(element, "items.source", null);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>Items generated on the canvas so an unbound collection is not drawn empty.</summary>
        public static int PreviewCount(DesignerElementMetadata element)
            => DesignerComponentPropertyAccess.GetInt(element, "items.previewCount");

        public static bool ShowsEmptyState(DesignerElementMetadata element)
            => DesignerComponentPropertyAccess.GetBool(element, "items.showEmptyState", true);

        /// <summary>True when this element runs on the CollectionView system (Core or preset).</summary>
        public static bool IsCollection(DesignerElementMetadata element)
        {
            if (element == null) return false;
            var descriptor = DesignerComponentRegistry.Get(element.elementType);
            return descriptor.IsCollectionComponent
                   || descriptor.TypeId == "CollectionView"
                   || descriptor.BaseTypeId == "CollectionView";
        }

        /// <summary>
        /// The child that acts as the item template, or null. Presets name the slot differently
        /// ("slot" for an inventory, "cell" for a grid), so every known template slot is accepted.
        /// </summary>
        public static DesignerElementMetadata FindTemplate(DesignerMetadataAsset metadata, DesignerElementMetadata element)
        {
            if (metadata == null || element == null) return null;
            foreach (var child in metadata.elements)
            {
                if (child == null || child.parentId != element.elementId) continue;
                if (string.IsNullOrEmpty(child.parentSlotId)) continue;
                for (var i = 0; i < TemplateSlotIds.Length; i++)
                    if (child.parentSlotId == TemplateSlotIds[i]) return child;
            }
            return null;
        }

        /// <summary>The child placed in a named state slot (empty / loading / error), or null.</summary>
        public static DesignerElementMetadata FindStateChild(DesignerMetadataAsset metadata,
            DesignerElementMetadata element, string slotId)
        {
            if (metadata == null || element == null || string.IsNullOrEmpty(slotId)) return null;
            foreach (var child in metadata.elements)
                if (child != null && child.parentId == element.elementId && child.parentSlotId == slotId)
                    return child;
            return null;
        }

        /// <summary>Problems with the authored combination, in the runtime's own words.</summary>
        public static List<string> Problems(DesignerElementMetadata element)
        {
            var problems = new List<string>();
            Read(element).Validate(problems);
            return problems;
        }

        // ---- Legacy-aware readers -------------------------------------------------------------

        private static NXCollectionLayout ReadLayout(DesignerElementMetadata element)
        {
            if (DesignerComponentPropertyAccess.IsOverridden(element, "items.layout"))
                return (NXCollectionLayout)Clamp(
                    DesignerComponentPropertyAccess.GetInt(element, "items.layout"), 0, 3);

            // Before Layout existed there was only Orientation: Horizontal(0) / Vertical(1).
            if (DesignerComponentPropertyAccess.IsOverridden(element, "items.orientation")
                && DesignerComponentPropertyAccess.GetInt(element, "items.orientation", 1) == 0)
                return NXCollectionLayout.Horizontal;

            return (NXCollectionLayout)Clamp(DesignerComponentPropertyAccess.GetInt(element, "items.layout"), 0, 3);
        }

        private static NXVirtualizationMode ReadVirtualization(DesignerElementMetadata element)
        {
            if (DesignerComponentPropertyAccess.IsOverridden(element, "items.virtualization"))
                return (NXVirtualizationMode)Clamp(
                    DesignerComponentPropertyAccess.GetInt(element, "items.virtualization"), 0, 2);

            // Before the enum existed virtualization was a bool.
            if (DesignerComponentPropertyAccess.IsOverridden(element, "items.virtualize")
                && !DesignerComponentPropertyAccess.GetBool(element, "items.virtualize", true))
                return NXVirtualizationMode.None;

            return (NXVirtualizationMode)Clamp(
                DesignerComponentPropertyAccess.GetInt(element, "items.virtualization", (int)NXVirtualizationMode.FixedSize),
                0, 2);
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}

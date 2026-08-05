using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Components.Definitions;
using Unity.Profiling;

namespace emiteat.NexUI.Designer.Editor
{
    /// <summary>
    /// Reusable-component surface of the context: the cached expansion the canvas draws from, and the
    /// mapping back to authored elements.
    ///
    /// The Designer edits <b>authored</b> metadata (instances stay one element with a reference), while
    /// the canvas, the serializers and Validation all consume the <b>expanded</b> tree. Keeping the two
    /// apart here - rather than materializing definition elements into the screen - is what makes
    /// "edit the definition once, every instance updates" true without a propagation pass, and it
    /// guarantees no expansion artefact can ever be written back into the user's asset.
    /// </summary>
    public sealed partial class NexUIDesignerContext
    {
        private static readonly ProfilerMarker ExpansionMarker = new ProfilerMarker("NexUI.Designer.ComponentExpansion");

        private DesignerComponentExpansion _expansion;
        private bool _expansionValid;
        private bool _componentLibraryHooked;

        /// <summary>
        /// Elements the canvas should draw: the authored list when the screen uses no components, or
        /// the flattened tree when it does. Never mutate the returned elements - generated entries are
        /// throw-away copies and authored entries are the real asset data.
        /// </summary>
        public IReadOnlyList<DesignerElementMetadata> PreviewElements
        {
            get
            {
                var expansion = Expansion;
                if (expansion?.Expanded == null)
                    return Metadata != null ? (IReadOnlyList<DesignerElementMetadata>)Metadata.elements : System.Array.Empty<DesignerElementMetadata>();
                return expansion.Expanded.elements;
            }
        }

        /// <summary>Issues raised while expanding component instances (missing definition, cycle, slot violations...).</summary>
        public IReadOnlyList<DesignerComponentExpansionIssue> ComponentIssues
            => Expansion?.Issues ?? (IReadOnlyList<DesignerComponentExpansionIssue>)System.Array.Empty<DesignerComponentExpansionIssue>();

        public bool HasComponentInstances => DesignerComponentExpander.HasInstances(Metadata);

        /// <summary>True when the element came from a definition rather than the screen - it is drawn but not selectable or editable.</summary>
        public bool IsGeneratedByComponent(DesignerElementMetadata element)
            => element != null && Expansion != null && Expansion.IsGenerated(element.elementId);

        /// <summary>
        /// Maps an element from <see cref="PreviewElements"/> back to the authored element the user can
        /// select. Instance roots map to their authored instance; generated children map to null.
        /// </summary>
        public DesignerElementMetadata ResolveAuthoredElement(DesignerElementMetadata previewElement)
        {
            if (previewElement == null || Metadata == null) return null;
            var authored = Metadata.Find(previewElement.elementId);
            return authored;
        }

        /// <summary>
        /// Returns the authored element that owns a preview element. Ordinary elements and component
        /// roots map to themselves; generated definition children map to their authored instance.
        /// This is for moving the whole visible component together, not for making generated children
        /// independently editable.
        /// </summary>
        public DesignerElementMetadata ResolveAuthoredOwner(DesignerElementMetadata previewElement)
        {
            var authored = ResolveAuthoredElement(previewElement);
            if (authored != null || previewElement == null || Metadata == null) return authored;

            var expansion = Expansion;
            if (expansion != null &&
                expansion.OwnerInstanceByElementId.TryGetValue(previewElement.elementId, out var ownerElementId))
                return Metadata.Find(ownerElementId);
            return null;
        }

        private DesignerComponentExpansion Expansion
        {
            get
            {
                EnsureComponentLibraryHook();
                if (_expansionValid) return _expansion;
                using var scope = ExpansionMarker.Auto();
                _expansion?.Dispose();
                _expansion = DesignerComponentExpander.Expand(Metadata, DesignerComponentLibrary.Resolver, VariantContext);
                _expansionValid = true;
                return _expansion;
            }
        }

        private void EnsureComponentLibraryHook()
        {
            if (_componentLibraryHooked) return;
            _componentLibraryHooked = true;
            // Editing a definition asset must repaint every open screen that instantiates it.
            DesignerComponentLibrary.Changed += InvalidateComponentExpansion;
        }

        /// <summary>Drops the cached expansion. Cheap: the next canvas rebuild or save recomputes it.</summary>
        public void InvalidateComponentExpansion()
        {
            _expansionValid = false;
            if (_disposed) return;
            CanvasChanged?.Invoke();
        }

        private void DisposeComponentExpansion()
        {
            if (_componentLibraryHooked)
            {
                DesignerComponentLibrary.Changed -= InvalidateComponentExpansion;
                _componentLibraryHooked = false;
            }
            _expansion?.Dispose();
            _expansion = null;
            _expansionValid = false;
        }
    }
}

using System;

namespace emiteat.NexUI.Designer
{
    /// <summary>What a <see cref="DesignerObjectReference"/> points at.</summary>
    public enum DesignerReferenceKind
    {
        /// <summary>Nothing assigned.</summary>
        None,

        /// <summary>Another element on this screen, resolved through its stable id.</summary>
        Element,

        /// <summary>A project asset, resolved through its GUID and local file id.</summary>
        Asset
    }

    /// <summary>
    /// A reference stored in a component's serialized property - the thing that makes a user's
    /// <c>[SerializeField] private Image fill;</c> usable from the Designer.
    /// </summary>
    /// <remarks>
    /// References are stored by identity, never by name or hierarchy index. A name changes when the
    /// designer renames an element and an index changes when anything is reordered; a stable id
    /// survives both, which is what lets duplication and Definition instancing re-map internal
    /// references correctly instead of leaving every copy pointing at the original.
    ///
    /// <see cref="componentTypeName"/> disambiguates which component on the target element is meant,
    /// because a field of type <c>Image</c> and a field of type <c>Button</c> can both point at the
    /// same element. It is empty when the field wants the GameObject or the RectTransform itself.
    ///
    /// A scene object cannot be referenced from a prefab, so there is deliberately no Scene kind:
    /// the writer reports that case rather than storing something that cannot be saved.
    /// </remarks>
    [Serializable]
    public sealed class DesignerObjectReference
    {
        public DesignerReferenceKind kind = DesignerReferenceKind.None;

        /// <summary>Target element's <c>stableId</c> when <see cref="kind"/> is Element.</summary>
        public string stableElementId;

        /// <summary>
        /// Assembly-qualified name of the component wanted on that element. Empty means the
        /// element's own GameObject or RectTransform, whichever the field's type asks for.
        /// </summary>
        public string componentTypeName;

        /// <summary>Asset GUID when <see cref="kind"/> is Asset.</summary>
        public string assetGuid;

        /// <summary>Local file id inside that asset, for sub-assets such as a sprite in an atlas.</summary>
        public long localFileId;

        public bool IsAssigned => kind != DesignerReferenceKind.None;

        public DesignerObjectReference Clone() => new DesignerObjectReference
        {
            kind = kind,
            stableElementId = stableElementId,
            componentTypeName = componentTypeName,
            assetGuid = assetGuid,
            localFileId = localFileId
        };

        /// <summary>
        /// Re-points an element reference through a stable-id map, for duplication and Definition
        /// instancing. A reference outside the copied set is left alone: pointing a duplicate at the
        /// original is right when the target was never copied.
        /// </summary>
        public void Remap(System.Collections.Generic.IReadOnlyDictionary<string, string> stableIdMap)
        {
            if (kind != DesignerReferenceKind.Element || string.IsNullOrEmpty(stableElementId)) return;
            if (stableIdMap != null && stableIdMap.TryGetValue(stableElementId, out var mapped))
                stableElementId = mapped;
        }

        public override string ToString() => kind switch
        {
            DesignerReferenceKind.Element => $"Element({stableElementId}{(string.IsNullOrEmpty(componentTypeName) ? "" : ":" + componentTypeName)})",
            DesignerReferenceKind.Asset => $"Asset({assetGuid}:{localFileId})",
            _ => "None"
        };
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Components.Serialization
{
    /// <summary>
    /// Reads and writes the two kinds of reference a component field can hold: another element on
    /// this screen, or a project asset.
    /// </summary>
    /// <remarks>
    /// A scene object is deliberately not a third kind. It cannot be stored in a prefab, so accepting
    /// one would produce a screen that looks correct in the editor and comes up with a null reference
    /// in a build - the writer reports the case instead.
    /// </remarks>
    public static class StudioReferenceUtility
    {
        /// <summary>The real <see cref="Type"/> behind a stack entry, or null when it cannot be resolved.</summary>
        public static Type ResolveComponentType(DesignerElementComponent component)
        {
            if (component == null) return null;

            if (!string.IsNullOrEmpty(component.assemblyQualifiedTypeName))
            {
                var resolved = StudioComponentTypeIndex.Resolve(component.assemblyQualifiedTypeName);
                if (resolved != null) return resolved;
            }
            return DesignerUIComponentRegistry.Get(component.typeId)?.BackingType;
        }

        /// <summary>
        /// Whether a field of <paramref name="fieldType"/> can point at an element rather than an
        /// asset - that is, whether it wants a GameObject or a Component.
        /// </summary>
        public static bool CanTargetElement(Type fieldType)
            => fieldType != null
               && (fieldType == typeof(GameObject) || typeof(Component).IsAssignableFrom(fieldType));

        /// <summary>
        /// Elements on <paramref name="metadata"/> that can satisfy a field of
        /// <paramref name="fieldType"/>, with the component that supplies it.
        /// </summary>
        public static List<(DesignerElementMetadata Element, Type Component)> CompatibleElements(
            DesignerMetadataAsset metadata, Type fieldType)
        {
            var matches = new List<(DesignerElementMetadata, Type)>();
            if (metadata?.elements == null || fieldType == null) return matches;

            // Every element becomes a GameObject with a RectTransform, so those two always match
            // without needing anything in the stack.
            var intrinsic = fieldType == typeof(GameObject)
                            || fieldType == typeof(Transform)
                            || fieldType == typeof(RectTransform);

            foreach (var element in metadata.elements)
            {
                if (element == null) continue;
                if (intrinsic) { matches.Add((element, null)); continue; }

                foreach (var component in element.components ?? new List<DesignerElementComponent>())
                {
                    var type = ResolveComponentType(component);
                    if (type == null || !fieldType.IsAssignableFrom(type)) continue;
                    matches.Add((element, type));
                    break;
                }
            }
            return matches;
        }

        // ---- Asset references --------------------------------------------------------------------

        /// <summary>Captures <paramref name="asset"/> as a GUID + local id, or None when it is null.</summary>
        public static DesignerObjectReference FromAsset(UnityEngine.Object asset)
        {
            if (asset == null) return new DesignerObjectReference();
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localId))
                return new DesignerObjectReference();

            return new DesignerObjectReference
            {
                kind = DesignerReferenceKind.Asset,
                assetGuid = guid,
                localFileId = localId,
                componentTypeName = asset.GetType().FullName
            };
        }

        /// <summary>Loads the asset a reference points at, or null when it is missing or not an asset.</summary>
        public static UnityEngine.Object ResolveAsset(DesignerObjectReference reference)
        {
            if (reference == null || reference.kind != DesignerReferenceKind.Asset) return null;
            if (string.IsNullOrEmpty(reference.assetGuid)) return null;

            var path = AssetDatabase.GUIDToAssetPath(reference.assetGuid);
            if (string.IsNullOrEmpty(path)) return null;

            // A sprite inside an atlas or a mesh inside an FBX is a sub-asset, so matching the local
            // file id is what distinguishes it from its container.
            foreach (var candidate in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (candidate == null) continue;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out _, out long localId)) continue;
                if (localId == reference.localFileId) return candidate;
            }
            return AssetDatabase.LoadMainAssetAtPath(path);
        }

        /// <summary>Whether a scene object was handed in - a reference a prefab could never keep.</summary>
        public static bool IsSceneObject(UnityEngine.Object candidate)
            => candidate != null && !EditorUtility.IsPersistent(candidate);

        // ---- Element references ------------------------------------------------------------------

        public static DesignerObjectReference ToElement(DesignerElementMetadata element, Type componentType)
        {
            if (element == null) return new DesignerObjectReference();
            return new DesignerObjectReference
            {
                kind = DesignerReferenceKind.Element,
                stableElementId = element.stableId,
                componentTypeName = componentType == null
                    ? string.Empty
                    : StudioComponentTypeIndex.Identity(componentType)
            };
        }

        public static DesignerElementMetadata FindElement(DesignerMetadataAsset metadata, string stableId)
        {
            if (metadata?.elements == null || string.IsNullOrEmpty(stableId)) return null;
            foreach (var element in metadata.elements)
                if (element != null && element.stableId == stableId) return element;
            return null;
        }

        /// <summary>Short human-readable label for a reference, for inspector rows and reports.</summary>
        public static string Describe(DesignerObjectReference reference, DesignerMetadataAsset metadata)
        {
            if (reference == null || !reference.IsAssigned) return "None";

            if (reference.kind == DesignerReferenceKind.Element)
            {
                var element = FindElement(metadata, reference.stableElementId);
                if (element == null) return "Missing element";
                var name = string.IsNullOrWhiteSpace(element.displayName) ? element.elementId : element.displayName;
                var component = StudioComponentTypeIndex.Resolve(reference.componentTypeName);
                return component == null ? name : $"{name} ({component.Name})";
            }

            var asset = ResolveAsset(reference);
            return asset == null ? "Missing asset" : asset.name;
        }
    }
}

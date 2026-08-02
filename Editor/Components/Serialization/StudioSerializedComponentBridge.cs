using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Components.Serialization
{
    /// <summary>
    /// Moves values between a Studio component's property bag and a real
    /// <see cref="SerializedObject"/> on the scratch host.
    /// </summary>
    /// <remarks>
    /// This is the piece that makes "edit any MonoBehaviour" true rather than "edit the seven types
    /// the Studio hard-codes". The inspector, the prefab writer and the round-trip test all go through
    /// the same two methods, so what the user sees, what is saved and what lands on the prefab cannot
    /// drift apart.
    /// </remarks>
    public static class StudioSerializedComponentBridge
    {
        public const string ScriptProperty = "m_Script";

        /// <summary>
        /// Every leaf property of <paramref name="serializedObject"/>: the values that actually hold
        /// data, with composite structs and arrays walked into their parts.
        /// </summary>
        /// <remarks>
        /// Only <see cref="SerializedPropertyType.Generic"/> is descended into. A Vector3 or a Color
        /// is stored whole rather than as three or four separate floats, which keeps the metadata
        /// readable and means a single edit is a single entry.
        /// </remarks>
        public static IEnumerable<SerializedProperty> Leaves(SerializedObject serializedObject)
        {
            if (serializedObject == null) yield break;

            var iterator = serializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = iterator.propertyType == SerializedPropertyType.Generic;
                if (enterChildren) continue;
                if (iterator.propertyPath == ScriptProperty) continue;
                yield return iterator.Copy();
            }
        }

        /// <summary>
        /// Property paths of every UnityEvent field on <paramref name="serializedObject"/>.
        /// </summary>
        /// <remarks>
        /// UnityEvents are owned by <see cref="StudioUnityEventModel"/>, not by the scratch object: a
        /// persistent call usually targets another element, which the scratch object cannot hold. The
        /// editor therefore has to leave these subtrees alone in both directions - capturing them from
        /// an always-empty scratch list would delete every call the user authored.
        /// </remarks>
        public static HashSet<string> UnityEventPaths(SerializedObject serializedObject)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            if (serializedObject == null) return paths;

            var iterator = serializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                if (iterator.propertyType != SerializedPropertyType.Generic) { enterChildren = false; continue; }

                if (StudioUnityEventModel.IsUnityEvent(StudioPropertyReflection.FieldTypeOf(iterator)))
                {
                    paths.Add(iterator.propertyPath);
                    enterChildren = false; // its innards belong to the event model
                    continue;
                }
                enterChildren = true;
            }
            return paths;
        }

        private static bool IsUnder(string path, HashSet<string> roots)
        {
            foreach (var root in roots)
                if (path.Length > root.Length && path[root.Length] == '.' &&
                    path.StartsWith(root, StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>
        /// Builds a <see cref="SerializedObject"/> over a scratch instance of <paramref name="type"/>
        /// carrying the values stored on <paramref name="component"/>.
        /// </summary>
        /// <param name="unsupported">
        /// Property keys whose stored value could not be applied. They are reported, never cleared -
        /// a value this build does not understand still belongs to the user.
        /// </param>
        public static SerializedObject Load(DesignerElementComponent component, Type type,
            List<string> unsupported = null)
        {
            var target = StudioScratchComponentHost.RentEditable(type);
            if (target == null) return null;

            // The scratch instance is pooled per type and therefore still holds the previous element's
            // values; resetting to the pristine copy is what stops them leaking across a selection.
            var pristine = StudioScratchComponentHost.RentPristine(type);
            if (pristine != null) EditorUtility.CopySerialized(pristine, target);

            var serializedObject = new SerializedObject(target);
            if (component?.properties == null) return serializedObject;

            var events = UnityEventPaths(serializedObject);
            foreach (var entry in component.properties)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key) || entry.value == null) continue;
                if (IsUnder(entry.key, events)) continue;

                var property = serializedObject.FindProperty(entry.key);
                if (property == null)
                {
                    unsupported?.Add(entry.key);
                    continue;
                }

                if (StudioPropertyValueCodec.IsReference(property))
                {
                    // An element reference has no object to point at until the prefab is written, so
                    // the scratch field stays null and the reference row draws the real target.
                    var reference = entry.value.reference;
                    if (reference != null && reference.kind == DesignerReferenceKind.Asset)
                        StudioPropertyValueCodec.SetReference(property, StudioReferenceUtility.ResolveAsset(reference));
                    continue;
                }

                if (!StudioPropertyValueCodec.TryDecode(entry.value, property))
                    unsupported?.Add(entry.key);
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            serializedObject.Update();
            return serializedObject;
        }

        /// <summary>
        /// Writes back every value that differs from Unity's own default for the type, and clears the
        /// ones that no longer do.
        /// </summary>
        /// <remarks>
        /// Storing the diff rather than the full object is what keeps the metadata small, keeps a
        /// prefab from being stamped with the Studio's defaults, and makes "Reset Property" mean
        /// exactly "stop storing this".
        /// </remarks>
        /// <returns>True when the bag changed.</returns>
        public static bool Capture(SerializedObject serializedObject, Type type,
            DesignerElementComponent component, List<string> unsupported = null)
        {
            if (serializedObject == null || component == null) return false;

            var pristine = StudioScratchComponentHost.RentPristine(type);
            var defaults = pristine != null ? new SerializedObject(pristine) : null;

            component.properties ??= new List<DesignerComponentPropertyEntry>();
            var changed = false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var events = UnityEventPaths(serializedObject);

            foreach (var property in Leaves(serializedObject))
            {
                var key = property.propertyPath;
                if (IsUnder(key, events)) continue;
                seen.Add(key);

                if (StudioPropertyValueCodec.IsReference(property))
                {
                    if (CaptureReference(property, key, component, unsupported)) changed = true;
                    continue;
                }

                var baseline = defaults?.FindProperty(key);
                if (baseline != null && SerializedProperty.DataEquals(property, baseline))
                {
                    if (DesignerComponentPropertyBag.Set(component.properties, key, null)) changed = true;
                    continue;
                }

                if (!StudioPropertyValueCodec.TryEncode(property, out var value))
                {
                    unsupported?.Add(key);
                    continue;
                }
                if (!Equals(DesignerComponentPropertyBag.Find(component.properties, key), value))
                {
                    DesignerComponentPropertyBag.Set(component.properties, key, value);
                    changed = true;
                }
            }

            // Entries for paths the type no longer has are kept: the script may simply be mid-rename,
            // and deleting the value would make that rename destructive.
            return changed;
        }

        /// <summary>
        /// Stores an object-reference field. An element reference already in the bag wins over the
        /// null the scratch object necessarily holds for it, so drawing the inspector never erases one.
        /// </summary>
        private static bool CaptureReference(SerializedProperty property, string key,
            DesignerElementComponent component, List<string> unsupported)
        {
            var stored = DesignerComponentPropertyBag.Find(component.properties, key);
            var target = StudioPropertyValueCodec.GetReference(property);

            if (target == null)
            {
                if (stored?.reference != null && stored.reference.kind == DesignerReferenceKind.Element)
                    return false;
                return DesignerComponentPropertyBag.Set(component.properties, key, null);
            }

            if (StudioReferenceUtility.IsSceneObject(target))
            {
                // A scene object cannot be saved into a prefab. Reporting it beats storing something
                // that would come back null in a build.
                unsupported?.Add(key);
                return false;
            }

            var reference = StudioReferenceUtility.FromAsset(target);
            if (!reference.IsAssigned)
            {
                unsupported?.Add(key);
                return false;
            }
            if (stored?.reference != null &&
                stored.reference.kind == DesignerReferenceKind.Asset &&
                stored.reference.assetGuid == reference.assetGuid &&
                stored.reference.localFileId == reference.localFileId)
                return false;

            DesignerComponentPropertyBag.Set(component.properties, key, new DesignerPropertyValue
            {
                type = DesignerPropertyValueType.AssetReference,
                assetValue = target,
                reference = reference
            });
            return true;
        }

        /// <summary>
        /// Reads a live component into a metadata property bag - the direction Prefab Import needs.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="Capture"/>, references are resolved through
        /// <paramref name="resolveElement"/> first: an <c>Image</c> field pointing at a sibling inside
        /// the same prefab has to become an element reference, not an asset one, or re-saving would
        /// point every imported copy back at the object it was imported from.
        /// </remarks>
        /// <param name="resolveElement">
        /// Maps an object inside the imported prefab to the stable id of the element being created for
        /// it. Returns null for anything outside the prefab.
        /// </param>
        public static bool CaptureFrom(Component source, DesignerElementComponent component,
            Func<UnityEngine.Object, string> resolveElement, List<string> unsupported = null)
        {
            if (source == null || component == null) return false;

            var serializedObject = new SerializedObject(source);
            var type = source.GetType();
            var pristine = StudioScratchComponentHost.RentPristine(type);
            var defaults = pristine != null ? new SerializedObject(pristine) : null;

            component.properties ??= new List<DesignerComponentPropertyEntry>();
            component.valueFormat = DesignerComponentValueFormat.PropertyPath;
            var changed = false;

            foreach (var property in Leaves(serializedObject))
            {
                var key = property.propertyPath;

                if (StudioPropertyValueCodec.IsReference(property))
                {
                    if (ImportReference(property, key, component, resolveElement, unsupported)) changed = true;
                    continue;
                }

                var baseline = defaults?.FindProperty(key);
                if (baseline != null && SerializedProperty.DataEquals(property, baseline)) continue;

                if (!StudioPropertyValueCodec.TryEncode(property, out var value))
                {
                    unsupported?.Add(key);
                    continue;
                }
                DesignerComponentPropertyBag.Set(component.properties, key, value);
                changed = true;
            }
            return changed;
        }

        private static bool ImportReference(SerializedProperty property, string key,
            DesignerElementComponent component, Func<UnityEngine.Object, string> resolveElement,
            List<string> unsupported)
        {
            var target = StudioPropertyValueCodec.GetReference(property);
            if (target == null) return false;

            var stableId = resolveElement?.Invoke(target);
            if (!string.IsNullOrEmpty(stableId))
            {
                DesignerComponentPropertyBag.Set(component.properties, key, new DesignerPropertyValue
                {
                    type = DesignerPropertyValueType.ElementReference,
                    reference = new DesignerObjectReference
                    {
                        kind = DesignerReferenceKind.Element,
                        stableElementId = stableId,
                        // Recording the concrete type is what lets the writer pick the right component
                        // when the target element carries several - and, for a UnityEvent target
                        // declared as UnityEngine.Object, it is the only way to say "the GameObject".
                        componentTypeName = StudioComponentTypeIndex.Identity(target.GetType())
                    }
                });
                return true;
            }

            var asset = StudioReferenceUtility.FromAsset(target);
            if (!asset.IsAssigned)
            {
                unsupported?.Add(key);
                return false;
            }
            DesignerComponentPropertyBag.Set(component.properties, key, new DesignerPropertyValue
            {
                type = DesignerPropertyValueType.AssetReference,
                assetValue = target,
                reference = asset
            });
            return true;
        }

        /// <summary>
        /// Whether two stored values carry the same content. Used to keep repeated captures from
        /// marking the asset dirty when nothing actually moved.
        /// </summary>
        private static bool Equals(DesignerPropertyValue left, DesignerPropertyValue right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null) return false;
            return left.type == right.type
                   && left.floatValue.Equals(right.floatValue)
                   && left.intValue == right.intValue
                   && left.boolValue == right.boolValue
                   && string.Equals(left.stringValue, right.stringValue, StringComparison.Ordinal)
                   && left.colorValue == right.colorValue
                   && left.vector2Value == right.vector2Value
                   && string.Equals(left.json, right.json, StringComparison.Ordinal);
        }
    }
}

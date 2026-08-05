using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Components
{
    /// <summary>
    /// Attach, detach, reorder and configure the components on an element - the operations Unity's
    /// component menu performs on a GameObject.
    /// </summary>
    /// <remarks>
    /// The rules mirror Unity's: a type marked single-instance cannot be added twice, required
    /// components come along automatically, and conflicting components (two Graphics, two layout
    /// groups) are refused with a reason rather than silently producing a broken element. Callers own
    /// the Undo group - every mutation here is a plain in-place edit.
    /// </remarks>
    public static class DesignerElementComponentAccess
    {
        public const string CoreElement = "Core.Element";

        public static IReadOnlyList<DesignerElementComponent> Components(DesignerElementMetadata element)
            => element?.components ?? (IReadOnlyList<DesignerElementComponent>)System.Array.Empty<DesignerElementComponent>();

        public static DesignerElementComponent Find(DesignerElementMetadata element, string instanceId)
        {
            if (element?.components == null) return null;
            foreach (var component in element.components)
                if (component != null && component.instanceId == instanceId) return component;
            return null;
        }

        public static bool Has(DesignerElementMetadata element, string typeId)
        {
            if (element?.components == null) return false;
            foreach (var component in element.components)
                if (component != null && component.typeId == typeId) return true;
            return false;
        }

        /// <summary>Every element carries the core element component, the way every GameObject has a transform.</summary>
        public static void EnsureCore(DesignerElementMetadata element)
        {
            if (element == null) return;
            element.components ??= new List<DesignerElementComponent>();
            if (Has(element, CoreElement)) return;
            element.components.Insert(0, new DesignerElementComponent(CoreElement) { fromPreset = true });
        }

        /// <summary>Why <paramref name="typeId"/> cannot be attached, or null when it can.</summary>
        public static string AttachBlockedReason(DesignerElementMetadata element, string typeId,
            DesignerUIComponentFamily backend)
        {
            var type = DesignerUIComponentRegistry.Get(typeId);
            if (type == null) return "Unknown component type.";
            if (!type.SupportsBackend(backend))
                return $"{type.DisplayName} does not run on a {backend} screen.";
            if (!type.AllowMultiple && Has(element, typeId))
                return $"{type.DisplayName} can only be added once.";

            foreach (var conflict in type.ConflictsWith)
            {
                if (conflict.EndsWith("*", System.StringComparison.Ordinal))
                {
                    var prefix = conflict.Substring(0, conflict.Length - 1);
                    foreach (var existing in Components(element))
                        if (existing.typeId != typeId && existing.typeId != null &&
                            existing.typeId.StartsWith(prefix, System.StringComparison.Ordinal))
                            return $"{type.DisplayName} conflicts with {DisplayNameOf(existing.typeId)}.";
                    continue;
                }
                if (Has(element, conflict))
                    return $"{type.DisplayName} conflicts with {DisplayNameOf(conflict)}.";
            }
            return null;
        }

        public static bool CanAttach(DesignerElementMetadata element, string typeId, DesignerUIComponentFamily backend)
            => AttachBlockedReason(element, typeId, backend) == null;

        /// <summary>
        /// Adds the component (and anything it requires). Returns the new instance, or null when the
        /// attachment was refused.
        /// </summary>
        public static DesignerElementComponent Attach(DesignerElementMetadata element, string typeId,
            DesignerUIComponentFamily backend, bool fromPreset = false)
        {
            if (element == null) return null;
            element.components ??= new List<DesignerElementComponent>();
            if (!CanAttach(element, typeId, backend)) return null;

            var type = DesignerUIComponentRegistry.Get(typeId);
            foreach (var required in type.RequiredComponents)
            {
                if (Has(element, required) || !CanAttach(element, required, backend)) continue;
                var dependency = new DesignerElementComponent(required, fromPreset);
                UseSerializedValues(dependency, DesignerUIComponentRegistry.Get(required));
                element.components.Add(dependency);
            }

            var component = new DesignerElementComponent(typeId, fromPreset);
            UseSerializedValues(component, type);
            element.components.Add(component);
            return component;
        }

        /// <summary>
        /// Points a registry component at the universal SerializedObject value path when it has a real
        /// runtime type behind it.
        /// </summary>
        /// <remarks>
        /// The curated schema is built by reflecting the backing type, and it can only describe the
        /// field shapes <c>DesignerReflectedSchema</c> knows how to name - bool, int, float, string,
        /// Color, Vector2, enum and object references. Everything else was dropped, which is why
        /// <c>TMP_Text.m_margin</c> (Vector4) and <c>LayoutGroup.m_Padding</c> (RectOffset) could
        /// neither be edited nor saved, while the very same field on an unregistered script could.
        ///
        /// Storing by property path instead hands both the inspector and the writer to the universal
        /// path, which covers every shape <c>SerializedProperty</c> has. The registry entry keeps doing
        /// what only it can do - display name, category, backend filtering, requires/conflicts - so
        /// nothing about Add Component changes.
        ///
        /// <see cref="DesignerElementComponent.adoptExistingComponent"/> is what keeps this safe on the
        /// prefab: the old writer called <c>GetComponent</c> before <c>AddComponent</c>, so a preset
        /// that stamps <c>UGUI.Image</c> onto an element the serializer also gives an Image to must
        /// bind to that one instead of adding a second.
        ///
        /// Existing metadata is untouched. It was written with <see cref="DesignerComponentValueFormat.SchemaKeys"/>
        /// and still round-trips through the registry writer, so this only changes newly attached
        /// components.
        /// </remarks>
        private static void UseSerializedValues(DesignerElementComponent component, DesignerUIComponentType type)
        {
            if (component == null || type?.BackingType == null) return;
            component.valueFormat = DesignerComponentValueFormat.PropertyPath;
            component.adoptExistingComponent = true;
        }

        /// <summary>
        /// Why <paramref name="type"/> cannot be attached to <paramref name="element"/>, or null when
        /// it can. Mirrors <see cref="AttachBlockedReason"/> for types that have no registry entry.
        /// </summary>
        public static string ProjectAttachBlockedReason(DesignerElementMetadata element, System.Type type)
        {
            if (type == null) return "Unknown component type.";
            if (System.Attribute.GetCustomAttribute(type, typeof(DisallowMultipleComponent)) == null) return null;

            var typeId = DesignerProjectComponentIds.FromQualifiedName(StudioComponentTypeIndex.Identity(type));
            return Has(element, typeId)
                ? $"{type.Name} is marked [DisallowMultipleComponent] and is already on this element."
                : null;
        }

        /// <summary>
        /// Adds a project or engine MonoBehaviour to the element's one component stack.
        /// </summary>
        /// <remarks>
        /// This is the whole point of the universal model: a user script lands in the same list as a
        /// uGUI Image, carrying its own <c>instanceId</c>, enable state and property bag, so the
        /// Inspector, the writer and the validator each have one path instead of two.
        /// </remarks>
        public static DesignerElementComponent AttachProject(DesignerElementMetadata element, System.Type type)
        {
            if (element == null || type == null) return null;
            if (ProjectAttachBlockedReason(element, type) != null) return null;

            element.components ??= new List<DesignerElementComponent>();
            var qualifiedName = StudioComponentTypeIndex.Identity(type);
            var component = new DesignerElementComponent
            {
                typeId = DesignerProjectComponentIds.FromQualifiedName(qualifiedName),
                source = StudioComponentTypeIndex.OriginOf(type) switch
                {
                    StudioComponentOrigin.NexUI => DesignerComponentSource.NexUI,
                    StudioComponentOrigin.UGUI => DesignerComponentSource.UGUI,
                    StudioComponentOrigin.Unity => DesignerComponentSource.Unity,
                    _ => DesignerComponentSource.Project
                },
                assemblyQualifiedTypeName = qualifiedName,
                enabled = true,
                // The generic inspector edits through SerializedObject, so its values are keyed by
                // Unity's property paths from the moment the component is added.
                valueFormat = DesignerComponentValueFormat.PropertyPath
            };
            element.components.Add(component);
            return component;
        }

        /// <summary>Removes a component unless it is essential or something still requires it.</summary>
        public static bool Detach(DesignerElementMetadata element, string instanceId, out string blockedReason)
        {
            blockedReason = null;
            var component = Find(element, instanceId);
            if (component == null) return false;

            var type = DesignerUIComponentRegistry.Get(component.typeId);
            if (type != null && type.IsEssential)
            {
                blockedReason = $"{type.DisplayName} cannot be removed.";
                return false;
            }

            foreach (var other in Components(element))
            {
                if (other == component) continue;
                var otherType = DesignerUIComponentRegistry.Get(other.typeId);
                if (otherType == null) continue;
                foreach (var required in otherType.RequiredComponents)
                    if (required == component.typeId)
                    {
                        blockedReason = $"{otherType.DisplayName} requires {DisplayNameOf(component.typeId)}.";
                        return false;
                    }
            }

            element.components.Remove(component);
            return true;
        }

        /// <summary>Moves a component up or down the inspector order (Unity's Move Up / Move Down).</summary>
        public static bool Move(DesignerElementMetadata element, string instanceId, int delta)
        {
            if (element?.components == null || delta == 0) return false;
            var index = element.components.FindIndex(c => c != null && c.instanceId == instanceId);
            if (index < 0) return false;

            var target = Mathf.Clamp(index + delta, 0, element.components.Count - 1);
            if (target == index) return false;

            // The core element component stays first, like the transform in Unity's inspector.
            if (element.components[target]?.typeId == CoreElement) return false;

            var component = element.components[index];
            element.components.RemoveAt(index);
            element.components.Insert(target, component);
            return true;
        }

        // ---- Property access ---------------------------------------------------------------

        public static DesignerComponentProperty Schema(string typeId, string key)
        {
            var type = DesignerUIComponentRegistry.Get(typeId);
            if (type == null) return null;
            foreach (var property in type.Properties)
                if (property.Key == key) return property;
            return null;
        }

        public static bool IsOverridden(DesignerElementComponent component, string key)
            => component != null && (DesignerComponentPropertyBag.Has(component.properties, key) ||
                                     DesignerComponentPropertyBag.Has(component.properties, AlternateKey(key)));

        /// <summary>Stored value if present, else the schema default.</summary>
        public static DesignerPropertyValue Value(DesignerElementComponent component, string key)
        {
            if (component == null) return null;
            var stored = DesignerComponentPropertyBag.Find(component.properties, key)
                         ?? DesignerComponentPropertyBag.Find(component.properties, AlternateKey(key));
            return stored ?? Schema(component.typeId, key)?.Default;
        }

        /// <summary>
        /// The same field's key in the other value format: <c>"segments"</c> ↔ <c>"m_Segments"</c>.
        /// </summary>
        /// <remarks>
        /// A curated schema key is Unity's backing field name with the <c>m_</c> stripped and the first
        /// letter lowered, so the two formats name the same field differently only for private
        /// serialized fields - a public field's key and property path are already identical.
        ///
        /// Readers that ask for a field by its schema key (the preview renderers, for one) predate the
        /// move to property paths and should not each have to know which format a component happens to
        /// use. Trying the counterpart key here keeps every one of them working across both.
        ///
        /// Only plain identifiers are translated. A nested path such as <c>"m_Colors.m_NormalColor"</c>
        /// has no schema counterpart at all, so guessing one would only produce a wrong lookup.
        /// </remarks>
        private static string AlternateKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.IndexOf('.') >= 0 || key.IndexOf('[') >= 0) return null;
            if (!key.StartsWith("m_", System.StringComparison.Ordinal))
                return "m_" + char.ToUpperInvariant(key[0]) + key.Substring(1);
            return key.Length > 2 ? char.ToLowerInvariant(key[2]) + key.Substring(3) : null;
        }

        public static bool Set(DesignerElementComponent component, string key, DesignerPropertyValue value)
        {
            if (component == null) return false;
            component.properties ??= new List<DesignerComponentPropertyEntry>();
            return DesignerComponentPropertyBag.Set(component.properties, key, value);
        }

        public static bool Reset(DesignerElementComponent component, string key) => Set(component, key, null);

        /// <summary>Clears every authored value, matching Unity's per-component Reset.</summary>
        public static void ResetAll(DesignerElementComponent component) => component?.properties?.Clear();

        public static float GetFloat(DesignerElementComponent component, string key, float fallback = 0f)
            => Value(component, key)?.floatValue ?? fallback;

        public static int GetInt(DesignerElementComponent component, string key, int fallback = 0)
            => Value(component, key)?.intValue ?? fallback;

        public static bool GetBool(DesignerElementComponent component, string key, bool fallback = false)
            => Value(component, key)?.boolValue ?? fallback;

        public static string GetString(DesignerElementComponent component, string key, string fallback = "")
            => Value(component, key)?.stringValue ?? fallback;

        public static Color GetColor(DesignerElementComponent component, string key)
            => Value(component, key)?.colorValue ?? Color.white;

        public static Vector2 GetVector2(DesignerElementComponent component, string key)
            => Value(component, key)?.vector2Value ?? Vector2.zero;

        public static Object GetAsset(DesignerElementComponent component, string key)
            => Value(component, key)?.assetValue;

        public static string GetEnum(DesignerElementComponent component, string key)
        {
            var property = Schema(component?.typeId, key);
            if (property?.EnumOptions == null || property.EnumOptions.Length == 0) return null;
            var index = GetInt(component, key, property.Default?.intValue ?? 0);
            return index >= 0 && index < property.EnumOptions.Length ? property.EnumOptions[index] : property.EnumOptions[0];
        }

        private static string DisplayNameOf(string typeId)
            => DesignerUIComponentRegistry.Get(typeId)?.DisplayName ?? typeId;
    }
}

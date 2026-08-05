using System;
using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Designer
{
    public enum DesignerManagedComponentOwnership
    {
        /// <summary>The Studio created this component and may remove it with its stack entry.</summary>
        Owned,

        /// <summary>The component existed before import; the Studio may edit but never delete it.</summary>
        Adopted
    }

    /// <summary>
    /// One component the Studio created, paired with the stack entry that owns it.
    /// </summary>
    /// <remarks>
    /// The pairing is what makes two components of the same type on one element safe: without an
    /// identity, removing the first of two <c>Outline</c>s would be indistinguishable from removing
    /// the second, and a save could destroy the wrong one.
    /// </remarks>
    [Serializable]
    public sealed class DesignerManagedComponent
    {
        /// <summary><see cref="DesignerElementComponent.instanceId"/> of the stack entry.</summary>
        public string instanceId;

        public Component component;

        public DesignerManagedComponentOwnership ownership;
    }

    /// <summary>
    /// Tracks components created by the Studio. Separate lists keep explicit Add Component
    /// attachments distinct from optional serializer helpers such as Outline or LayoutGroup, so
    /// a later save never removes a same-type component authored manually by the user.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class DesignerAttachedComponentTracker : MonoBehaviour
    {
        [SerializeField, HideInInspector]
        public List<Component> managedComponents = new List<Component>();

        [SerializeField, HideInInspector]
        public List<Component> managedGeneratedComponents = new List<Component>();

        /// <summary>
        /// Studio-owned components keyed by the stack entry that created them.
        /// </summary>
        /// <remarks>
        /// Prefabs written before this list existed only have <see cref="managedComponents"/>. Those
        /// entries are adopted by type on the next save rather than being orphaned, which is why the
        /// older list is still read.
        /// </remarks>
        [SerializeField, HideInInspector]
        public List<DesignerManagedComponent> managedByInstance = new List<DesignerManagedComponent>();

        public Component Find(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId) || managedByInstance == null) return null;
            foreach (var entry in managedByInstance)
                if (entry != null && entry.instanceId == instanceId && entry.component != null)
                    return entry.component;
            return null;
        }

        public void Track(string instanceId, Component component,
            DesignerManagedComponentOwnership ownership = DesignerManagedComponentOwnership.Owned)
        {
            if (component == null || string.IsNullOrEmpty(instanceId)) return;
            managedByInstance ??= new List<DesignerManagedComponent>();
            foreach (var entry in managedByInstance)
                if (entry != null && entry.instanceId == instanceId)
                {
                    entry.component = component;
                    entry.ownership = ownership;
                    return;
                }
            managedByInstance.Add(new DesignerManagedComponent
            {
                instanceId = instanceId,
                component = component,
                ownership = ownership
            });

            // Mirrored into the legacy list so a downgrade still recognises the component as ours.
            managedComponents ??= new List<Component>();
            if (!managedComponents.Contains(component)) managedComponents.Add(component);
        }

        public void Forget(Component component)
        {
            managedByInstance?.RemoveAll(entry => entry == null || entry.component == component);
            managedComponents?.RemoveAll(existing => existing == component);
        }

        /// <summary>Whether the Studio put <paramref name="component"/> on this object.</summary>
        /// <remarks>
        /// All three lists count. A serializer helper such as an <c>Outline</c> is just as much the
        /// Studio's to rewrite as an explicit Add Component attachment, and a prefab written by an
        /// older build only has the legacy list.
        /// </remarks>
        public bool Owns(Component component)
        {
            if (component == null) return false;
            if (managedComponents != null && managedComponents.Contains(component)) return true;
            if (managedGeneratedComponents != null && managedGeneratedComponents.Contains(component)) return true;
            if (managedByInstance != null)
                foreach (var entry in managedByInstance)
                    if (entry != null && entry.component == component) return true;
            return false;
        }

        public DesignerManagedComponentOwnership OwnershipOf(Component component)
        {
            if (component == null || managedByInstance == null)
                return DesignerManagedComponentOwnership.Owned;
            foreach (var entry in managedByInstance)
                if (entry != null && entry.component == component)
                    return entry.ownership;
            return DesignerManagedComponentOwnership.Owned;
        }

        /// <summary>
        /// Drops entries whose component was deleted or moved to another object, and makes sure the
        /// lists exist - a tracker added from code rather than deserialized starts with nulls.
        /// </summary>
        public void Prune()
        {
            managedByInstance ??= new List<DesignerManagedComponent>();
            managedComponents ??= new List<Component>();
            managedGeneratedComponents ??= new List<Component>();

            managedByInstance?.RemoveAll(entry =>
                entry == null || entry.component == null || entry.component.gameObject != gameObject);
            managedComponents?.RemoveAll(component => component == null || component.gameObject != gameObject);
            managedGeneratedComponents?.RemoveAll(component => component == null || component.gameObject != gameObject);
        }

        public bool IsEmpty =>
            (managedByInstance == null || managedByInstance.Count == 0) &&
            (managedComponents == null || managedComponents.Count == 0) &&
            (managedGeneratedComponents == null || managedGeneratedComponents.Count == 0);
    }
}

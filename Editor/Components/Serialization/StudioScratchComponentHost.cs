using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Components.Serialization
{
    /// <summary>
    /// The hidden GameObject the generic inspector edits on.
    /// </summary>
    /// <remarks>
    /// Studio metadata is not a Unity object, so there is nothing for <c>SerializedObject</c> to wrap.
    /// Creating the real component on a throwaway GameObject gives the inspector Unity's own drawers,
    /// <c>[Range]</c>/<c>[Header]</c>/<c>[Tooltip]</c> handling, <c>[FormerlySerializedAs]</c> and any
    /// custom <c>PropertyDrawer</c> the user wrote - none of which could be reproduced by hand.
    ///
    /// Two instances are handed out per type: the one the user edits, and an untouched one that
    /// supplies Unity's real defaults. Storing the diff against those defaults is what keeps a screen
    /// from baking the Studio's idea of a default into every prefab.
    ///
    /// <see cref="HideFlags.HideAndDontSave"/> keeps the object out of the hierarchy, out of saves and
    /// out of the undo stack, and everything is destroyed before a domain reload so a recompile never
    /// leaves an orphan behind.
    /// </remarks>
    [InitializeOnLoad]
    public static class StudioScratchComponentHost
    {
        private const string HostName = "__NexUIStudioScratch";

        private static GameObject _host;
        private static readonly Dictionary<Type, Component> Editable = new Dictionary<Type, Component>();
        private static readonly Dictionary<Type, Component> Pristine = new Dictionary<Type, Component>();

        static StudioScratchComponentHost()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting += Dispose;
        }

        /// <summary>A component of <paramref name="type"/> the caller may freely write to.</summary>
        public static Component RentEditable(Type type) => Rent(type, Editable, "Edit");

        /// <summary>
        /// A never-written instance of <paramref name="type"/>, used as the baseline for "what did the
        /// user actually change".
        /// </summary>
        public static Component RentPristine(Type type) => Rent(type, Pristine, "Default");

        /// <summary>Destroys every scratch object. Safe to call at any time; the pools rebuild lazily.</summary>
        public static void Dispose()
        {
            Editable.Clear();
            Pristine.Clear();
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
            _host = null;

            // A previous domain that crashed before its own cleanup can leave a host behind.
            foreach (var stray in Resources.FindObjectsOfTypeAll<GameObject>())
                if (stray != null && stray.name.StartsWith(HostName, StringComparison.Ordinal) &&
                    stray.hideFlags == HideFlags.HideAndDontSave)
                    UnityEngine.Object.DestroyImmediate(stray);
        }

        private static Component Rent(Type type, Dictionary<Type, Component> pool, string suffix)
        {
            if (type == null || !typeof(Component).IsAssignableFrom(type)) return null;
            if (type.IsAbstract || type.ContainsGenericParameters) return null;

            if (pool.TryGetValue(type, out var existing) && existing != null) return existing;

            EnsureHost();

            // One child per component so [DisallowMultipleComponent] and [RequireComponent] behave
            // exactly as they would on a real element, instead of piling up on a single object.
            var carrier = new GameObject($"{HostName}.{type.Name}.{suffix}")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            carrier.transform.SetParent(_host.transform, false);
            carrier.SetActive(false);

            Component component;
            try
            {
                component = carrier.AddComponent(type);
            }
            catch (Exception exception)
            {
                UnityEngine.Object.DestroyImmediate(carrier);
                Debug.LogWarning($"[NexUI Studio] Could not create a scratch {type.Name}: {exception.Message}");
                return null;
            }

            if (component == null)
            {
                UnityEngine.Object.DestroyImmediate(carrier);
                return null;
            }

            pool[type] = component;
            return component;
        }

        private static void EnsureHost()
        {
            if (_host != null) return;
            _host = new GameObject(HostName) { hideFlags = HideFlags.HideAndDontSave };

            // A UI component measured on a Canvas-less object logs layout warnings on every rebuild;
            // giving the host a Canvas keeps the scratch pass silent.
            _host.AddComponent<Canvas>();
            _host.SetActive(false);
        }
    }
}

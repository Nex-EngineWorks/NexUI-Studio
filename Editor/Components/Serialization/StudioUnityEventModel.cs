using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace emiteat.NexUI.Designer.Editor.Components.Serialization
{
    /// <summary>
    /// Reads and writes a UnityEvent's persistent call list through the component's property bag.
    /// </summary>
    /// <remarks>
    /// The bag is the source of truth rather than the scratch object, because a call's target is very
    /// often another element on the screen - something the scratch object has no way to hold. Working
    /// at the property-path level means the same entries the prefab writer applies are the ones the
    /// inspector edits, so what the user sets is exactly what gets saved.
    ///
    /// The paths mirror Unity's own serialization of <see cref="UnityEventBase"/>. They are stable
    /// across Unity versions because they are the format prefabs on disk already use.
    /// </remarks>
    public static class StudioUnityEventModel
    {
        /// <summary>How Unity dispatches one persistent call's argument.</summary>
        public enum ListenerMode
        {
            EventDefined = 0,
            Void = 1,
            Object = 2,
            Int = 3,
            Float = 4,
            String = 5,
            Bool = 6
        }

        /// <summary>One persistent call, in the shape the inspector edits.</summary>
        public sealed class Call
        {
            public DesignerObjectReference Target = new DesignerObjectReference();
            public string MethodName = string.Empty;
            public ListenerMode Mode = ListenerMode.Void;
            public UnityEventCallState CallState = UnityEventCallState.RuntimeOnly;

            public int IntArgument;
            public float FloatArgument;
            public string StringArgument = string.Empty;
            public bool BoolArgument;
            public DesignerObjectReference ObjectArgument = new DesignerObjectReference();
        }

        public static bool IsUnityEvent(Type fieldType)
            => fieldType != null && typeof(UnityEventBase).IsAssignableFrom(fieldType);

        // ---- Paths -------------------------------------------------------------------------------

        private static string CallsSize(string key) => key + ".m_PersistentCalls.m_Calls.Array.size";
        private static string Call_(string key, int index) => key + $".m_PersistentCalls.m_Calls.Array.data[{index}]";
        private static string Target(string key, int i) => Call_(key, i) + ".m_Target";
        private static string TargetType(string key, int i) => Call_(key, i) + ".m_TargetAssemblyTypeName";
        private static string Method(string key, int i) => Call_(key, i) + ".m_MethodName";
        private static string Mode(string key, int i) => Call_(key, i) + ".m_Mode";
        private static string CallState(string key, int i) => Call_(key, i) + ".m_CallState";
        private static string IntArg(string key, int i) => Call_(key, i) + ".m_Arguments.m_IntArgument";
        private static string FloatArg(string key, int i) => Call_(key, i) + ".m_Arguments.m_FloatArgument";
        private static string StringArg(string key, int i) => Call_(key, i) + ".m_Arguments.m_StringArgument";
        private static string BoolArg(string key, int i) => Call_(key, i) + ".m_Arguments.m_BoolArgument";
        private static string ObjectArg(string key, int i) => Call_(key, i) + ".m_Arguments.m_ObjectArgument";

        // ---- Read ---------------------------------------------------------------------------------

        public static List<Call> Read(DesignerElementComponent component, string key)
        {
            var calls = new List<Call>();
            if (component?.properties == null || string.IsNullOrEmpty(key)) return calls;

            var count = Int(component, CallsSize(key), 0);
            for (var i = 0; i < count; i++)
            {
                calls.Add(new Call
                {
                    Target = Reference(component, Target(key, i)),
                    MethodName = String(component, Method(key, i)),
                    Mode = (ListenerMode)Int(component, Mode(key, i), (int)ListenerMode.Void),
                    CallState = (UnityEventCallState)Int(component, CallState(key, i), (int)UnityEventCallState.RuntimeOnly),
                    IntArgument = Int(component, IntArg(key, i), 0),
                    FloatArgument = Float(component, FloatArg(key, i)),
                    StringArgument = String(component, StringArg(key, i)),
                    BoolArgument = Bool(component, BoolArg(key, i)),
                    ObjectArgument = Reference(component, ObjectArg(key, i))
                });
            }
            return calls;
        }

        // ---- Write --------------------------------------------------------------------------------

        /// <summary>
        /// Replaces the whole call list. Writing every index rather than patching one keeps the array
        /// size and its entries from ever disagreeing - the failure that produces a call pointing at
        /// nothing.
        /// </summary>
        public static void Write(DesignerElementComponent component, string key, List<Call> calls)
        {
            if (component == null || string.IsNullOrEmpty(key)) return;
            component.properties ??= new List<DesignerComponentPropertyEntry>();

            ClearCalls(component, key);
            calls ??= new List<Call>();
            SetInt(component, CallsSize(key), calls.Count);

            for (var i = 0; i < calls.Count; i++)
            {
                var call = calls[i];
                SetReference(component, Target(key, i), call.Target);
                SetString(component, TargetType(key, i), TargetTypeNameOf(call));
                SetString(component, Method(key, i), call.MethodName ?? string.Empty);
                SetInt(component, Mode(key, i), (int)call.Mode);
                SetInt(component, CallState(key, i), (int)call.CallState);

                // Every argument slot is written, matching Unity: the unused ones are simply zero, and
                // leaving them absent would make a mode change read a stale value from a previous call.
                SetInt(component, IntArg(key, i), call.IntArgument);
                SetFloat(component, FloatArg(key, i), call.FloatArgument);
                SetString(component, StringArg(key, i), call.StringArgument ?? string.Empty);
                SetBool(component, BoolArg(key, i), call.BoolArgument);
                SetReference(component, ObjectArg(key, i), call.ObjectArgument);
            }
        }

        /// <summary>
        /// The assembly-qualified type Unity records for the call target. It is what the runtime uses
        /// to find the method, so it has to match the component the reference names, not the element.
        /// </summary>
        private static string TargetTypeNameOf(Call call)
        {
            if (call?.Target == null || !call.Target.IsAssigned) return string.Empty;
            if (!string.IsNullOrEmpty(call.Target.componentTypeName)) return call.Target.componentTypeName;
            return StudioComponentTypeIndex.Identity(typeof(GameObject));
        }

        private static void ClearCalls(DesignerElementComponent component, string key)
        {
            var prefix = key + ".m_PersistentCalls.";
            component.properties.RemoveAll(entry =>
                entry != null && entry.key != null && entry.key.StartsWith(prefix, StringComparison.Ordinal));
        }

        // ---- Method discovery ------------------------------------------------------------------------

        /// <summary>
        /// Methods on <paramref name="targetType"/> a persistent call can invoke, in Unity's own terms:
        /// public, returning void, and taking either nothing or one argument UnityEvent can serialize.
        /// Property setters are included because that is how <c>GameObject.SetActive</c>-style entries
        /// appear in Unity's own dropdown.
        /// </summary>
        public static List<MethodInfo> InvokableMethods(Type targetType)
        {
            var methods = new List<MethodInfo>();
            if (targetType == null) return methods;

            foreach (var method in targetType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.ReturnType != typeof(void)) continue;
                if (method.IsGenericMethod || method.ContainsGenericParameters) continue;
                if (method.GetCustomAttribute<ObsoleteAttribute>() != null) continue;
                if (method.IsSpecialName && !method.Name.StartsWith("set_", StringComparison.Ordinal)) continue;

                var parameters = method.GetParameters();
                if (parameters.Length > 1) continue;
                if (parameters.Length == 1 && ModeFor(parameters[0].ParameterType) == null) continue;

                methods.Add(method);
            }
            methods.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return methods;
        }

        /// <summary>The listener mode a one-argument method needs, or null when the type is unsupported.</summary>
        public static ListenerMode? ModeFor(Type parameterType)
        {
            if (parameterType == typeof(int)) return ListenerMode.Int;
            if (parameterType == typeof(float)) return ListenerMode.Float;
            if (parameterType == typeof(string)) return ListenerMode.String;
            if (parameterType == typeof(bool)) return ListenerMode.Bool;
            if (typeof(UnityEngine.Object).IsAssignableFrom(parameterType)) return ListenerMode.Object;
            return null;
        }

        public static ListenerMode ModeOf(MethodInfo method)
        {
            var parameters = method.GetParameters();
            if (parameters.Length == 0) return ListenerMode.Void;
            return ModeFor(parameters[0].ParameterType) ?? ListenerMode.Void;
        }

        /// <summary>"SetActive (bool)" - the label Unity shows in its own dropdown.</summary>
        public static string Label(MethodInfo method)
        {
            var parameters = method.GetParameters();
            var name = method.Name.StartsWith("set_", StringComparison.Ordinal)
                ? method.Name.Substring(4)
                : method.Name;
            return parameters.Length == 0 ? name : $"{name} ({parameters[0].ParameterType.Name})";
        }

        /// <summary>
        /// Whether <paramref name="call"/> still names a method that exists on its target. Used by
        /// validation: a renamed method leaves a call that silently does nothing at runtime.
        /// </summary>
        public static bool MethodExists(Call call, Type targetType)
        {
            if (call == null || string.IsNullOrEmpty(call.MethodName)) return false;
            if (targetType == null) return false;
            foreach (var method in InvokableMethods(targetType))
                if (method.Name == call.MethodName) return true;
            return false;
        }

        // ---- Bag helpers -----------------------------------------------------------------------------

        private static DesignerPropertyValue Value(DesignerElementComponent component, string key)
            => DesignerComponentPropertyBag.Find(component?.properties, key);

        private static int Int(DesignerElementComponent component, string key, int fallback)
            => Value(component, key)?.intValue ?? fallback;

        private static float Float(DesignerElementComponent component, string key)
            => Value(component, key)?.floatValue ?? 0f;

        private static bool Bool(DesignerElementComponent component, string key)
            => Value(component, key)?.boolValue ?? false;

        private static string String(DesignerElementComponent component, string key)
            => Value(component, key)?.stringValue ?? string.Empty;

        private static DesignerObjectReference Reference(DesignerElementComponent component, string key)
            => Value(component, key)?.reference?.Clone() ?? new DesignerObjectReference();

        private static void SetInt(DesignerElementComponent component, string key, int value)
            => DesignerComponentPropertyBag.Set(component.properties, key,
                new DesignerPropertyValue { type = DesignerPropertyValueType.Integer, intValue = value });

        private static void SetFloat(DesignerElementComponent component, string key, float value)
            => DesignerComponentPropertyBag.Set(component.properties, key,
                new DesignerPropertyValue { type = DesignerPropertyValueType.Float, floatValue = value });

        private static void SetBool(DesignerElementComponent component, string key, bool value)
            => DesignerComponentPropertyBag.Set(component.properties, key,
                new DesignerPropertyValue { type = DesignerPropertyValueType.Boolean, boolValue = value });

        private static void SetString(DesignerElementComponent component, string key, string value)
            => DesignerComponentPropertyBag.Set(component.properties, key,
                new DesignerPropertyValue { type = DesignerPropertyValueType.String, stringValue = value });

        private static void SetReference(DesignerElementComponent component, string key,
            DesignerObjectReference reference)
        {
            if (reference == null || !reference.IsAssigned)
            {
                DesignerComponentPropertyBag.Set(component.properties, key, null);
                return;
            }
            DesignerComponentPropertyBag.Set(component.properties, key, new DesignerPropertyValue
            {
                type = reference.kind == DesignerReferenceKind.Element
                    ? DesignerPropertyValueType.ElementReference
                    : DesignerPropertyValueType.AssetReference,
                assetValue = reference.kind == DesignerReferenceKind.Asset
                    ? StudioReferenceUtility.ResolveAsset(reference)
                    : null,
                reference = reference.Clone()
            });
        }
    }
}

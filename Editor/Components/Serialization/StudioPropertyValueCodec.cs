using System;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Components.Serialization
{
    /// <summary>
    /// Translates one <see cref="SerializedProperty"/> to and from the
    /// <see cref="DesignerPropertyValue"/> stored in metadata.
    /// </summary>
    /// <remarks>
    /// The seven typed fields on <see cref="DesignerPropertyValue"/> predate this codec and are used
    /// where they fit exactly, so screens authored before the universal system keep loading unchanged.
    /// Everything else - Vector3, Rect, Bounds, Quaternion, AnimationCurve, Gradient - goes through
    /// <see cref="DesignerPropertyValue.json"/>.
    ///
    /// A shape this build cannot express is <b>never dropped</b>: <see cref="TryEncode"/> returns false
    /// and the caller reports it, leaving whatever value was already stored untouched. That is what
    /// stops a round trip through an older Studio from silently deleting a user's data.
    ///
    /// Object references are deliberately not handled here. A field pointing at another element cannot
    /// be materialized on the scratch object the inspector edits, so references live in
    /// <see cref="DesignerPropertyValue.reference"/> and are written by the reference row directly.
    /// </remarks>
    public static class StudioPropertyValueCodec
    {
        // ---- Serializable carriers for the shapes JsonUtility can round-trip -------------------

        [Serializable] private struct V3 { public Vector3 v; }
        [Serializable] private struct V4 { public Vector4 v; }
        [Serializable] private struct V2I { public Vector2Int v; }
        [Serializable] private struct V3I { public Vector3Int v; }
        [Serializable] private struct RectBox { public Rect v; }
        [Serializable] private struct RectIntBox { public RectInt v; }
        [Serializable] private struct BoundsBox { public Bounds v; }
        [Serializable] private struct BoundsIntBox { public BoundsInt v; }
        [Serializable] private struct QuatBox { public Quaternion v; }
        [Serializable] private struct DoubleBox { public double v; }
        [Serializable] private sealed class CurveBox { public AnimationCurve v = new AnimationCurve(); }
        [Serializable] private sealed class GradientBox { public Gradient v = new Gradient(); }

        /// <summary>True when this property is one the reference row owns rather than the codec.</summary>
        public static bool IsReference(SerializedProperty property)
            => property != null && (property.propertyType == SerializedPropertyType.ObjectReference ||
                                    property.propertyType == SerializedPropertyType.ExposedReference);

        public static UnityEngine.Object GetReference(SerializedProperty property)
            => property.propertyType == SerializedPropertyType.ExposedReference
                ? property.exposedReferenceValue
                : property.objectReferenceValue;

        public static void SetReference(SerializedProperty property, UnityEngine.Object value)
        {
            if (property.propertyType == SerializedPropertyType.ExposedReference)
                property.exposedReferenceValue = value;
            else
                property.objectReferenceValue = value;
        }

        /// <summary>
        /// Reads <paramref name="property"/> into a metadata value. Returns false - without touching
        /// <paramref name="value"/> - for a shape this build cannot represent.
        /// </summary>
        public static bool TryEncode(SerializedProperty property, out DesignerPropertyValue value)
        {
            value = null;
            if (property == null) return false;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    value = new DesignerPropertyValue
                        { type = DesignerPropertyValueType.Boolean, boolValue = property.boolValue };
                    return true;

                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Character:
                    value = new DesignerPropertyValue
                        { type = DesignerPropertyValueType.Integer, intValue = property.intValue };
                    return true;

                case SerializedPropertyType.Enum:
                    // The raw backing value, not enumValueIndex: a [Flags] enum and an enum with
                    // explicit numbering both survive, where an index would silently remap them.
                    value = new DesignerPropertyValue
                        { type = DesignerPropertyValueType.Enum, intValue = property.intValue };
                    return true;

                case SerializedPropertyType.Float:
                    // A double field is also reported as Float; storing it in the float slot would
                    // quietly round the user's value, so it takes the json path instead.
                    if (property.type == "double")
                    {
                        value = Json(new DoubleBox { v = property.doubleValue });
                        return true;
                    }
                    value = new DesignerPropertyValue
                        { type = DesignerPropertyValueType.Float, floatValue = property.floatValue };
                    return true;

                case SerializedPropertyType.String:
                    value = new DesignerPropertyValue
                        { type = DesignerPropertyValueType.String, stringValue = property.stringValue ?? string.Empty };
                    return true;

                case SerializedPropertyType.Color:
                    value = new DesignerPropertyValue
                        { type = DesignerPropertyValueType.Color, colorValue = property.colorValue };
                    return true;

                case SerializedPropertyType.Vector2:
                    value = new DesignerPropertyValue
                        { type = DesignerPropertyValueType.Vector2, vector2Value = property.vector2Value };
                    return true;

                case SerializedPropertyType.Vector3: value = Json(new V3 { v = property.vector3Value }); return true;
                case SerializedPropertyType.Vector4: value = Json(new V4 { v = property.vector4Value }); return true;
                case SerializedPropertyType.Vector2Int: value = Json(new V2I { v = property.vector2IntValue }); return true;
                case SerializedPropertyType.Vector3Int: value = Json(new V3I { v = property.vector3IntValue }); return true;
                case SerializedPropertyType.Rect: value = Json(new RectBox { v = property.rectValue }); return true;
                case SerializedPropertyType.RectInt: value = Json(new RectIntBox { v = property.rectIntValue }); return true;
                case SerializedPropertyType.Bounds: value = Json(new BoundsBox { v = property.boundsValue }); return true;
                case SerializedPropertyType.BoundsInt: value = Json(new BoundsIntBox { v = property.boundsIntValue }); return true;
                case SerializedPropertyType.Quaternion: value = Json(new QuatBox { v = property.quaternionValue }); return true;
                case SerializedPropertyType.AnimationCurve:
                    value = Json(new CurveBox { v = property.animationCurveValue }); return true;
                case SerializedPropertyType.Gradient:
                    var gradient = GradientOf(property);
                    if (gradient == null) return false;
                    value = Json(new GradientBox { v = gradient });
                    return true;

                case SerializedPropertyType.Hash128:
                    value = new DesignerPropertyValue
                        { type = DesignerPropertyValueType.Serialized, stringValue = property.hash128Value.ToString() };
                    return true;

                case SerializedPropertyType.ManagedReference:
                    var managed = property.managedReferenceValue;
                    value = new DesignerPropertyValue
                    {
                        type = DesignerPropertyValueType.Serialized,
                        stringValue = managed?.GetType().AssemblyQualifiedName ?? string.Empty,
                        json = managed == null ? string.Empty : JsonUtility.ToJson(managed)
                    };
                    return true;

                default:
                    // Anything Unity adds later remains preserved by the caller.
                    return false;
            }
        }

        /// <summary>
        /// Writes a stored value back onto <paramref name="property"/>. Returns false when the value
        /// cannot be applied to this property's current shape - a field whose type changed since the
        /// screen was authored - so the caller reports it instead of writing something wrong.
        /// </summary>
        public static bool TryDecode(DesignerPropertyValue value, SerializedProperty property)
        {
            if (value == null || property == null) return false;

            try
            {
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Boolean:
                        property.boolValue = value.boolValue; return true;

                    case SerializedPropertyType.ArraySize:
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.LayerMask:
                    case SerializedPropertyType.Character:
                    case SerializedPropertyType.Enum:
                        property.intValue = value.intValue; return true;

                    case SerializedPropertyType.Float:
                        if (property.type == "double")
                        {
                            if (string.IsNullOrEmpty(value.json)) return false;
                            property.doubleValue = JsonUtility.FromJson<DoubleBox>(value.json).v;
                            return true;
                        }
                        property.floatValue = value.floatValue; return true;

                    case SerializedPropertyType.String:
                        property.stringValue = value.stringValue ?? string.Empty; return true;

                    case SerializedPropertyType.Color:
                        property.colorValue = value.colorValue; return true;

                    case SerializedPropertyType.Vector2:
                        property.vector2Value = value.vector2Value; return true;

                    case SerializedPropertyType.Vector3:
                        property.vector3Value = FromJson<V3>(value).v; return true;
                    case SerializedPropertyType.Vector4:
                        property.vector4Value = FromJson<V4>(value).v; return true;
                    case SerializedPropertyType.Vector2Int:
                        property.vector2IntValue = FromJson<V2I>(value).v; return true;
                    case SerializedPropertyType.Vector3Int:
                        property.vector3IntValue = FromJson<V3I>(value).v; return true;
                    case SerializedPropertyType.Rect:
                        property.rectValue = FromJson<RectBox>(value).v; return true;
                    case SerializedPropertyType.RectInt:
                        property.rectIntValue = FromJson<RectIntBox>(value).v; return true;
                    case SerializedPropertyType.Bounds:
                        property.boundsValue = FromJson<BoundsBox>(value).v; return true;
                    case SerializedPropertyType.BoundsInt:
                        property.boundsIntValue = FromJson<BoundsIntBox>(value).v; return true;
                    case SerializedPropertyType.Quaternion:
                        property.quaternionValue = FromJson<QuatBox>(value).v; return true;
                    case SerializedPropertyType.AnimationCurve:
                        if (string.IsNullOrEmpty(value.json)) return false;
                        property.animationCurveValue = JsonUtility.FromJson<CurveBox>(value.json).v;
                        return true;
                    case SerializedPropertyType.Gradient:
                        if (string.IsNullOrEmpty(value.json)) return false;
                        return TrySetGradient(property, JsonUtility.FromJson<GradientBox>(value.json).v);

                    case SerializedPropertyType.Hash128:
                        if (string.IsNullOrEmpty(value.stringValue)) return false;
                        property.hash128Value = Hash128.Parse(value.stringValue);
                        return true;

                    case SerializedPropertyType.ManagedReference:
                        if (string.IsNullOrEmpty(value.stringValue))
                        {
                            property.managedReferenceValue = null;
                            return true;
                        }
                        var managedType = Type.GetType(value.stringValue, false);
                        if (managedType == null || string.IsNullOrEmpty(value.json)) return false;
                        property.managedReferenceValue = JsonUtility.FromJson(value.json, managedType);
                        return true;

                    default:
                        return false;
                }
            }
            catch (Exception)
            {
                // A malformed json blob is data loss only if we also clear it; returning false leaves
                // the stored value in place for a build that does understand it.
                return false;
            }
        }

        private static DesignerPropertyValue Json(object box) => new DesignerPropertyValue
        {
            type = DesignerPropertyValueType.Serialized,
            json = JsonUtility.ToJson(box)
        };

        private static T FromJson<T>(DesignerPropertyValue value)
        {
            if (string.IsNullOrEmpty(value.json)) throw new FormatException("empty json payload");
            return JsonUtility.FromJson<T>(value.json);
        }

        // Gradient has no public SerializedProperty accessor before Unity 2022.1 and is still exposed
        // through an internal property on some versions; reflection keeps one code path across both.
        private static readonly System.Reflection.PropertyInfo GradientAccessor =
            typeof(SerializedProperty).GetProperty("gradientValue",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);

        private static Gradient GradientOf(SerializedProperty property)
            => GradientAccessor?.GetValue(property) as Gradient;

        private static bool TrySetGradient(SerializedProperty property, Gradient gradient)
        {
            if (GradientAccessor == null || !GradientAccessor.CanWrite) return false;
            GradientAccessor.SetValue(property, gradient);
            return true;
        }
    }
}

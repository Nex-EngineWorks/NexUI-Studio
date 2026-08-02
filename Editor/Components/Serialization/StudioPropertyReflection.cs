using System;
using System.Collections;
using System.Reflection;
using UnityEditor;

namespace emiteat.NexUI.Designer.Editor.Components.Serialization
{
    /// <summary>
    /// Finds the declared C# type behind a <see cref="SerializedProperty"/>.
    /// </summary>
    /// <remarks>
    /// <c>objectReferenceValue.GetType()</c> only answers this when the field is already assigned, and
    /// an empty field is exactly the case the reference row has to draw. Walking the property path
    /// against the target's real type answers it either way.
    /// </remarks>
    public static class StudioPropertyReflection
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static Type FieldTypeOf(SerializedProperty property)
        {
            if (property?.serializedObject?.targetObject == null) return null;

            var current = property.serializedObject.targetObject.GetType();
            var path = property.propertyPath.Replace(".Array.data[", "[");

            foreach (var segment in path.Split('.'))
            {
                if (current == null) return null;

                var name = segment;
                var indexed = false;
                var bracket = segment.IndexOf('[');
                if (bracket >= 0)
                {
                    name = segment.Substring(0, bracket);
                    indexed = true;
                }

                var field = FindField(current, name);
                if (field == null) return null;

                current = field.FieldType;
                if (!indexed) continue;

                current = current.IsArray
                    ? current.GetElementType()
                    : (current.IsGenericType ? current.GetGenericArguments()[0] : null);
            }
            return current;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                var field = current.GetField(name, Fields | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            return null;
        }
    }
}

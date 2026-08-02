using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Components
{
    /// <summary>
    /// Where a component type came from, for grouping in Add Component.
    /// </summary>
    public enum StudioComponentOrigin
    {
        /// <summary>NexUI's own runtime components.</summary>
        NexUI,

        /// <summary>Unity's uGUI controls.</summary>
        UGUI,

        /// <summary>Unity engine components that are not UI-specific.</summary>
        Unity,

        /// <summary>A MonoBehaviour from the user's own project.</summary>
        Project
    }

    /// <summary>One attachable component type, with everything Add Component needs to show it.</summary>
    public sealed class StudioComponentTypeEntry
    {
        public Type Type;
        public StudioComponentOrigin Origin;

        /// <summary>Assembly-qualified name, the form stored in metadata.</summary>
        public string QualifiedName;

        /// <summary>Menu-style path: <c>[AddComponentMenu]</c> when present, else namespace.</summary>
        public string MenuPath;

        public string DisplayName;
        public string Category;
        public string AssemblyName;
        public bool DisallowMultiple;

        /// <summary>Nicified names of every <c>[RequireComponent]</c> target, comma separated.</summary>
        public string Requirements;

        /// <summary>Lower-cased haystack the search matches against; built once.</summary>
        internal string SearchKey;
    }

    /// <summary>
    /// The searchable index of every MonoBehaviour that can be attached to an element.
    /// </summary>
    /// <remarks>
    /// Built once per domain from <see cref="TypeCache"/> rather than on every Add Component click:
    /// walking every loaded assembly and reading attributes per type is measured in tens of
    /// milliseconds on a real project, which is a visible stall on a menu that is opened constantly.
    ///
    /// The cache is invalidated on assembly reload and project change. Script compilation and package
    /// installs both end in a domain reload, which resets the static field anyway; the explicit hooks
    /// cover the cases where Unity swaps types without a full reload.
    /// </remarks>
    [InitializeOnLoad]
    public static class StudioComponentTypeIndex
    {
        private static List<StudioComponentTypeEntry> _entries;

        static StudioComponentTypeIndex()
        {
            AssemblyReloadEvents.afterAssemblyReload += Invalidate;
            EditorApplication.projectChanged += Invalidate;
        }

        public static void Invalidate() => _entries = null;

        public static IReadOnlyList<StudioComponentTypeEntry> All
        {
            get
            {
                if (_entries != null) return _entries;
                _entries = Build();
                return _entries;
            }
        }

        /// <summary>Entries whose name, namespace, assembly or menu path contain every search term.</summary>
        public static IEnumerable<StudioComponentTypeEntry> Search(string query)
        {
            var all = All;
            if (string.IsNullOrWhiteSpace(query)) return all;

            var terms = query.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var matches = new List<StudioComponentTypeEntry>();
            foreach (var entry in all)
            {
                var hit = true;
                foreach (var term in terms)
                    if (entry.SearchKey.IndexOf(term, StringComparison.Ordinal) < 0) { hit = false; break; }
                if (hit) matches.Add(entry);
            }
            return matches;
        }

        public static StudioComponentTypeEntry Find(Type type)
        {
            if (type == null) return null;
            foreach (var entry in All)
                if (entry.Type == type) return entry;
            return null;
        }

        // ---- Type resolution -------------------------------------------------------------------

        /// <summary>The form written into <see cref="DesignerElementComponent.assemblyQualifiedTypeName"/>.</summary>
        public static string Identity(Type type)
            => type == null ? string.Empty : type.FullName + ", " + type.Assembly.GetName().Name;

        /// <summary>
        /// Resolves a stored type name back to a <see cref="Type"/>, tolerating the several shapes
        /// metadata has carried over time: full assembly-qualified names, "FullName, Assembly", and
        /// bare full names from hand-authored data.
        /// </summary>
        public static Type Resolve(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;

            var type = Type.GetType(typeName, false);
            if (type != null) return type;

            var comma = typeName.IndexOf(',');
            var fullName = comma >= 0 ? typeName.Substring(0, comma).Trim() : typeName.Trim();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        // ---- Presentation ----------------------------------------------------------------------

        public static string MenuPathOf(Type type)
        {
            if (type == null) return string.Empty;
            var menu = type.GetCustomAttribute<AddComponentMenu>();
            return menu != null && !string.IsNullOrEmpty(menu.componentMenu)
                ? menu.componentMenu
                : (string.IsNullOrEmpty(type.Namespace) ? type.Name : type.Namespace + "/" + type.Name);
        }

        public static string ShortName(Type type)
        {
            if (type == null) return string.Empty;
            var path = MenuPathOf(type);
            var slash = path.LastIndexOf('/');
            var name = slash >= 0 ? path.Substring(slash + 1) : type.Name;
            return ObjectNames.NicifyVariableName(name);
        }

        public static string CategoryOf(Type type)
        {
            if (type == null) return string.Empty;
            var path = MenuPathOf(type);
            var slash = path.LastIndexOf('/');
            if (slash > 0) return path.Substring(0, slash);
            if (!string.IsNullOrWhiteSpace(type.Namespace)) return type.Namespace;
            return type.Assembly.GetName().Name;
        }

        /// <summary>
        /// Which library shipped a type, judged by its assembly rather than its namespace.
        /// </summary>
        /// <remarks>
        /// Namespace is the wrong signal: a user script written inside a <c>emiteat.NexUI.*</c>
        /// namespace - a sample, a test fixture, an extension in the user's own project - would be
        /// mistaken for part of the framework and grouped away from the user's own components. The
        /// assembly says who actually built it.
        /// </remarks>
        public static StudioComponentOrigin OriginOf(Type type)
        {
            if (type == null) return StudioComponentOrigin.Project;
            var assembly = type.Assembly.GetName().Name;

            if (assembly == "UnityEngine.UI" || assembly == "Unity.TextMeshPro")
                return StudioComponentOrigin.UGUI;

            if (assembly.StartsWith("UnityEngine", StringComparison.Ordinal) ||
                assembly.StartsWith("Unity.", StringComparison.Ordinal))
                return StudioComponentOrigin.Unity;

            // Test and sample assemblies live under the framework's own prefix but hold user code.
            if (assembly.StartsWith("emiteat.NexUI", StringComparison.Ordinal) &&
                assembly.IndexOf(".Tests", StringComparison.Ordinal) < 0 &&
                assembly.IndexOf(".Samples", StringComparison.Ordinal) < 0)
                return StudioComponentOrigin.NexUI;

            return StudioComponentOrigin.Project;
        }

        public static Texture2D Icon(Type type)
            => type == null ? null : EditorGUIUtility.ObjectContent(null, type)?.image as Texture2D;

        public static string RequirementsOf(Type type)
        {
            if (type == null) return string.Empty;
            var names = new List<string>();
            foreach (var attribute in type.GetCustomAttributes<RequireComponent>())
            {
                Add(attribute.m_Type0);
                Add(attribute.m_Type1);
                Add(attribute.m_Type2);
            }
            return string.Join(", ", names);

            void Add(Type requirement)
            {
                if (requirement == null) return;
                var name = ObjectNames.NicifyVariableName(requirement.Name);
                if (!names.Contains(name)) names.Add(name);
            }
        }

        public static MonoScript FindScript(Type type)
        {
            if (type == null) return null;
            foreach (var guid in AssetDatabase.FindAssets(type.Name + " t:MonoScript"))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script != null && script.GetClass() == type) return script;
            }
            return null;
        }

        // ---- Build -----------------------------------------------------------------------------

        private static List<StudioComponentTypeEntry> Build()
        {
            var entries = new List<StudioComponentTypeEntry>(512);
            foreach (var type in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
            {
                if (!IsAttachable(type)) continue;

                var menuPath = MenuPathOf(type);
                var display = ShortName(type);
                var category = CategoryOf(type);
                var assembly = type.Assembly.GetName().Name;
                entries.Add(new StudioComponentTypeEntry
                {
                    Type = type,
                    Origin = OriginOf(type),
                    QualifiedName = Identity(type),
                    MenuPath = menuPath,
                    DisplayName = display,
                    Category = category,
                    AssemblyName = assembly,
                    DisallowMultiple = type.GetCustomAttribute<DisallowMultipleComponent>() != null,
                    Requirements = RequirementsOf(type),
                    SearchKey = (display + " " + menuPath + " " + category + " " + assembly + " " + type.FullName)
                        .ToLowerInvariant()
                });
            }

            entries.Sort((a, b) => string.Compare(a.MenuPath, b.MenuPath, StringComparison.OrdinalIgnoreCase));
            return entries;
        }

        /// <summary>
        /// Whether a type can exist on a prefab's GameObject at all. Abstract and open generic types
        /// cannot be instantiated, editor-assembly behaviours do not exist in a player, and obsolete
        /// types are not something a new screen should be authored against.
        /// </summary>
        private static bool IsAttachable(Type type)
        {
            if (type == null || type.IsAbstract || type.ContainsGenericParameters) return false;
            if (!type.IsPublic && !type.IsNestedPublic) return false;
            if (type.GetCustomAttribute<ObsoleteAttribute>() != null) return false;

            // The Studio's own bookkeeping component is written by the serializer, never chosen by hand.
            if (type == typeof(DesignerAttachedComponentTracker)) return false;

            if (type.Namespace != null && type.Namespace.StartsWith("UnityEditor", StringComparison.Ordinal))
                return false;

            var assembly = type.Assembly.GetName().Name;
            return !assembly.EndsWith(".Editor", StringComparison.OrdinalIgnoreCase)
                   && !assembly.EndsWith("-Editor", StringComparison.OrdinalIgnoreCase);
        }
    }
}

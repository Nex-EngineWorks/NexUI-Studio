using System;

namespace emiteat.NexUI.Designer
{
    /// <summary>
    /// Builds and reads the <c>typeId</c> used for components that have no entry in the Designer's
    /// own component registry - the user's project scripts and plain Unity components.
    /// </summary>
    /// <remarks>
    /// The registry's ids live in dotted namespaces ("UGUI.Image", "UITK.Button", "NX.RoundedRect").
    /// A project script joins the same id space behind a <c>Project:</c> prefix, which keeps one list
    /// of components per element instead of two, and makes a collision with a registry id impossible.
    ///
    /// The id is the full type name without the assembly, because that is what stays readable in the
    /// serialized asset and stable across an assembly rename. The assembly-qualified name is stored
    /// separately on the component for actually resolving the <c>System.Type</c>.
    /// </remarks>
    public static class DesignerProjectComponentIds
    {
        public const string Prefix = "Project:";

        /// <summary>"Health.HealthBarController, Assembly-CSharp" → "Project:Health.HealthBarController".</summary>
        public static string FromQualifiedName(string assemblyQualifiedName)
        {
            if (string.IsNullOrWhiteSpace(assemblyQualifiedName)) return null;
            var comma = assemblyQualifiedName.IndexOf(',');
            var fullName = comma > 0 ? assemblyQualifiedName.Substring(0, comma) : assemblyQualifiedName;
            return Prefix + fullName.Trim();
        }

        public static bool IsProjectId(string typeId)
            => !string.IsNullOrEmpty(typeId) && typeId.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>"Project:Health.HealthBarController" → "Health.HealthBarController".</summary>
        public static string ToFullName(string typeId)
            => IsProjectId(typeId) ? typeId.Substring(Prefix.Length) : typeId;

        /// <summary>Last segment of the type name, for compact display: "HealthBarController".</summary>
        public static string ShortName(string typeId)
        {
            var fullName = ToFullName(typeId);
            if (string.IsNullOrEmpty(fullName)) return fullName;
            var dot = fullName.LastIndexOf('.');
            return dot >= 0 && dot < fullName.Length - 1 ? fullName.Substring(dot + 1) : fullName;
        }
    }
}

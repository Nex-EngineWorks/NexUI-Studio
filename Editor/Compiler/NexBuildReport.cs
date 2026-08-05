using System;
using System.Globalization;
using System.IO;
using System.Text;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Compiler
{
    /// <summary>
    /// Renders what a compile produced and, for every runtime feature it pulled in, why.
    /// </summary>
    /// <remarks>
    /// The "why" column is the part that earns its keep. Anyone can count nodes; the question
    /// that costs money is why a shipping build contains a subsystem nobody remembers asking
    /// for. Because every feature requirement carries the node that caused it, that question has
    /// a one-line answer here instead of a bisect.
    ///
    /// Written under <c>Library/</c>, not <c>Assets/</c>: it is a derived artifact keyed to one
    /// machine's compile, and putting it in the project would put it in version control, where it
    /// would conflict on every commit and tell nobody anything.
    /// </remarks>
    public static class NexBuildReport
    {
        public static string ReportDirectory =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Library", "NexUI", "Reports");

        public static string Render(NexCompileResult result, NexPublishResult publish, string assetPath)
        {
            var sb = new StringBuilder();
            var program = result != null ? result.Program : null;
            var screenId = program != null ? program.ScreenId : "<unknown>";

            sb.Append("# NexUI build report: ").Append(screenId).Append("\n\n");
            sb.Append("Generated: ").Append(DateTime.Now.ToString("u", CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("Status: ").Append(publish.Published ? "published" : "not published").Append('\n');

            if (publish.Published)
                sb.Append("Output: ").Append(assetPath).Append('\n');
            else
                sb.Append("Output: unchanged (previous version, if any, is intact)\n");

            if (program != null)
            {
                sb.Append("Content hash: ").Append(program.ContentHash).Append('\n');
                sb.Append("Compiler version: ").Append(program.CompilerVersion).Append('\n');
                sb.Append("Nodes: ").Append(program.Nodes.Length).Append('\n');
            }

            if (result != null)
                sb.Append("Compile time: ")
                  .Append(result.ElapsedMs.ToString("F2", CultureInfo.InvariantCulture)).Append(" ms\n");

            sb.Append('\n');
            AppendFeatures(sb, program);
            AppendNodeBreakdown(sb, program);
            AppendDiagnostics(sb, result, publish);

            return sb.ToString();
        }

        private static void AppendFeatures(StringBuilder sb, NexScreenProgram program)
        {
            sb.Append("## Included runtime features\n\n");

            if (program == null || program.Features.Requirements.Count == 0)
            {
                sb.Append("None. This screen needs no optional runtime feature.\n\n");
                return;
            }

            sb.Append("| Feature | Reason | Authoring node |\n|---|---|---|\n");
            foreach (var requirement in program.Features.Requirements)
            {
                sb.Append("| `").Append(requirement.FeatureId).Append("` | ")
                  .Append(requirement.Reason).Append(" | `").Append(requirement.NodeId).Append("` |\n");
            }
            sb.Append('\n');
        }

        private static void AppendNodeBreakdown(StringBuilder sb, NexScreenProgram program)
        {
            if (program == null) return;

            int panels = 0, images = 0, labels = 0, buttons = 0;
            for (int i = 0; i < program.Nodes.Length; i++)
            {
                switch (program.Nodes[i].Kind)
                {
                    case NexNodeKind.Panel: panels++; break;
                    case NexNodeKind.Image: images++; break;
                    case NexNodeKind.Label: labels++; break;
                    case NexNodeKind.Button: buttons++; break;
                }
            }

            sb.Append("## Compiled nodes\n\n");
            sb.Append("| Kind | Count |\n|---|---:|\n");
            sb.Append("| Panel | ").Append(panels).Append(" |\n");
            sb.Append("| Image | ").Append(images).Append(" |\n");
            sb.Append("| Label | ").Append(labels).Append(" |\n");
            sb.Append("| Button | ").Append(buttons).Append(" |\n\n");
        }

        private static void AppendDiagnostics(StringBuilder sb, NexCompileResult result, NexPublishResult publish)
        {
            sb.Append("## Diagnostics\n\n");

            var any = false;

            if (result != null && result.Diagnostics.Count > 0)
            {
                foreach (var diagnostic in result.Diagnostics)
                {
                    any = true;
                    sb.Append("- **").Append(diagnostic.Severity).Append(' ').Append(diagnostic.Code)
                      .Append("** ").Append(diagnostic.Message);

                    if (!diagnostic.Location.IsNone) sb.Append("  \n  at `").Append(diagnostic.Location).Append('`');

                    var entry = NexDiagnosticCodes.Find(diagnostic.Code);
                    if (entry != null && !string.IsNullOrEmpty(entry.Resolution))
                        sb.Append("  \n  Fix: ").Append(entry.Resolution);

                    sb.Append('\n');
                }
            }

            if (publish.Diagnostic != null)
            {
                any = true;
                sb.Append("- **Publish** ").Append(publish.Diagnostic.Code).Append(' ')
                  .Append(publish.Diagnostic.Message).Append('\n');

                var root = publish.Diagnostic.RootCause();
                if (!ReferenceEquals(root, publish.Diagnostic))
                    sb.Append("  \n  Root cause: ").Append(root.Code).Append(' ').Append(root.Message).Append('\n');
            }

            if (!any) sb.Append("None.\n");
        }

        /// <summary>Writes the report and returns its path, or an empty string if it could not be written.</summary>
        /// <remarks>A report that cannot be written is never fatal - the compile it describes already happened.</remarks>
        public static string Write(NexCompileResult result, NexPublishResult publish, string assetPath)
        {
            try
            {
                Directory.CreateDirectory(ReportDirectory);

                var screenId = result != null && result.Program != null ? result.Program.ScreenId : "unknown";
                var fileName = SanitizeFileName(string.IsNullOrEmpty(screenId) ? "unknown" : screenId) + ".md";
                var path = Path.Combine(ReportDirectory, fileName);

                File.WriteAllText(path, Render(result, publish, assetPath));
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NexUI] Build report could not be written: " + ex.Message);
                return string.Empty;
            }
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
                sb.Append(Array.IndexOf(invalid, value[i]) >= 0 ? '_' : value[i]);
            return sb.ToString();
        }
    }
}

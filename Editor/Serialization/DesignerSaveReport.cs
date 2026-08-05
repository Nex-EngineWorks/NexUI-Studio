using System.Collections.Generic;
using System.Text;

namespace emiteat.NexUI.Designer.Editor.Serialization
{
    public enum DesignerSaveImpactKind
    {
        Created,
        Modified,
        Skipped,
        Unsupported,
        PreviewOnly,
        Conflict,
        Orphan,
        UserImpact,

        /// <summary>
        /// States which part of the backend asset the save owns and which part it leaves to the user.
        /// </summary>
        /// <remarks>
        /// Neither a change nor a limitation, so it belongs in neither list: it is the boundary the
        /// other entries are read against. "Preserved user-authored LayoutGroup" only means something
        /// once you know the serializer would otherwise have written that component.
        /// </remarks>
        Ownership
    }

    public sealed class DesignerSaveImpact
    {
        public DesignerSaveImpactKind Kind;
        public string Subject;
        public string Message;
        public string ElementId;
        public string Path;
        public bool WritesToDisk;

        public override string ToString()
            => $"[{Kind}] {(!string.IsNullOrEmpty(Subject) ? Subject + ": " : string.Empty)}{Message}";
    }

    /// <summary>
    /// Result of a Designer save operation. Distinguishes what was actually persisted
    /// from what was skipped or is preview-only, so the tool never implies a change was
    /// written to disk when it was not (acceptance criterion).
    /// </summary>
    public sealed class DesignerSaveReport
    {
        /// <summary>True when this report was produced without mutating assets.</summary>
        public bool IsPreview { get; set; }

        /// <summary>Machine-readable save plan used by the preview UI and tests.</summary>
        public readonly List<DesignerSaveImpact> Impacts = new List<DesignerSaveImpact>();

        /// <summary>Things that were written to disk.</summary>
        public readonly List<string> Changed = new List<string>();

        /// <summary>Things that were intentionally not written (preview-only, unsupported).</summary>
        public readonly List<string> Skipped = new List<string>();

        /// <summary>Non-fatal problems the user should know about.</summary>
        public readonly List<string> Warnings = new List<string>();

        /// <summary>Fatal problems that stopped part of the save.</summary>
        public readonly List<string> Errors = new List<string>();

        /// <summary>What the save owned and overwrote, and what it deliberately left alone.</summary>
        public readonly List<string> Ownership = new List<string>();

        public bool HasErrors => Errors.Count > 0;
        public bool HasWarnings => Warnings.Count > 0;

        public void MarkChanged(string message) => AddImpact(DesignerSaveImpactKind.Modified, message, writesToDisk: true);
        public void MarkCreated(string subject, string message, string elementId = null, string path = null)
            => AddImpact(DesignerSaveImpactKind.Created, message, subject, elementId, path, true);
        public void MarkModified(string subject, string message, string elementId = null, string path = null, bool writesToDisk = true)
            => AddImpact(DesignerSaveImpactKind.Modified, message, subject, elementId, path, writesToDisk);
        public void MarkSkipped(string message) => AddImpact(DesignerSaveImpactKind.Skipped, message);
        public void MarkUnsupported(string subject, string message, string elementId = null)
            => AddImpact(DesignerSaveImpactKind.Unsupported, message, subject, elementId);
        public void MarkPreviewOnly(string subject, string message, string elementId = null)
            => AddImpact(DesignerSaveImpactKind.PreviewOnly, message, subject, elementId);
        public void MarkConflict(string subject, string message, string elementId = null, string path = null)
        {
            AddImpact(DesignerSaveImpactKind.Conflict, message, subject, elementId, path);
            Errors.Add(message);
        }
        public void MarkOrphan(string subject, string message, string elementId = null)
        {
            AddImpact(DesignerSaveImpactKind.Orphan, message, subject, elementId);
            Warnings.Add(message);
        }
        public void MarkUserImpact(string subject, string message, string elementId = null)
            => AddImpact(DesignerSaveImpactKind.UserImpact, message, subject, elementId);

        /// <summary>Records one statement about the overwrite boundary of this save.</summary>
        public void MarkOwnership(string subject, string message, string elementId = null)
            => AddImpact(DesignerSaveImpactKind.Ownership, message, subject, elementId);
        public void Warn(string message)
        {
            Warnings.Add(message);
            AddImpactOnly(DesignerSaveImpactKind.UserImpact, message);
        }
        public void Error(string message)
        {
            Errors.Add(message);
            AddImpactOnly(DesignerSaveImpactKind.Conflict, message);
        }

        public int Count(DesignerSaveImpactKind kind)
        {
            var count = 0;
            foreach (var impact in Impacts) if (impact.Kind == kind) count++;
            return count;
        }

        private void AddImpact(DesignerSaveImpactKind kind, string message, string subject = null,
            string elementId = null, string path = null, bool writesToDisk = false)
        {
            AddImpactOnly(kind, message, subject, elementId, path, writesToDisk);
            if (kind == DesignerSaveImpactKind.Created || kind == DesignerSaveImpactKind.Modified) Changed.Add(message);
            else if (kind == DesignerSaveImpactKind.Ownership) Ownership.Add(message);
            else if (kind == DesignerSaveImpactKind.Skipped || kind == DesignerSaveImpactKind.Unsupported ||
                     kind == DesignerSaveImpactKind.PreviewOnly || kind == DesignerSaveImpactKind.Orphan) Skipped.Add(message);
        }

        private void AddImpactOnly(DesignerSaveImpactKind kind, string message, string subject = null,
            string elementId = null, string path = null, bool writesToDisk = false)
            => Impacts.Add(new DesignerSaveImpact
            {
                Kind = kind, Subject = subject, Message = message, ElementId = elementId,
                Path = path, WritesToDisk = writesToDisk
            });

        public void Merge(DesignerSaveReport other)
        {
            if (other == null) return;
            Changed.AddRange(other.Changed);
            Skipped.AddRange(other.Skipped);
            Ownership.AddRange(other.Ownership);
            Warnings.AddRange(other.Warnings);
            Errors.AddRange(other.Errors);
            Impacts.AddRange(other.Impacts);
            IsPreview |= other.IsPreview;
        }

        /// <summary>One-line summary suitable for a toolbar status label.</summary>
        public string Summary()
        {
            if (IsPreview)
                return HasErrors
                    ? $"Save preview found {Errors.Count} conflict(s). {Count(DesignerSaveImpactKind.Created)} create, {Count(DesignerSaveImpactKind.Modified)} modify."
                    : $"Save preview: {Count(DesignerSaveImpactKind.Created)} create, {Count(DesignerSaveImpactKind.Modified)} modify, {Skipped.Count} skipped/limited.";
            if (HasErrors) return $"Save failed: {Errors.Count} error(s), {Changed.Count} change(s) written.";
            if (HasWarnings) return $"Saved with {Warnings.Count} warning(s). {Changed.Count} change(s) written.";
            return Changed.Count == 0 ? "Nothing to save (no changes)." : $"Saved. {Changed.Count} change(s) written.";
        }

        /// <summary>Full multi-line report for the console / a details panel.</summary>
        public string Details()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Summary());
            // The boundary goes first: every line below it is read against "what did this save own".
            Append(sb, "Overwrite scope", Ownership);
            Append(sb, "Written", Changed);
            Append(sb, "Skipped / preview-only", Skipped);
            Append(sb, "Warnings", Warnings);
            Append(sb, "Errors", Errors);
            return sb.ToString().TrimEnd();
        }

        private static void Append(StringBuilder sb, string header, List<string> items)
        {
            if (items.Count == 0) return;
            sb.AppendLine();
            sb.AppendLine(header + ":");
            foreach (var item in items)
                sb.AppendLine("  - " + item);
        }
    }
}

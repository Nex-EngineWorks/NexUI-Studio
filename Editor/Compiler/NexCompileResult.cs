using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Compiler
{
    /// <summary>Inputs a compile is allowed to vary on. Part of the cache key, so keep it small.</summary>
    public struct NexCompileOptions
    {
        /// <summary>Design resolution the authored rects are absolute against.</summary>
        public Vector2 ReferenceResolution;

        public static NexCompileOptions Default =>
            new NexCompileOptions { ReferenceResolution = new Vector2(1920f, 1080f) };

        public Vector2 ResolvedReferenceResolution =>
            ReferenceResolution.x > 0f && ReferenceResolution.y > 0f
                ? ReferenceResolution
                : new Vector2(1920f, 1080f);
    }

    /// <summary>
    /// What one compile produced: the program when it succeeded, and always the diagnostics.
    /// </summary>
    /// <remarks>
    /// A failed compile still returns a result rather than null or an exception, because the
    /// diagnostics are the useful part of a failure. <see cref="Program"/> being null and
    /// <see cref="Succeeded"/> being false are the same condition stated twice on purpose - call
    /// sites read better for it, and nothing downstream has to null-check to know what happened.
    ///
    /// Note that a successful compile is not yet a published one. Nothing has touched the disk at
    /// this point; see <c>NexScreenPublisher</c> for why that separation matters.
    /// </remarks>
    public sealed class NexCompileResult
    {
        public NexScreenProgram Program { get; }

        public NexDiagnosticBag Diagnostics { get; }

        /// <summary>Wall-clock time of the compile itself, excluding the disk write.</summary>
        public double ElapsedMs { get; }

        public bool Succeeded => Program != null && !Diagnostics.HasErrors;

        public NexCompileResult(NexScreenProgram program, NexDiagnosticBag diagnostics, double elapsedMs)
        {
            Program = program;
            Diagnostics = diagnostics ?? new NexDiagnosticBag();
            ElapsedMs = elapsedMs;
        }

        /// <summary>
        /// The one diagnostic to show when there is only room for one: the top-level failure with
        /// the root cause hanging off it, which is what the error catalog's examples look like.
        /// </summary>
        public NexDiagnostic Summarize(string screenId)
        {
            if (Succeeded) return null;

            var first = Diagnostics.FirstError();
            return NexDiagnosticCodes.Create(
                NexDiagnosticCodes.CompileFailed,
                new NexSourceLocation(screenId),
                "Screen '" + screenId + "' failed to compile.",
                cause: first);
        }
    }
}

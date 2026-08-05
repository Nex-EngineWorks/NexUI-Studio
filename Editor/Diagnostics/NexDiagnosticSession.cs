using emiteat.NexUI.Diagnostics;

namespace emiteat.NexUI.Designer.Editor.Diagnostics
{
    /// <summary>
    /// The editor-wide diagnostic log the console shows.
    /// </summary>
    /// <remarks>
    /// Static, and only here. The runtime assemblies deliberately have no global diagnostic sink -
    /// a router, a screen and an interaction engine each report to whoever owns them, so two of
    /// them can coexist and a test never inherits another test's diagnostics. A console, though,
    /// is inherently one shared thing per editor, and threading a log instance through every menu
    /// item and window would be ceremony that buys nothing.
    ///
    /// It resets on domain reload like any other static, which is the right lifetime: diagnostics
    /// describe what this editing session did.
    /// </remarks>
    public static class NexDiagnosticSession
    {
        private static NexDiagnosticLog _log;

        public static NexDiagnosticLog Log => _log ??= new NexDiagnosticLog();

        /// <summary>Replaces the log. For tests that want isolation.</summary>
        public static void Use(NexDiagnosticLog log) => _log = log;

        public static void Reset() => _log = null;
    }
}

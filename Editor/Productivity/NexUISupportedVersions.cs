namespace emiteat.NexUI.Designer.Editor.Productivity
{
    /// <summary>
    /// The editor versions NexUI claims to support, in one place.
    /// </summary>
    /// <remarks>
    /// This has to agree with the <c>unity</c> field in both package.json files. It exists as a
    /// type rather than as a literal inside the setup check because the check previously tested
    /// for the exact version NexUI was developed on, which warned every supported 2022.3 user that
    /// their editor was unverified. A check that is wrong about a configuration the package
    /// advertises is worse than no check.
    /// </remarks>
    public static class NexUISupportedVersions
    {
        public const int MinimumMajor = 2022;
        public const int MinimumMinor = 3;

        /// <summary>Human-readable floor, for messages.</summary>
        public const string MinimumDisplay = "2022.3 LTS";

        /// <summary>
        /// Whether a version string such as <c>2022.3.62f3</c> or <c>6000.4.2f1</c> is supported.
        /// </summary>
        /// <remarks>
        /// Unity 6 reports a 6000.x major, so a plain numeric comparison orders it above 2022
        /// without a special case. Anything unparseable is reported as unsupported: a version this
        /// cannot read is one nothing was verified against.
        /// </remarks>
        public static bool IsSupported(string unityVersion)
        {
            if (string.IsNullOrEmpty(unityVersion)) return false;

            var parts = unityVersion.Split('.');
            if (parts.Length < 2) return false;
            if (!int.TryParse(parts[0], out var major)) return false;

            if (major > MinimumMajor) return true;
            if (major < MinimumMajor) return false;

            // "2022.3.62f3" - the minor is a plain integer; the patch carries the f/b suffix.
            return int.TryParse(parts[1], out var minor) && minor >= MinimumMinor;
        }
    }
}

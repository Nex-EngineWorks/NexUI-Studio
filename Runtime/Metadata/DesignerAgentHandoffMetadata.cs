using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Designer
{
    /// <summary>
    /// Snapshot of the project's NexUI surface area used to generate the AI agent
    /// handoff manifest (JSON) and brief (Markdown). Populated by the handoff service.
    /// </summary>
    [Serializable]
    public sealed class DesignerAgentHandoffMetadata
    {
        public string package = "com.nexengineworks.nexui";
        public string designerPackage = "com.nexengineworks.nexui.studio";

        public List<string> screens = new();
        public List<string> stateKeys = new();
        public List<string> actionKeys = new();
        public List<string> motionIds = new();
        public List<string> themeIds = new();
        public List<string> contracts = new();
        public List<string> knownIssues = new();
        public List<string> forbiddenRules = new();
    }
}

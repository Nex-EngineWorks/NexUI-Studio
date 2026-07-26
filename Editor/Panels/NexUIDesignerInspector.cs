using System;
using emiteat.NexUI.Designer.Editor.UI.Shell;

namespace emiteat.NexUI.Designer.Editor.Panels
{
    /// <summary>
    /// Compatibility name for integrations that constructed the original Inspector directly.
    /// All Inspector rendering now goes through <see cref="NexUIRightInspector"/>.
    /// </summary>
    [Obsolete("Use NexUIRightInspector. This compatibility type uses the same unified Inspector host.")]
    public sealed class NexUIDesignerInspector : NexUIRightInspector
    {
        public NexUIDesignerInspector(NexUIDesignerContext context) : base(context) { }
    }
}

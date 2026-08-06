namespace emiteat.NexUI.Designer.Editor
{
    public enum DesignerTool
    {
        Select,
        Move,
        Frame,
        Hand,

        /// <summary>
        /// Draws and edits vector paths. Appended rather than inserted: the current tool is
        /// persisted to EditorPrefs as an int, so renumbering the existing entries would silently
        /// change which tool a returning user comes back to.
        /// </summary>
        Pen
    }

    public enum DesignerSidebarTab
    {
        Layers,
        Components,
        Assets
    }

    public enum DesignerInspectorTab
    {
        Design,
        Prototype,
        Motion
    }

    public enum DesignerBottomTab
    {
        Timeline,
        Validation,
        History,
        Graph,
        Preview
    }

    /// <summary>
    /// Canvas interaction mode. <see cref="Design"/> = author (select/move/resize/reparent);
    /// <see cref="Interactive"/> = exercise the UI (hover/press/activate) with <b>simulated</b>
    /// commands logged to the Preview Log - never runs real game logic from the editor.
    /// </summary>
    public enum DesignerInteractionMode
    {
        Design,
        Interactive
    }
}

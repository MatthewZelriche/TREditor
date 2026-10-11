public sealed class CollapseFacesTool : ToolBase, IEditorToolPrimaryAction
{
    public const string ToolId = "tool.collapse_faces";

    public CollapseFacesTool(SelectionModeState? selectionMode = null)
        : base(selectionMode) { }

    public override string Id => ToolId;

    public string PrimaryActionText => "Collapse Selected";

    public EditorToolAction CreatePrimaryAction(IEditorToolContext context) =>
        CollapseFacesCommand.CreateAction(context);
}

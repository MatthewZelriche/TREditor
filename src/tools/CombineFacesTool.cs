public sealed class CombineFacesTool : ToolBase, IEditorToolPrimaryAction
{
    public const string ToolId = "tool.combine_faces";

    public CombineFacesTool(SelectionModeState? selectionMode = null)
        : base(selectionMode) { }

    public override string Id => ToolId;

    public string PrimaryActionText => "Combine Selected";

    public EditorToolAction CreatePrimaryAction(IEditorToolContext context) =>
        CombineFacesCommand.CreateAction(context);
}

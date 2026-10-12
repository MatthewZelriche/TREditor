public sealed class DissolveEdgesTool : ToolBase, IEditorToolPrimaryAction
{
    public const string ToolId = "tool.dissolve_edges";

    public DissolveEdgesTool(SelectionModeState? selectionMode = null)
        : base(selectionMode) { }

    public override string Id => ToolId;

    public string PrimaryActionText => "Dissolve Selected";

    public EditorToolAction CreatePrimaryAction(IEditorToolContext context) =>
        DissolveEdgesCommand.CreateAction(context);
}

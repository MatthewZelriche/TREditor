using System;

public sealed class CollapseEdgesTool : ToolBase, IEditorToolPrimaryAction
{
    public const string ToolId = "tool.collapse_edges";

    public CollapseEdgesTool(SelectionModeState? selectionMode = null)
        : base(selectionMode) { }

    public override string Id => ToolId;

    public string PrimaryActionText => "Collapse Selected";

    public EditorToolAction CreatePrimaryAction(IEditorToolContext context) =>
        CollapseEdgesCommand.CreateAction(context);
}

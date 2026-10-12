public sealed partial class ExtendEdgesTool : LengthToolBase
{
    public const string ToolId = "tool.extend_edges";

    public ExtendEdgesTool(SelectionModeState? selectionMode = null)
        : base(selectionMode) { }

    public override string Id => ToolId;

    public override string PrimaryActionText => "Extend Selected";

    protected override string SettingsName => "ExtendEdgesSettings";

    protected override string SettingsTitle => "Extend Edges";

    protected override EditorToolAction CreateLengthAction(
        IEditorToolContext context,
        float length
    ) => ExtendEdgesCommand.CreateAction(context, length);
}

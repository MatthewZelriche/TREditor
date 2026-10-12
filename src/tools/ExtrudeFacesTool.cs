public sealed partial class ExtrudeFacesTool : LengthToolBase
{
    public const string ToolId = "tool.extrude_faces";

    public ExtrudeFacesTool(SelectionModeState? selectionMode = null)
        : base(selectionMode) { }

    public override string Id => ToolId;

    public override string PrimaryActionText => "Extrude Selected";

    protected override string SettingsName => "ExtrudeFacesSettings";

    protected override string SettingsTitle => "Extrude Faces";

    protected override EditorToolAction CreateLengthAction(
        IEditorToolContext context,
        float length
    ) => ExtrudeFacesCommand.CreateAction(context, length);
}

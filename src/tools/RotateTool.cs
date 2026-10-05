public sealed class RotateTool : ObjectTransformTool
{
    public const string ToolId = "tool.rotate";

    public RotateTool(SelectionModeState selectionMode)
        : base(TransformOperation.Rotate, selectionMode) { }

    public override string Id => ToolId;
}

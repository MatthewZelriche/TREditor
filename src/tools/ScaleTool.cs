public sealed class ScaleTool : ObjectTransformTool
{
    public const string ToolId = "tool.scale";

    public ScaleTool(SelectionModeState selectionMode)
        : base(TransformOperation.Scale, selectionMode) { }

    public override string Id => ToolId;
}

public enum EditorToolStatus
{
    Continue,
    Complete,
    Cancelled,
}

public readonly record struct EditorToolResult(
    EditorToolStatus Status,
    EditorCommand Command = null,
    EditorPreviewRequest Preview = null
)
{
    public static EditorToolResult Continue { get; } = new(EditorToolStatus.Continue);

    public static EditorToolResult ContinueWithCommand(EditorCommand command) =>
        new(EditorToolStatus.Continue, command);

    public static EditorToolResult ContinueWithPreview(EditorPreviewRequest preview) =>
        new(EditorToolStatus.Continue, Preview: preview);

    public static EditorToolResult Complete(
        EditorCommand command = null,
        EditorPreviewRequest preview = null
    ) => new(EditorToolStatus.Complete, command, preview);

    public static EditorToolResult Cancelled(
        EditorCommand command = null,
        EditorPreviewRequest preview = null
    ) => new(EditorToolStatus.Cancelled, command, preview);
}

public enum EditorToolStatus
{
    Continue,
    Complete,
    Cancelled,
}

public readonly record struct EditorToolResult(
    EditorToolStatus Status,
    EditorCommand Command = null
)
{
    public static EditorToolResult Continue { get; } = new(EditorToolStatus.Continue);

    public static EditorToolResult ContinueWithCommand(EditorCommand command) =>
        new(EditorToolStatus.Continue, command);

    public static EditorToolResult Complete(EditorCommand command = null) =>
        new(EditorToolStatus.Complete, command);

    public static EditorToolResult Cancelled(EditorCommand command = null) =>
        new(EditorToolStatus.Cancelled, command);
}

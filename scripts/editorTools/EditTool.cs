// Placeholder for editing components of an individual mesh.
public sealed class EditTool : IEditorTool
{
    public void Enter() { }

    public void Exit() { }

    public EditorToolResult HandleMouseButton(ViewportMouseButtonEvent input) =>
        EditorToolResult.Continue;

    public EditorToolResult HandleMouseMotion(ViewportMouseMotionEvent input) =>
        EditorToolResult.Continue;

    public EditorToolResult Cancel() => EditorToolResult.Cancelled();
}

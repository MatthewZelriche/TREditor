// Placeholder for selecting entire meshes in the scene for all-at-once editing.
public sealed class SelectTool : IEditorTool
{
    public void Enter() { }

    public void Exit() { }

    public EditorToolResult HandleMouseButton(ViewportMouseButtonEvent input) =>
        EditorToolResult.Continue;

    public EditorToolResult HandleMouseMotion(ViewportMouseMotionEvent input) =>
        EditorToolResult.Continue;

    public EditorToolResult Cancel() => EditorToolResult.Cancelled();
}

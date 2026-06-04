public interface IEditorTool
{
    void Enter();

    void Exit();

    EditorToolResult HandleMouseButton(ViewportMouseButtonEvent input);

    EditorToolResult HandleMouseMotion(ViewportMouseMotionEvent input);

    EditorToolResult Cancel();
}

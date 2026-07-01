// Tools should interpret input, compute intent, and return descriptions of what should happen.
// Scene mutation, command execution, and preview ownership stay outside the tools themselves.
public interface IEditorTool
{
    void Enter();

    void Exit();

    EditorToolResult HandleMouseButton(ViewportMouseButtonEvent input);

    EditorToolResult HandleMouseMotion(ViewportMouseMotionEvent input);

    EditorToolResult HandleAction(EditorInputAction action);

    EditorToolResult Cancel();
}

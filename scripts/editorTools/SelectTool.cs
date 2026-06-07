using Godot;

public sealed class SelectTool : IEditorTool
{
    private readonly EditorToolContext _context;

    public SelectTool(EditorToolContext context)
    {
        _context = context;
    }

    public void Enter()
    {
        _context.ObjectSelectionHighlight.SetActive(true);
        _context.SelectionTranslationGizmo.SetActive(true);
    }

    public void Exit()
    {
        _context.ObjectSelectionHighlight.SetActive(false);
        _context.SelectionTranslationGizmo.SetActive(false);
    }

    public EditorToolResult HandleMouseButton(ViewportMouseButtonEvent input) =>
        SelectionToolInput.HandleMouseButton(_context, input, ScenePickElementFilter.Object);

    public EditorToolResult HandleMouseMotion(ViewportMouseMotionEvent input) =>
        EditorToolResult.Continue;

    public EditorToolResult HandleKey(Key key) =>
        key == Key.Delete
            ? EditorToolResult.ContinueWithCommand(
                DeleteMeshCommand.CreateIfAny(_context.Selection.Current)
            )
            : EditorToolResult.Continue;

    public EditorToolResult Cancel() => EditorToolResult.Cancelled();
}

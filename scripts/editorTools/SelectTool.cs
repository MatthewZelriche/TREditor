// Placeholder for selecting entire meshes in the scene for all-at-once editing.
public sealed class SelectTool : IEditorTool
{
    private readonly EditorToolContext _context;

    public SelectTool(EditorToolContext context)
    {
        _context = context;
    }

    public void Enter() { }

    public void Exit() { }

    public EditorToolResult HandleMouseButton(ViewportMouseButtonEvent input) =>
        SelectionToolInput.HandleMouseButton(_context, input, ScenePickElementFilter.Object);

    public EditorToolResult HandleMouseMotion(ViewportMouseMotionEvent input) =>
        EditorToolResult.Continue;

    public EditorToolResult Cancel() => EditorToolResult.Cancelled();
}

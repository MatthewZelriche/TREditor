// Placeholder for editing components of an individual mesh.
public sealed class EditTool : IEditorTool
{
    private readonly EditorToolContext _context;

    public EditTool(EditorToolContext context)
    {
        _context = context;
    }

    public void Enter() { }

    public void Exit() { }

    public EditorToolResult HandleMouseButton(ViewportMouseButtonEvent input) =>
        SelectionToolInput.HandleMouseButton(_context, input, ScenePickElementFilter.AnyComponent);

    public EditorToolResult HandleMouseMotion(ViewportMouseMotionEvent input) =>
        EditorToolResult.Continue;

    public EditorToolResult Cancel() => EditorToolResult.Cancelled();
}

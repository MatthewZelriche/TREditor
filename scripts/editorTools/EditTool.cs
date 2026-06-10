using Godot;

public sealed class EditTool : IEditorTool
{
    // First-pass default; later this should be driven by an Edit-mode UI toggle.
    private const bool XRayModeEnabled = true;

    private readonly EditorToolContext _context;

    public EditTool(EditorToolContext context)
    {
        _context = context;
    }

    public void Enter()
    {
        _context.ComponentSelectionHighlight.SetMode(ComponentHighlightMode.Edit);
        _context.ComponentSelectionHighlight.SetActive(true);
        _context.SelectionTranslationGizmo.SetActive(true);
    }

    public void Exit()
    {
        _context.ComponentSelectionHighlight.SetActive(false);
        _context.SelectionTranslationGizmo.SetActive(false);
    }

    public EditorToolResult HandleMouseButton(ViewportMouseButtonEvent input)
    {
        UpdateHover(input.RayOrigin, input.RayDirection);
        return SelectionToolInput.HandleMouseButton(
            _context,
            input,
            ScenePickElementFilter.AnyComponent,
            XRayModeEnabled
        );
    }

    public EditorToolResult HandleMouseMotion(ViewportMouseMotionEvent input)
    {
        UpdateHover(input.RayOrigin, input.RayDirection);
        return EditorToolResult.Continue;
    }

    public EditorToolResult HandleKey(Key key) =>
        key == Key.Delete
            ? EditorToolResult.ContinueWithCommand(
                DeleteFaceCommand.CreateIfAny(_context.Selection.Current)
            )
            : EditorToolResult.Continue;

    public EditorToolResult Cancel() => EditorToolResult.Cancelled();

    private void UpdateHover(Vector3 rayOrigin, Vector3 rayDirection)
    {
        SelectionTarget? hover = null;
        if (
            _context.ScenePicking.TryPickScene(
                rayOrigin,
                rayDirection,
                out ScenePickHit hit,
                ScenePickElementFilter.AnyComponent,
                XRayModeEnabled
            ) && SelectionTarget.TryFromHit(hit, out SelectionTarget target)
        )
        {
            hover = target;
        }

        _context.ComponentSelectionHighlight.SetPointerState(rayOrigin, hover);
    }
}

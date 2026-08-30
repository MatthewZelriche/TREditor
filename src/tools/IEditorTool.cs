// An interactive editor tool driven by the three viewport mouse phases. The context is supplied per
// call so a tool never stores the editor services it was activated with.
public interface IEditorTool
{
    // Stable registry key. Tools expose it as a constant so the UI can activate one without
    // referencing its type.
    string Id { get; }

    void OnMouseDown(IEditorToolContext context, EditorMouseInput input);

    void OnMouseDrag(IEditorToolContext context, EditorMouseInput input);

    void OnMouseUp(IEditorToolContext context, EditorMouseInput input);

    // Abandons any in-progress interaction without committing it. Must be safe to call when the tool
    // is idle, because lifecycle transitions cancel unconditionally.
    void Cancel(IEditorToolContext context);
}

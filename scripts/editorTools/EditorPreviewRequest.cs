public abstract record EditorPreviewRequest
{
    public sealed record Clear : EditorPreviewRequest;

    public sealed record Primitive(PrimitiveCreationSettings Settings, PrimitiveBounds Bounds)
        : EditorPreviewRequest;

    public sealed record TranslateSelection(SelectionSnapshot Selection, Godot.Vector3 Delta)
        : EditorPreviewRequest;
}

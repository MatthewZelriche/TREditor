public abstract record EditorPreviewRequest
{
    public sealed record Clear : EditorPreviewRequest;

    public sealed record Primitive(PrimitiveCreationSettings Settings, PrimitiveBounds Bounds)
        : EditorPreviewRequest;
}

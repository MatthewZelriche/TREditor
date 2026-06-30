public abstract record EditorPreviewRequest
{
    public sealed record Clear : EditorPreviewRequest;

    public sealed record Primitive(PrimitiveCreationSettings Settings, PrimitiveBounds Bounds)
        : EditorPreviewRequest;

    public sealed record TranslateSelection(SelectionSnapshot Selection, Godot.Vector3 Delta)
        : EditorPreviewRequest;

    public sealed record ExtrudeFace(SelectionTarget Face, Godot.Vector3 Delta)
        : EditorPreviewRequest;

    public sealed record ExtrudeEdge(SelectionTarget Edge, Godot.Vector3 Delta)
        : EditorPreviewRequest;

    public sealed record InsetFace(SelectionTarget Face, float Depth) : EditorPreviewRequest;

    public sealed record BevelEdges(SelectionSnapshot Selection, float Width)
        : EditorPreviewRequest;

    public sealed record BevelVertices(SelectionSnapshot Selection, float Width)
        : EditorPreviewRequest;

    public sealed record FillHole(SelectionTarget Edge) : EditorPreviewRequest;

    public sealed record CollapseFace(SelectionTarget Face) : EditorPreviewRequest;

    public sealed record CollapseVertices(
        SelectionSnapshot Selection,
        CollapseVerticesTarget TwoVertexTarget
    ) : EditorPreviewRequest;

    public sealed record BridgeEdges(
        SelectionTarget First,
        SelectionTarget Second,
        int Segments,
        float ArchAngleDegrees
    ) : EditorPreviewRequest;

    public sealed record DetachFaces(SelectionSnapshot Selection) : EditorPreviewRequest;

    public sealed record EdgeCut(
        Godot.Transform3D MeshTransform,
        Godot.Vector3 Start,
        Godot.Vector3 End,
        bool HasValidTarget
    ) : EditorPreviewRequest;
}

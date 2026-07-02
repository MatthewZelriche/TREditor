using System;

/// <summary>
/// Tagged selection/pick payload. Only one handle kind is active; <see cref="None"/> is the
/// explicit invalid sentinel.
/// </summary>
public readonly record struct SelectionElement
{
    private readonly ScenePickElementKind _kind;
    private readonly VertexHandle _vertex;
    private readonly HalfEdgeHandle _edge;
    private readonly FaceHandle _face;

    private SelectionElement(
        ScenePickElementKind kind,
        VertexHandle vertex,
        HalfEdgeHandle edge,
        FaceHandle face
    )
    {
        _kind = kind;
        _vertex = vertex;
        _edge = edge;
        _face = face;
    }

    public ScenePickElementKind Kind => _kind;

    public bool IsValid => _kind != ScenePickElementKind.None;

    public static SelectionElement None => default;

    public static SelectionElement Object() =>
        new(ScenePickElementKind.Object, default, default, default);

    public static SelectionElement Vertex(VertexHandle vertex) =>
        new(ScenePickElementKind.Vertex, vertex, default, default);

    public static SelectionElement Edge(HalfEdgeHandle edge) =>
        new(ScenePickElementKind.Edge, default, edge, default);

    public static SelectionElement Face(FaceHandle face) =>
        new(ScenePickElementKind.Face, default, default, face);

    public bool TryGetVertex(out VertexHandle vertex)
    {
        vertex = _vertex;
        return _kind == ScenePickElementKind.Vertex;
    }

    public bool TryGetEdge(out HalfEdgeHandle edge)
    {
        edge = _edge;
        return _kind == ScenePickElementKind.Edge;
    }

    public bool TryGetFace(out FaceHandle face)
    {
        face = _face;
        return _kind == ScenePickElementKind.Face;
    }
}

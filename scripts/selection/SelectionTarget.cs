using System;

public readonly record struct SelectionTarget
{
    public EditorObjectId ObjectId { get; init; }

    public SelectionElement Element { get; init; }

    public ScenePickElementKind Kind => Element.Kind;

    public bool IsValid => ObjectId.Value != Guid.Empty && Element.IsValid;

    public VertexHandle Vertex => Element.TryGetVertex(out VertexHandle vertex) ? vertex : default;

    public HalfEdgeHandle Edge => Element.TryGetEdge(out HalfEdgeHandle edge) ? edge : default;

    public FaceHandle Face => Element.TryGetFace(out FaceHandle face) ? face : default;

    public static SelectionTarget ForObject(EditorObjectId objectId) =>
        new() { ObjectId = objectId, Element = SelectionElement.Object() };

    public static SelectionTarget ForVertex(EditorObjectId objectId, VertexHandle vertex) =>
        new() { ObjectId = objectId, Element = SelectionElement.Vertex(vertex) };

    public static SelectionTarget ForEdge(EditorObjectId objectId, HalfEdgeHandle edge) =>
        new() { ObjectId = objectId, Element = SelectionElement.Edge(edge) };

    public static SelectionTarget ForFace(EditorObjectId objectId, FaceHandle face) =>
        new() { ObjectId = objectId, Element = SelectionElement.Face(face) };

    public static bool TryFromHit(ScenePickHit hit, out SelectionTarget target)
    {
        target = default;

        if (!hit.HasHit)
            return false;

        target = hit.Kind switch
        {
            ScenePickElementKind.Object => ForObject(hit.ObjectId),
            ScenePickElementKind.Vertex => ForVertex(hit.ObjectId, hit.Vertex),
            ScenePickElementKind.Edge => ForEdge(hit.ObjectId, hit.Edge),
            ScenePickElementKind.Face => ForFace(hit.ObjectId, hit.Face),
            _ => default,
        };

        return target.IsValid;
    }

    public override string ToString() =>
        Kind switch
        {
            ScenePickElementKind.Object => $"{ObjectId}:Object",
            ScenePickElementKind.Vertex => $"{ObjectId}:Vertex:{Vertex}",
            ScenePickElementKind.Edge => $"{ObjectId}:Edge:{Edge}",
            ScenePickElementKind.Face => $"{ObjectId}:Face:{Face}",
            _ => throw new InvalidOperationException($"Unsupported selection kind {Kind}."),
        };
}

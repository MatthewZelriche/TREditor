using System;

// TODO: Some kind of union/variant?
public readonly record struct SelectionTarget(
    EditorObjectId ObjectId,
    ScenePickElementKind Kind,
    VertexHandle Vertex,
    HalfEdgeHandle Edge,
    FaceHandle Face
)
{
    public static SelectionTarget ForObject(EditorObjectId objectId) =>
        new(objectId, ScenePickElementKind.Object, default, default, default);

    public static SelectionTarget ForVertex(EditorObjectId objectId, VertexHandle vertex) =>
        new(objectId, ScenePickElementKind.Vertex, vertex, default, default);

    public static SelectionTarget ForEdge(EditorObjectId objectId, HalfEdgeHandle edge) =>
        new(objectId, ScenePickElementKind.Edge, default, edge, default);

    public static SelectionTarget ForFace(EditorObjectId objectId, FaceHandle face) =>
        new(objectId, ScenePickElementKind.Face, default, default, face);

    public static bool TryFromHit(ScenePickHit hit, out SelectionTarget target)
    {
        target = default;

        if (!hit.HasHit || hit.Mesh == null)
        {
            return false;
        }

        target = hit.Kind switch
        {
            ScenePickElementKind.Object => ForObject(hit.Mesh.ObjectId),
            ScenePickElementKind.Vertex => ForVertex(hit.Mesh.ObjectId, hit.Vertex),
            ScenePickElementKind.Edge => ForEdge(hit.Mesh.ObjectId, hit.Edge),
            ScenePickElementKind.Face => ForFace(hit.Mesh.ObjectId, hit.Face),
            _ => default,
        };

        return target.Kind != ScenePickElementKind.None;
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

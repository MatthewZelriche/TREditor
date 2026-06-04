using Godot;

public enum ScenePickElementKind
{
    None,
    Object,
    Vertex,
    Edge,
    Face,
}

public enum ScenePickElementFilter
{
    AnyComponent,
    Object,
    Vertex,
    Edge,
    Face,
}

public readonly record struct ScenePickHit(
    ScenePickElementKind Kind,
    TRMeshGD Mesh,
    VertexHandle Vertex,
    HalfEdgeHandle Edge,
    FaceHandle Face,
    Vector3 Position,
    float Distance
)
{
    public static ScenePickHit None => default;

    public bool HasHit => Kind != ScenePickElementKind.None;

    public static ScenePickHit ObjectHit(TRMeshGD mesh, Vector3 position, float distance) =>
        new(ScenePickElementKind.Object, mesh, default, default, default, position, distance);

    public static ScenePickHit VertexHit(
        TRMeshGD mesh,
        VertexHandle vertex,
        Vector3 position,
        float distance
    ) => new(ScenePickElementKind.Vertex, mesh, vertex, default, default, position, distance);

    public static ScenePickHit EdgeHit(
        TRMeshGD mesh,
        HalfEdgeHandle edge,
        Vector3 position,
        float distance
    ) => new(ScenePickElementKind.Edge, mesh, default, edge, default, position, distance);

    public static ScenePickHit FaceHit(
        TRMeshGD mesh,
        FaceHandle face,
        Vector3 position,
        float distance
    ) => new(ScenePickElementKind.Face, mesh, default, default, face, position, distance);
}

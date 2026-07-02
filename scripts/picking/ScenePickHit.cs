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
    EditorObjectId ObjectId,
    SelectionElement Element,
    Vector3 Position,
    float Distance
)
{
    public static ScenePickHit None => default;

    public ScenePickElementKind Kind => Element.Kind;

    public bool HasHit => ObjectId.Value != System.Guid.Empty && Element.IsValid;

    public VertexHandle Vertex => Element.TryGetVertex(out VertexHandle vertex) ? vertex : default;

    public HalfEdgeHandle Edge => Element.TryGetEdge(out HalfEdgeHandle edge) ? edge : default;

    public FaceHandle Face => Element.TryGetFace(out FaceHandle face) ? face : default;

    public static ScenePickHit ObjectHit(
        EditorObjectId objectId,
        Vector3 position,
        float distance
    ) => new(objectId, SelectionElement.Object(), position, distance);

    public static ScenePickHit VertexHit(
        EditorObjectId objectId,
        VertexHandle vertex,
        Vector3 position,
        float distance
    ) => new(objectId, SelectionElement.Vertex(vertex), position, distance);

    public static ScenePickHit EdgeHit(
        EditorObjectId objectId,
        HalfEdgeHandle edge,
        Vector3 position,
        float distance
    ) => new(objectId, SelectionElement.Edge(edge), position, distance);

    public static ScenePickHit FaceHit(
        EditorObjectId objectId,
        FaceHandle face,
        Vector3 position,
        float distance
    ) => new(objectId, SelectionElement.Face(face), position, distance);
}

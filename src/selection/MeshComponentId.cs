public enum MeshComponentType
{
    Vertex,
    Edge,
    Face,
}

// Component slots are TRMesh topology handles. Edge slots use the lower handle from a twin
// half-edge pair, giving each undirected edge one stable identity.
public readonly record struct MeshComponentId(
    MeshObjectId ObjectId,
    MeshComponentType Type,
    int Slot
);

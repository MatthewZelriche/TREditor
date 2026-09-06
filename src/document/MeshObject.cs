using Godot;

// Immutable document-owned mesh object. Metadata edits install a replacement record rather than
// mutating an existing one. Callers that retain a previously returned instance observe its original
// values.
public sealed record MeshObject
{
    internal MeshObject(MeshObjectId id, string name, Transform3D transform, GDMesh mesh)
    {
        Id = id;
        Name = name;
        Transform = transform;
        Mesh = mesh;
    }

    public MeshObjectId Id { get; init; }

    public string Name { get; init; }

    public Transform3D Transform { get; init; }

    // Document-owned mesh. Kept internal until a safe mesh read API is introduced.
    internal GDMesh Mesh { get; init; }
}

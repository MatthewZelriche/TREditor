using System.Collections.Generic;
using Godot;

// Read-only view of the editor document. Immutable MeshObject records may cross the boundary; no
// member may hand out a live collection, GDMesh, or SpatialMesh.
public interface IEditorDocumentReader
{
    int ObjectCount { get; }

    bool TryGetMeshTransform(MeshObjectId id, out Transform3D transform);

    bool TryGetObject(MeshObjectId id, out MeshObject meshObject);

    IReadOnlyList<MeshObjectId> GetObjectIds();
}

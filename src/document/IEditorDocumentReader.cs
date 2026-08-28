using System.Collections.Generic;
using Godot;

// Read-only view of the editor document. Implementations must return copied value data only; no
// member may hand out a live model object, mesh, or scene-backed collection.
public interface IEditorDocumentReader
{
    int ObjectCount { get; }

    bool TryGetMeshTransform(MeshObjectId id, out Transform3D transform);

    IReadOnlyList<MeshObjectId> GetObjectIds();
}

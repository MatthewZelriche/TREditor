using TREditorSharp;

internal interface IEditorObjectLifecycle
{
    bool CreateMeshObject(EditorObjectId objectId, SpatialMesh mesh, string displayName);

    bool RemoveMeshObject(EditorObjectId objectId);

    bool RestoreMeshObject(EditorObjectId objectId);

    bool DestroyMeshObject(EditorObjectId objectId);
}

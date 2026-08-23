public interface ISceneService
{
    void Attach(Scene scene);

    void Detach();

    void OnObjectAdded(Scene.MeshObject meshObject);

    void OnObjectRemoved(Scene.MeshObject meshObject);

    void OnObjectGeometryChanged(MeshObjectId id);

    void OnObjectTransformChanged(MeshObjectId id);

    void Flush();
}

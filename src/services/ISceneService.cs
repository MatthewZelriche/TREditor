using System;

public interface ISceneService : IDisposable
{
    /// <summary>
    /// Replaces the scene used by this service and clears state derived from the previous scene.
    /// Existing objects in <paramref name="scene"/> are replayed separately by
    /// <see cref="SceneServices"/>. The service does not own or dispose either scene.
    /// </summary>
    /// <remarks>
    /// If this method throws, <see cref="SceneServices"/> treats the entire service collection
    /// as unusable and disposes it. Implementations are not required to support recovery.
    /// </remarks>
    void SetScene(Scene scene);

    void OnObjectAdded(Scene.MeshObject meshObject);

    void OnObjectRemoved(Scene.MeshObject meshObject);

    void OnObjectGeometryChanged(MeshObjectId id);

    void OnObjectTransformChanged(MeshObjectId id);

    void Flush();
}

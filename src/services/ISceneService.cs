using System;

public interface ISceneService : IDisposable
{
    /// <summary>
    /// Replaces the scene used by this service and clears state derived from the previous scene.
    /// Existing objects in <paramref name="scene"/> are replayed separately by
    /// <see cref="SceneServices"/>. The service does not own or dispose either scene.
    /// </summary>
    /// <remarks>
    /// After successful construction, <see cref="SceneServices"/> catches service failures at its
    /// boundary, reports a fatal error, and stops dispatching further work. Implementations are not
    /// required to support recovery.
    /// As a result, this means throwing exceptions should be reserved for fatal scenarios only.
    /// </remarks>
    void SetScene(Scene scene);

    void OnObjectAdded(Scene.MeshObject meshObject);

    void OnObjectRemoved(Scene.MeshObject meshObject);

    void OnObjectGeometryChanged(MeshObjectId id);

    void OnObjectTransformChanged(MeshObjectId id);

    void Flush();
}

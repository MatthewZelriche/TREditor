using System.Collections.Generic;
using Godot;

/// <summary>
/// Narrow view contract used by <see cref="EditorObjectLifecycle"/>
/// </summary>
public interface IEditorSceneView
{
    bool Attach(EditorObjectModel obj);

    void Destroy(EditorObjectId id);

    void SyncTransform(EditorObjectModel obj);

    void SyncGeometry(EditorObjectId id);

    void SyncRender(EditorObjectId id);

    bool TryGetNode(EditorObjectId id, out TRMeshGD node);

    IEnumerable<KeyValuePair<EditorObjectId, TRMeshGD>> Nodes { get; }

    void Clear();
}

using System;
using System.Collections.Generic;
using Godot;
using TREditorSharp;

// Centralizes committed scene-node mutation for now. This is intentionally a step toward
// a proper editor document/model layer, where commands would mutate model state and the
// Godot scene would become a synchronized view of that model.
public sealed class EditorSceneService : IDisposable
{
    private readonly Node3D _worldRoot;
    private readonly Dictionary<EditorObjectId, TRMeshGD> _meshNodes = [];

    private bool _disposed;

    public EditorSceneService(Node3D worldRoot)
    {
        ArgumentNullException.ThrowIfNull(worldRoot);

        _worldRoot = worldRoot;
    }

    public void CreateMeshObject(EditorObjectId objectId, SpatialMesh mesh, string displayName)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (_meshNodes.TryGetValue(objectId, out TRMeshGD existingNode))
        {
            if (existingNode.GetParent() == null)
            {
                _worldRoot.AddChild(existingNode);
            }

            return;
        }

        TRMeshGD meshNode = new() { Name = displayName };
        meshNode.TakeMesh(mesh);
        _meshNodes.Add(objectId, meshNode);
        _worldRoot.AddChild(meshNode);
    }

    public void RemoveMeshObject(EditorObjectId objectId)
    {
        if (!_meshNodes.TryGetValue(objectId, out TRMeshGD meshNode))
        {
            return;
        }

        Node parent = meshNode.GetParent();
        parent?.RemoveChild(meshNode);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (TRMeshGD meshNode in _meshNodes.Values)
        {
            Node parent = meshNode.GetParent();
            parent?.RemoveChild(meshNode);
            meshNode.QueueFree();
        }

        _meshNodes.Clear();
    }
}

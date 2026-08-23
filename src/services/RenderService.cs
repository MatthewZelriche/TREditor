using System;
using System.Collections.Generic;
using Godot;

public sealed class RenderService : ISceneService
{
    private readonly Node3D _renderRoot;
    private readonly Dictionary<MeshObjectId, MeshInstance3D> _instances = [];
    private readonly HashSet<MeshObjectId> _dirtyMeshes = [];
    private Scene _scene;
    private bool _disposed;

    public RenderService(Scene scene, Node3D mapRoot)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(mapRoot);

        _scene = scene;
        _renderRoot = new Node3D { Name = "Render" };
        try
        {
            mapRoot.AddChild(_renderRoot);
        }
        catch
        {
            _renderRoot.Free();
            throw;
        }
    }

    public void SetScene(Scene scene)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(scene);
        if (ReferenceEquals(_scene, scene))
            return;

        ClearSceneState();
        _scene = scene;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ClearSceneState();
        _renderRoot.Free();
    }

    public void OnObjectAdded(Scene.MeshObject meshObject)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(meshObject);
        ArgumentNullException.ThrowIfNull(meshObject.Mesh);

        var instance = new MeshInstance3D
        {
            Name = $"MeshObject_{meshObject.Id.Value}",
            Mesh = meshObject.Mesh.ArrayMesh,
            Transform = meshObject.Transform,
        };

        _renderRoot.AddChild(instance);
        _instances.Add(meshObject.Id, instance);
        meshObject.Mesh.MarkDirty();
        _dirtyMeshes.Add(meshObject.Id);
    }

    public void OnObjectRemoved(Scene.MeshObject meshObject)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(meshObject);

        _dirtyMeshes.Remove(meshObject.Id);
        if (!_instances.Remove(meshObject.Id, out MeshInstance3D? instance))
            return;

        instance.Mesh = null;
        instance.Free();
    }

    public void OnObjectGeometryChanged(MeshObjectId id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_scene.Objects.TryGetValue(id, out Scene.MeshObject? meshObject))
            return;

        meshObject.Mesh.MarkDirty();
        _dirtyMeshes.Add(id);
    }

    public void OnObjectTransformChanged(MeshObjectId id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (
            _scene.Objects.TryGetValue(id, out Scene.MeshObject? meshObject)
            && _instances.TryGetValue(id, out MeshInstance3D? instance)
        )
        {
            instance.Transform = meshObject.Transform;
        }
    }

    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (MeshObjectId id in _dirtyMeshes)
        {
            if (_scene.Objects.TryGetValue(id, out Scene.MeshObject? meshObject))
                meshObject.Mesh.RebuildIfDirty();
        }

        _dirtyMeshes.Clear();
    }

    private void ClearSceneState()
    {
        foreach (MeshInstance3D instance in _instances.Values)
        {
            instance.Mesh = null;
            instance.Free();
        }

        _instances.Clear();
        _dirtyMeshes.Clear();
    }
}

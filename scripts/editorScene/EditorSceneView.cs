using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Owns live <see cref="TRMeshGD"/> nodes for model objects. Nodes borrow model meshes and never
/// dispose them.
/// </summary>
public sealed class EditorSceneView : IEditorSceneView, IDisposable
{
    private readonly Node3D _worldRoot;
    private readonly TextureMaterialLibrary _textureMaterials;
    private readonly Dictionary<EditorObjectId, TRMeshGD> _nodes = [];
    private bool _disposed;

    public EditorSceneView(Node3D worldRoot, TextureMaterialLibrary textureMaterials)
    {
        ArgumentNullException.ThrowIfNull(worldRoot);
        ArgumentNullException.ThrowIfNull(textureMaterials);

        _worldRoot = worldRoot;
        _textureMaterials = textureMaterials;
    }

    public IEnumerable<KeyValuePair<EditorObjectId, TRMeshGD>> Nodes
    {
        get
        {
            ThrowIfDisposed();
            return _nodes;
        }
    }

    public bool Attach(EditorObjectModel obj)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(obj);

        if (_nodes.ContainsKey(obj.Id))
            return false;

        TRMeshGD meshNode = new() { Name = obj.Name, ObjectId = obj.Id };
        meshNode.SetTextureMaterialLibrary(_textureMaterials);
        meshNode.BindMesh(obj.Mesh);
        meshNode.Transform = obj.LocalTransform;

        try
        {
            _worldRoot.AddChild(meshNode);
        }
        catch
        {
            meshNode.UnbindMesh();
            meshNode.Free();
            return false;
        }

        _nodes.Add(obj.Id, meshNode);
        return true;
    }

    public void Destroy(EditorObjectId id)
    {
        ThrowIfDisposed();

        if (!_nodes.Remove(id, out TRMeshGD meshNode))
            return;

        DestroyNode(meshNode);
    }

    public void SyncTransform(EditorObjectModel obj)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(obj);

        if (_nodes.TryGetValue(obj.Id, out TRMeshGD meshNode))
            meshNode.Transform = obj.LocalTransform;
    }

    public void SyncGeometry(EditorObjectId id)
    {
        ThrowIfDisposed();

        if (_nodes.TryGetValue(id, out TRMeshGD meshNode))
            meshNode.Rebuild();
    }

    public void SyncRender(EditorObjectId id)
    {
        ThrowIfDisposed();

        if (_nodes.TryGetValue(id, out TRMeshGD meshNode))
            meshNode.RebuildRender();
    }

    public bool TryGetNode(EditorObjectId id, out TRMeshGD node)
    {
        ThrowIfDisposed();
        return _nodes.TryGetValue(id, out node);
    }

    public void Clear()
    {
        ThrowIfDisposed();

        foreach (TRMeshGD meshNode in _nodes.Values)
            DestroyNode(meshNode);

        _nodes.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Clear();
        GC.SuppressFinalize(this);
    }

    private static void DestroyNode(TRMeshGD meshNode)
    {
        Node parent = meshNode.GetParent();
        parent?.RemoveChild(meshNode);
        meshNode.UnbindMesh();
        meshNode.Free();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

using System;
using System.Collections.Generic;
using Godot;

public sealed class Scene : IDisposable, IEditorDocumentReader
{
    // Exists to enforce that only Scene class instances can access MeshObject.SetTransform
    private static readonly object TransformGuard = new();

    readonly Dictionary<MeshObjectId, MeshObject> _objects = [];
    int _nextObjectId;
    bool _disposed;

    internal IReadOnlyDictionary<MeshObjectId, MeshObject> Objects => _objects;

    public int ObjectCount => _objects.Count;

    internal event Action<MeshObject>? ObjectAdded;
    internal event Action<MeshObject>? ObjectRemoved;
    internal event Action<MeshObjectId>? ObjectGeometryChanged;
    internal event Action<MeshObjectId>? ObjectTransformChanged;

    public bool TryGetMeshTransform(MeshObjectId id, out Transform3D transform)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_objects.TryGetValue(id, out MeshObject? meshObject))
        {
            transform = default;
            return false;
        }

        transform = meshObject.Transform;
        return true;
    }

    public IReadOnlyList<MeshObjectId> GetObjectIds()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var ids = new MeshObjectId[_objects.Count];
        _objects.Keys.CopyTo(ids, 0);
        return ids;
    }

    internal MeshObject Add(string name, Transform3D transform, GDMesh mesh)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(name);

        var id = new MeshObjectId(_nextObjectId);
        _nextObjectId = checked(_nextObjectId + 1);
        var meshObject = new MeshObject(id, name, transform, mesh);
        _objects.Add(meshObject.Id, meshObject);
        ObjectAdded?.Invoke(meshObject);
        return meshObject;
    }

    internal bool Remove(MeshObjectId id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_objects.Remove(id, out MeshObject? meshObject))
            return false;

        try
        {
            ObjectRemoved?.Invoke(meshObject);
        }
        finally
        {
            meshObject.Mesh?.Dispose();
        }

        return true;
    }

    internal bool SetTransform(MeshObjectId id, Transform3D transform)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_objects.TryGetValue(id, out MeshObject? meshObject))
            return false;

        meshObject.SetTransform(transform, TransformGuard);
        ObjectTransformChanged?.Invoke(id);
        return true;
    }

    internal bool MarkGeometryChanged(MeshObjectId id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_objects.ContainsKey(id))
            return false;

        ObjectGeometryChanged?.Invoke(id);
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (MeshObject meshObject in _objects.Values)
            meshObject.Mesh?.Dispose();

        _objects.Clear();
        ObjectAdded = null;
        ObjectRemoved = null;
        ObjectGeometryChanged = null;
        ObjectTransformChanged = null;
    }

    public sealed class MeshObject
    {
        internal MeshObject(MeshObjectId id, string name, Transform3D transform, GDMesh mesh)
        {
            Id = id;
            Name = name;
            Transform = transform;
            Mesh = mesh;
        }

        internal void SetTransform(Transform3D transform, object guard)
        {
            if (!ReferenceEquals(guard, TransformGuard))
            {
                throw new InvalidOperationException(
                    "Transform can only be changed through Scene.SetTransform."
                );
            }

            Transform = transform;
        }

        public MeshObjectId Id { get; }

        public string Name { get; }

        public Transform3D Transform { get; private set; }

        public GDMesh Mesh { get; }
    }
}

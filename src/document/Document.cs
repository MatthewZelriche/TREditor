using System;
using System.Collections.Generic;
using Godot;

// Authoritative document for persisted data. Owns MeshObject records; transient state
// such as selection, history, etc is stored elsewhere
public sealed class Document : IDisposable, IEditorDocumentReader
{
    readonly Dictionary<MeshObjectId, MeshObject> _objects = [];
    int _nextObjectId;
    bool _disposed;

    public int ObjectCount => _objects.Count;

    // Compatibility notifications: Commit 3 replaces these with one post-commit DocumentChangeSet.
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

    // Snapshot of current object records. Callers may retain entries; later replacements do not mutate them.
    internal MeshObject[] GetObjects()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var objects = new MeshObject[_objects.Count];
        _objects.Values.CopyTo(objects, 0);
        return objects;
    }

    internal bool TryGetObject(MeshObjectId id, out MeshObject meshObject)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _objects.TryGetValue(id, out meshObject!);
    }

    // Compatibility insertion path: Commit 3 makes add atomic via candidate collection state and
    // explicit mesh ownership transfer before publication.
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

    // Compatibility removal path: Commit 3 changes ownership so removal retains mesh state for
    // possible undo instead of disposing during the observable callback.
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

    // Compatibility transform path: Commit 2 removes this once callers use ReplaceObject.
    internal bool SetTransform(MeshObjectId id, Transform3D transform)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_objects.TryGetValue(id, out MeshObject? current))
            return false;

        _objects[id] = current with { Transform = transform };
        ObjectTransformChanged?.Invoke(id);
        return true;
    }

    // Compatibility geometry notification: Commit 3 replaces this with an internal committed-change
    // route so arbitrary callers cannot announce geometry changes that did not occur.
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
}

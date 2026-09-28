using System;
using System.Collections.Generic;
using Godot;

// Authoritative document for persisted data. Owns MeshObject records; transient state
// such as selection, history, etc is stored elsewhere.
public sealed class Document : IDisposable, IEditorDocumentReader
{
    readonly Dictionary<MeshObjectId, MeshObject> _objects = [];
    int _nextObjectId;
    long _mutationSerial;
    bool _disposed;

    public int ObjectCount => _objects.Count;

    // Session-local commit counter. Advances exactly once after each successful atomic operation.
    public long MutationSerial => _mutationSerial;

    // Raised once after authoritative state is committed. Payloads contain IDs and kinds only.
    internal event Action<DocumentChangeSet>? Changed;

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

    public bool TryGetObject(MeshObjectId id, out MeshObject meshObject)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _objects.TryGetValue(id, out meshObject!);
    }

    // After publication the document owns the mesh; on failure before insertion the caller retains it.
    internal MeshObject Add(string name, Transform3D transform, GDMesh mesh)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(name);

        var id = new MeshObjectId(_nextObjectId);
        var meshObject = new MeshObject(id, name, transform, mesh);
        _objects.Add(id, meshObject);
        _nextObjectId = checked(_nextObjectId + 1);
        Publish(new DocumentObjectChange(id, DocumentChangeKind.Added));
        return meshObject;
    }

    // Re-inserts the exact record retained by an undoable create command. Object IDs are never
    // reused, so restoration cannot silently replace a different object.
    internal bool Restore(MeshObject meshObject)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(meshObject);
        if (_objects.ContainsKey(meshObject.Id))
            return false;

        _objects.Add(meshObject.Id, meshObject);
        _nextObjectId = Math.Max(_nextObjectId, checked(meshObject.Id.Value + 1));
        Publish(new DocumentObjectChange(meshObject.Id, DocumentChangeKind.Added));
        return true;
    }

    // Removes the object from the document without disposing its mesh. The returned record's mesh is
    // owned by the caller (e.g. an undo command) until they dispose it or re-insert it.
    internal MeshObject? Remove(MeshObjectId id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_objects.Remove(id, out MeshObject? meshObject))
            return null;

        Publish(new DocumentObjectChange(id, DocumentChangeKind.Removed));
        return meshObject;
    }

    // Compare-and-replace a single object record in place. Validation completes before mutation, so
    // a rejected call leaves the live collection untouched without allocating a candidate map.
    internal bool ReplaceObject(MeshObject expected, MeshObject replacement)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsValidReplacement(expected, replacement))
            return false;

        _objects[expected.Id] = replacement;
        Publish(new DocumentObjectChange(expected.Id, DocumentChangeKind.TransformChanged));
        return true;
    }

    // Atomically validates and replaces a group of object records. All authoritative values are
    // installed before notifications are published, so consumers never observe a partially moved
    // selection. Every change set from the group shares one mutation serial.
    internal bool ReplaceObjects(
        IReadOnlyList<MeshObject> expected,
        IReadOnlyList<MeshObject> replacements
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(replacements);
        if (expected.Count == 0 || expected.Count != replacements.Count)
            return false;

        var ids = new HashSet<MeshObjectId>();
        for (int index = 0; index < expected.Count; index++)
        {
            if (!ids.Add(expected[index].Id) || !IsValidReplacement(expected[index], replacements[index]))
                return false;
        }

        var changes = new DocumentObjectChange[expected.Count];
        for (int index = 0; index < expected.Count; index++)
        {
            MeshObject replacement = replacements[index];
            _objects[replacement.Id] = replacement;
            changes[index] = new DocumentObjectChange(
                replacement.Id,
                DocumentChangeKind.TransformChanged
            );
        }

        Publish(changes);
        return true;
    }

    private bool IsValidReplacement(MeshObject expected, MeshObject replacement) =>
        expected is not null
        && replacement is not null
        && replacement.Name is not null
        && expected.Id == replacement.Id
        && ReferenceEquals(expected.Mesh, replacement.Mesh)
        && _objects.TryGetValue(expected.Id, out MeshObject? current)
        && ReferenceEquals(current, expected);

    // Committed geometry announcement after topology has already changed through the document.
    // Not a free-form "pretend geometry changed" hook for arbitrary callers.
    internal bool CommitGeometryChange(MeshObjectId id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_objects.ContainsKey(id))
            return false;

        Publish(new DocumentObjectChange(id, DocumentChangeKind.GeometryChanged));
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
        Changed = null;
    }

    private void Publish(DocumentObjectChange change)
    {
        Publish([change]);
    }

    private void Publish(IReadOnlyList<DocumentObjectChange> changes)
    {
        long serial = checked(_mutationSerial + 1);
        _mutationSerial = serial;
        foreach (DocumentObjectChange change in changes)
            Changed?.Invoke(new DocumentChangeSet(serial, change));
    }
}

using System;
using System.Collections.Generic;
using Godot;

// Transient transform overrides used while a tool drags. Previews never reach the document: the
// document keeps the authoritative transform and a preview only changes what services display until
// it is cleared. Changes are announced through <see cref="TransformChanged"/> so every scene
// service can recompute an object's effective transform through the same path a committed change
// uses.
public sealed class PreviewState : ISceneService
{
    private readonly Dictionary<MeshObjectId, Transform3D> _transforms = [];
    private bool _disposed;

    // Raised for a single object whose preview was set, changed, or explicitly cleared. Lifecycle
    // cleanup (document replacement, object removal, disposal) is silent because the services being
    // notified are discarding their own state for those objects at the same time.
    public event Action<MeshObjectId>? TransformChanged;

    public bool TryGetTransform(MeshObjectId id, out Transform3D transform)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transforms.TryGetValue(id, out transform);
    }

    public void SetTransform(MeshObjectId id, Transform3D transform)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_transforms.TryGetValue(id, out Transform3D existing) && existing == transform)
            return;

        _transforms[id] = transform;
        TransformChanged?.Invoke(id);
    }

    public void ClearTransform(MeshObjectId id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_transforms.Remove(id))
            return;

        TransformChanged?.Invoke(id);
    }

    public void ClearAll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_transforms.Count == 0)
            return;

        var previewed = new MeshObjectId[_transforms.Count];
        _transforms.Keys.CopyTo(previewed, 0);
        _transforms.Clear();

        foreach (MeshObjectId id in previewed)
            TransformChanged?.Invoke(id);
    }

    public void SetDocument(Document document)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(document);
        _transforms.Clear();
    }

    public void OnObjectAdded(MeshObject meshObject)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(meshObject);
    }

    public void OnObjectRemoved(MeshObject meshObject)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(meshObject);
        _transforms.Remove(meshObject.Id);
    }

    public void OnObjectGeometryChanged(MeshObjectId id) =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    public void OnObjectTransformChanged(MeshObjectId id) =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    public void Flush() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _transforms.Clear();
        TransformChanged = null;
    }
}

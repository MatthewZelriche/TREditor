using System;
using System.Collections.Generic;
using Godot;

// Transient transform and geometry overrides used by tools. Previews never reach the document: the
// document remains authoritative while previews only change what services display until cleared.
public sealed class PreviewState : ISceneService
{
    private readonly Dictionary<MeshObjectId, Transform3D> _transforms = [];
    private GeometryPreview? _geometry;
    private bool _disposed;

    // Raised for a single object whose preview was set, changed, or explicitly cleared. Lifecycle
    // cleanup (document replacement, object removal, disposal) is silent because the services being
    // notified are discarding their own state for those objects at the same time.
    public event Action<MeshObjectId>? TransformChanged;

    public event Action? PreviewGeometryChanged;

    public GeometryPreview? Geometry
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _geometry;
        }
    }

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
        if (_transforms.Count != 0)
        {
            var previewed = new MeshObjectId[_transforms.Count];
            _transforms.Keys.CopyTo(previewed, 0);
            _transforms.Clear();

            foreach (MeshObjectId id in previewed)
                TransformChanged?.Invoke(id);
        }

        ClearGeometry();
    }

    // Ownership of the mesh transfers to preview state. Replacing or clearing the preview disposes
    // the old mesh, keeping high-frequency geometry feedback from leaking native mesh resources.
    public void SetGeometry(GDMesh mesh, Transform3D transform)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(mesh);

        GeometryPreview? previous = _geometry;
        _geometry = new GeometryPreview(mesh, transform);
        PreviewGeometryChanged?.Invoke();
        previous?.Mesh.Dispose();
    }

    public void ClearGeometry(bool silent = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_geometry is not GeometryPreview previous)
            return;

        _geometry = null;
        if (!silent)
            PreviewGeometryChanged?.Invoke();
        previous.Mesh.Dispose();
    }

    public void SetDocument(Document document)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(document);
        _transforms.Clear();
        ClearGeometry(silent: true);
    }

    public void OnDocumentChanged(DocumentChangeSet changeSet)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(changeSet);

        if (changeSet.Change.Kind == DocumentChangeKind.Removed)
            _transforms.Remove(changeSet.Change.ObjectId);
    }

    public void OnPreviewTransformChanged(MeshObjectId id) =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    public void OnPreviewGeometryChanged() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Flush() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed)
            return;

        _transforms.Clear();
        ClearGeometry(silent: true);
        _disposed = true;
        TransformChanged = null;
        PreviewGeometryChanged = null;
    }
}

public sealed record GeometryPreview(GDMesh Mesh, Transform3D Transform);

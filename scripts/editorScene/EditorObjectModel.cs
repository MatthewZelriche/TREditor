using System;
using Godot;
using TREditorSharp;

/// <summary>
/// Authoritative editor object state: identity, display name, editor-root-local transform,
/// owned mesh geometry, and session-local revision counters. Godot-runtime-independent aside
/// from the managed <see cref="Transform3D"/> value type.
/// </summary>
public sealed class EditorObjectModel : IDisposable
{
    private ulong _geometryRevision;
    private ulong _appearanceRevision;
    private ulong _transformRevision;
    private readonly SpatialMesh _mesh;
    private Transform3D _localTransform;
    private bool _disposed;

    public EditorObjectModel(
        EditorObjectId id,
        string name,
        Transform3D localTransform,
        SpatialMesh mesh
    )
    {
        if (id.Value == Guid.Empty)
            throw new ArgumentException("Object ID must not be empty.", nameof(id));
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(mesh);
        if (!IsFinite(localTransform))
            throw new ArgumentException("Transform must be finite.", nameof(localTransform));

        Id = id;
        Name = name;
        _localTransform = localTransform;
        _mesh = mesh;
    }

    public EditorObjectId Id { get; }

    public string Name { get; }

    public Transform3D LocalTransform
    {
        get
        {
            ThrowIfDisposed();
            return _localTransform;
        }
    }

    public SpatialMesh Mesh
    {
        get
        {
            ThrowIfDisposed();
            return _mesh;
        }
    }

    public ulong GeometryRevision
    {
        get
        {
            ThrowIfDisposed();
            return _geometryRevision;
        }
    }

    public ulong AppearanceRevision
    {
        get
        {
            ThrowIfDisposed();
            return _appearanceRevision;
        }
    }

    public ulong TransformRevision
    {
        get
        {
            ThrowIfDisposed();
            return _transformRevision;
        }
    }

    public void SetLocalTransform(Transform3D transform)
    {
        ThrowIfDisposed();
        if (!IsFinite(transform))
            throw new ArgumentException("Transform must be finite.", nameof(transform));

        _localTransform = transform;
        _transformRevision++;
    }

    public void MarkGeometryChanged()
    {
        ThrowIfDisposed();
        _geometryRevision++;
    }

    public void MarkAppearanceChanged()
    {
        ThrowIfDisposed();
        _appearanceRevision++;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _mesh.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private static bool IsFinite(Transform3D transform) =>
        IsFinite(transform.Basis.Column0)
        && IsFinite(transform.Basis.Column1)
        && IsFinite(transform.Basis.Column2)
        && IsFinite(transform.Origin);

    private static bool IsFinite(Vector3 vector) =>
        float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z);
}

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using TREditorSharp;

/// <summary>
/// In-memory snapshot of a persistent editor document: the mesh objects in the scene plus the
/// session material-slot table needed to resolve their per-face textures. This is a plain data
/// carrier with no engine dependencies beyond value types, so it can be serialized and unit-tested
/// in isolation from the live scene tree.
/// </summary>
public sealed class EditorDocument
{
    public EditorDocument(
        IReadOnlyList<EditorDocumentObject> objects,
        IReadOnlyList<MaterialSlotMapping> materialMappings
    )
    {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(materialMappings);

        Objects = objects;
        MaterialMappings = materialMappings;
    }

    public IReadOnlyList<EditorDocumentObject> Objects { get; }

    public IReadOnlyList<MaterialSlotMapping> MaterialMappings { get; }
}

/// <summary>
/// One persisted mesh object: stable identity, display name, world transform, and geometry.
/// Ownership of <see cref="Mesh"/> belongs to the container that produced this object.
/// </summary>
public sealed class EditorDocumentObject
{
    public EditorDocumentObject(
        EditorObjectId id,
        string name,
        Transform3D transform,
        SpatialMesh mesh
    )
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(mesh);

        Id = id;
        Name = name;
        Transform = transform;
        Mesh = mesh;
    }

    public EditorObjectId Id { get; }

    public string Name { get; }

    public Transform3D Transform { get; }

    public SpatialMesh Mesh { get; }
}

/// <summary>
/// Owns every mesh parsed from a document until ownership is transferred to the live scene.
/// Disposing a partially consumed result releases all meshes that were not transferred.
/// </summary>
public sealed class LoadedEditorDocument : IDisposable
{
    private readonly HashSet<SpatialMesh> _ownedMeshes;
    private bool _disposed;

    internal LoadedEditorDocument(EditorDocument document)
    {
        Document = document;
        _ownedMeshes = new HashSet<SpatialMesh>(
            document.Objects.Select(documentObject => documentObject.Mesh),
            ReferenceEqualityComparer.Instance
        );
    }

    public EditorDocument Document { get; }

    public IReadOnlyList<EditorDocumentObject> Objects => Document.Objects;

    public IReadOnlyList<MaterialSlotMapping> MaterialMappings => Document.MaterialMappings;

    public void TransferMeshOwnership(EditorDocumentObject documentObject)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(documentObject);
        if (!_ownedMeshes.Remove(documentObject.Mesh))
            throw new InvalidOperationException("The document no longer owns this object's mesh.");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (SpatialMesh mesh in _ownedMeshes)
            mesh.Dispose();
        _ownedMeshes.Clear();
    }
}

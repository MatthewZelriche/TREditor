using System;
using System.Collections.Generic;
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
/// One persisted mesh object: stable identity, display name, world transform, and geometry. The
/// object owns its <see cref="Mesh"/> only for the duration of a load; on save the mesh is borrowed
/// from the live scene and must not be disposed by the document layer.
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

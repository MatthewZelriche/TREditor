using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using TREditorSharp;
using FileAccess = Godot.FileAccess;

/// <summary>
/// Coordinates persistent document operations (new/open/save) over the live editor session. Saving
/// snapshots the committed scene into an <see cref="EditorDocument"/>; loading resets the session
/// and rebuilds the scene and material table from a document. File bytes are bridged through Godot
/// <see cref="FileAccess"/> so user://, res://, and absolute OS paths all resolve.
/// </summary>
public sealed class DocumentService
{
    private readonly EditorSceneService _scene;
    private readonly TextureMaterialLibrary _textureMaterials;
    private readonly SelectionService _selection;
    private readonly CommandService _commands;
    private readonly EditorDocumentSerializer _serializer = new();

    public DocumentService(
        EditorSceneService scene,
        TextureMaterialLibrary textureMaterials,
        SelectionService selection,
        CommandService commands
    )
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(textureMaterials);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(commands);

        _scene = scene;
        _textureMaterials = textureMaterials;
        _selection = selection;
        _commands = commands;
    }

    /// <summary>Discards the current document and starts from an empty scene.</summary>
    public void New() => Reset();

    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        EditorDocument document = CaptureDocument();

        using var buffer = new MemoryStream();
        _serializer.Write(document, buffer);

        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
            throw new IOException($"Unable to save document '{path}': {FileAccess.GetOpenError()}.");

        file.StoreBuffer(buffer.ToArray());
    }

    public void Open(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        EditorDocument document;
        using (var buffer = ReadAllBytes(path))
        {
            document = _serializer.Read(buffer);
        }

        Reset();

        foreach (MaterialSlotMapping mapping in document.MaterialMappings)
            _textureMaterials.RegisterSlot(mapping.Slot, mapping.AssetId);

        foreach (EditorDocumentObject documentObject in document.Objects)
            _scene.CreateMeshObject(
                documentObject.Id,
                documentObject.Mesh,
                documentObject.Name,
                documentObject.Transform
            );
    }

    private EditorDocument CaptureDocument()
    {
        List<EditorDocumentObject> objects = [];
        foreach ((EditorObjectId id, TRMeshGD meshNode) in _scene.EnumerateMeshObjects())
        {
            objects.Add(
                new EditorDocumentObject(
                    id,
                    meshNode.Name.ToString(),
                    meshNode.Transform,
                    meshNode.SourceMesh
                )
            );
        }

        return new EditorDocument(objects, _textureMaterials.GetMappings());
    }

    // The reset ordering matters: clear undo history first so commands release the topology patches
    // and reserved handles that point at meshes we are about to free.
    private void Reset()
    {
        _commands.ClearHistory();
        _selection.Apply(SelectionSnapshot.Empty);
        _scene.ClearAll();
        _textureMaterials.Clear();
    }

    private static MemoryStream ReadAllBytes(string path)
    {
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
            throw new IOException($"Unable to open document '{path}': {FileAccess.GetOpenError()}.");

        byte[] data = file.GetBuffer((long)file.GetLength());
        return new MemoryStream(data, writable: false);
    }
}

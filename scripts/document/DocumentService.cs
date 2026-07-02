using System;
using System.IO;

/// <summary>
/// Coordinates persistent document operations (new/open/save) over the live editor session. Saving
/// snapshots the committed scene into an <see cref="EditorDocument"/>; loading resets the session
/// and rebuilds the scene and material table from a document. File bytes are bridged through Godot
/// <see cref="FileAccess"/> so user://, res://, and absolute OS paths all resolve.
/// </summary>
public sealed class DocumentService
{
    private readonly IEditorDocumentSession _session;
    private readonly EditorDocumentSerializer _serializer;
    private readonly IDocumentFileSystem _fileSystem;

    public DocumentService(
        EditorSceneModel model,
        EditorObjectLifecycle lifecycle,
        TextureMaterialLibrary textureMaterials,
        SelectionService selection,
        CommandService commands,
        Action cancelPreview
    )
    {
        _session = new EditorDocumentSession(
            model,
            lifecycle,
            textureMaterials,
            selection,
            commands,
            cancelPreview
        );
        _serializer = new EditorDocumentSerializer();
        _fileSystem = new GodotDocumentFileSystem();
    }

    internal DocumentService(IEditorDocumentSession session, EditorDocumentSerializer serializer)
        : this(session, serializer, new GodotDocumentFileSystem()) { }

    internal DocumentService(
        IEditorDocumentSession session,
        EditorDocumentSerializer serializer,
        IDocumentFileSystem fileSystem
    )
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(fileSystem);
        _session = session;
        _serializer = serializer;
        _fileSystem = fileSystem;
    }

    /// <summary>Discards the current document and starts from an empty scene.</summary>
    public void New() => _session.Reset();

    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var buffer = new MemoryStream();
        Save(buffer);
        WriteAtomically(path, buffer.ToArray());
    }

    public void Open(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        using MemoryStream buffer = new(_fileSystem.ReadAllBytes(path), writable: false);
        Open(buffer);
    }

    internal void Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        _session.CancelPreview();
        _serializer.Write(_session.CaptureDocument(), destination);
    }

    internal void Open(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using LoadedEditorDocument loadedDocument = _serializer.Read(source);

        _session.Reset();
        try
        {
            _session.Apply(loadedDocument);
        }
        catch
        {
            // A validated document should not fail here, but never leave a partially replaced
            // session if a runtime scene operation does.
            _session.Reset();
            throw;
        }
    }

    private void WriteAtomically(string path, byte[] data)
    {
        string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            _fileSystem.WriteAllBytes(temporaryPath, data);
            _fileSystem.Replace(temporaryPath, path);
        }
        finally
        {
            if (_fileSystem.Exists(temporaryPath))
                _fileSystem.Remove(temporaryPath);
        }
    }
}

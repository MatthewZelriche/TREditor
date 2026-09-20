using System;
using Godot;

// Owns a newly-built mesh until Execute transfers it to the document. When undone, ownership moves
// back to the command so clearing or evicting history releases the native resource.
public sealed class CreatePrimitiveCommand : IEditorCommand, IDisposable
{
    private readonly string _name;
    private readonly Transform3D _transform;

    // TODO: I don't like the way we have an initialMesh and a created mesh.
    // This split exists because only the document can provide an ID, creating a MeshObject
    // from a GDMesh. But we need to store the initial mesh until the first call to Execute...
    private GDMesh? _initialMesh;
    private MeshObject? _created;

    private bool _isInDocument;
    private bool _disposed;

    public CreatePrimitiveCommand(string name, Transform3D transform, GDMesh mesh)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(mesh);
        _name = name;
        _transform = transform;
        _initialMesh = mesh;
    }

    public string DisplayName => "Create Primitive";

    public void Execute(Document document)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(document);
        if (_isInDocument)
            throw new NonFatalCommandError("The primitive is already in the document.");

        if (_created is null)
        {
            GDMesh mesh = _initialMesh
                ?? throw new InvalidOperationException("The primitive mesh is unavailable.");
            _created = document.Add(_name, _transform, mesh);
            _initialMesh = null;
        }
        else if (!document.Restore(_created))
        {
            throw new NonFatalCommandError(
                $"Object {_created.Id.Value} cannot be restored because its ID is already in use."
            );
        }

        _isInDocument = true;
    }

    public void Undo(Document document)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(document);
        if (!_isInDocument || _created is null)
            throw new NonFatalCommandError("The primitive is not in the document.");

        MeshObject? removed = document.Remove(_created.Id);
        if (!ReferenceEquals(removed, _created))
        {
            if (removed is not null)
                document.Restore(removed);
            throw new NonFatalCommandError(
                $"Object {_created.Id.Value} is missing or stale and cannot be removed."
            );
        }

        _isInDocument = false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (!_isInDocument)
            (_created?.Mesh ?? _initialMesh)?.Dispose();

        _initialMesh = null;
        _created = null;
    }
}

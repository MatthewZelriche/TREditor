using System;
using System.Collections.Generic;
using Godot;

// Clones one or more mesh objects into the document and selects the copies. Owns the cloned meshes
// until Execute transfers them; when undone, ownership returns so history eviction can dispose them.
internal sealed class DuplicateObjectsCommand : IEditorCommand, IDisposable
{
    private readonly Pending[] _pending;
    private readonly IEditorSelection _selection;
    private MeshObject[]? _created;
    private IEditorCommand? _selectionCommand;
    private bool _isInDocument;
    private bool _disposed;

    internal DuplicateObjectsCommand(
        IReadOnlyList<MeshObject> sources,
        IEditorSelection selection
    )
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(selection);
        if (sources.Count == 0)
            throw new ArgumentException("Select at least one object to duplicate.", nameof(sources));

        _selection = selection;
        _pending = new Pending[sources.Count];
        int created = 0;
        try
        {
            for (; created < sources.Count; created++)
            {
                MeshObject source = sources[created]
                    ?? throw new ArgumentException("Sources cannot contain null.", nameof(sources));
                _pending[created] = new Pending(
                    source.Name,
                    source.Transform,
                    new GDMesh(source.Mesh.SpatialMesh.Clone())
                );
            }
        }
        catch
        {
            for (int index = 0; index < created; index++)
                _pending[index].Mesh.Dispose();
            throw;
        }
    }

    public string DisplayName =>
        _pending.Length == 1 ? "Duplicate Object" : "Duplicate Objects";

    public void Execute(Document document)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(document);
        if (_isInDocument)
            throw new NonFatalCommandError("The duplicates are already in the document.");

        if (_created is null)
        {
            var created = new MeshObject[_pending.Length];
            int added = 0;
            try
            {
                for (; added < _pending.Length; added++)
                {
                    Pending pending = _pending[added];
                    created[added] = document.Add(pending.Name, pending.Transform, pending.Mesh);
                }
            }
            catch
            {
                for (int index = 0; index < added; index++)
                    document.Remove(created[index].Id)?.Mesh.Dispose();
                throw;
            }

            _created = created;
        }
        else
        {
            for (int index = 0; index < _created.Length; index++)
            {
                if (document.Restore(_created[index]))
                    continue;

                for (int undo = 0; undo < index; undo++)
                    document.Remove(_created[undo].Id);
                throw new NonFatalCommandError(
                    $"Object {_created[index].Id.Value} cannot be restored because its ID is already in use."
                );
            }
        }

        _isInDocument = true;
        var ids = new MeshObjectId[_created.Length];
        for (int index = 0; index < _created.Length; index++)
            ids[index] = _created[index].Id;
        _selectionCommand = _selection.CreateCommand(SelectionValue.ForSelections(ids, []));
        _selectionCommand.Execute(document);
    }

    public void Undo(Document document)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(document);
        if (!_isInDocument || _created is null)
            throw new NonFatalCommandError("The duplicates are not in the document.");

        for (int index = 0; index < _created.Length; index++)
        {
            MeshObject? removed = document.Remove(_created[index].Id);
            if (!ReferenceEquals(removed, _created[index]))
            {
                for (int restore = 0; restore < index; restore++)
                    document.Restore(_created[restore]);
                if (removed is not null)
                    document.Restore(removed);
                throw new NonFatalCommandError(
                    $"Object {_created[index].Id.Value} is missing or stale and cannot be removed."
                );
            }
        }

        _isInDocument = false;
        _selectionCommand!.Undo(document);
        _selectionCommand = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (!_isInDocument)
        {
            if (_created is not null)
            {
                foreach (MeshObject meshObject in _created)
                    meshObject.Mesh.Dispose();
            }
            else
            {
                foreach (Pending pending in _pending)
                    pending.Mesh.Dispose();
            }
        }

        _created = null;
        _selectionCommand = null;
    }

    private readonly record struct Pending(string Name, Transform3D Transform, GDMesh Mesh);
}

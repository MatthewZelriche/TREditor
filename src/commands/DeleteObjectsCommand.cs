using System;

// Removes one or more selected mesh objects from the document. Removed records are retained for
// undo and disposed when the command leaves history while the objects are still out of the document.
internal sealed class DeleteObjectsCommand : IEditorCommand, IDisposable
{
    private const string StaleMessage =
        "One or more selected objects are missing or stale and cannot be deleted.";

    private readonly MeshObject[] _objects;
    private readonly IEditorSelection _selection;
    private IEditorCommand? _selectionCommand;
    private bool _removed;
    private bool _disposed;

    internal DeleteObjectsCommand(
        IEditorDocumentReader document,
        SelectionValue selection,
        IEditorSelection editorSelection
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(editorSelection);
        if (selection.ObjectIds.Count == 0)
            throw new ArgumentException("Select at least one object to delete.");

        _objects = new MeshObject[selection.ObjectIds.Count];
        int index = 0;
        foreach (MeshObjectId id in selection.ObjectIds)
        {
            if (!document.TryGetObject(id, out MeshObject meshObject))
                throw new ArgumentException(StaleMessage);

            _objects[index++] = meshObject;
        }

        _selection = editorSelection;
    }

    public string DisplayName => _objects.Length == 1 ? "Delete Object" : "Delete Objects";

    public void Execute(Document document)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(document);
        if (_removed)
            throw new NonFatalCommandError(StaleMessage);

        for (int index = 0; index < _objects.Length; index++)
        {
            MeshObject? removed = document.Remove(_objects[index].Id);
            if (!ReferenceEquals(removed, _objects[index]))
            {
                for (int restore = 0; restore < index; restore++)
                    document.Restore(_objects[restore]);
                if (removed is not null)
                    document.Restore(removed);
                throw new NonFatalCommandError(StaleMessage);
            }
        }

        _removed = true;
        if (_selection.Current.IsEmpty)
            return;

        _selectionCommand = _selection.CreateCommand(SelectionValue.Empty);
        _selectionCommand.Execute(document);
    }

    public void Undo(Document document)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(document);
        if (!_removed)
            throw new NonFatalCommandError(StaleMessage);

        for (int index = 0; index < _objects.Length; index++)
        {
            if (!document.Restore(_objects[index]))
            {
                for (int undo = 0; undo < index; undo++)
                    document.Remove(_objects[undo].Id);
                throw new NonFatalCommandError(StaleMessage);
            }
        }

        _removed = false;
        _selectionCommand?.Undo(document);
        _selectionCommand = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_removed)
        {
            foreach (MeshObject meshObject in _objects)
                meshObject.Mesh?.Dispose();
        }
    }

}

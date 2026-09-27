using System;

// A user-driven change to the transient selection, recorded in the shared history so it can be undone
// alongside document edits. Selection is editor state rather than document state, so this command keeps
// the application-owned SelectionState it was built with and ignores the document it is handed. Storing
// the exact before and after values keeps undo and redo deterministic regardless of intervening selections.
public sealed class SelectionCommand : IEditorCommand
{
    private readonly SelectionState _selection;
    private readonly SelectionValue _before;
    private readonly SelectionValue _after;

    public SelectionCommand(SelectionState selection, MeshObjectId? before, MeshObjectId? after)
        : this(selection, ToSelection(before), ToSelection(after)) { }

    public SelectionCommand(
        SelectionState selection,
        SelectionValue before,
        SelectionValue after
    )
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (before == after)
        {
            throw new ArgumentException(
                "A selection command must change the selection.",
                nameof(after)
            );
        }

        _selection = selection;
        _before = before;
        _after = after;
    }

    public string DisplayName => _after.IsEmpty
        ? "Clear Selection"
        : _after.Component is not null
            ? "Select Component"
            : "Select Object";

    public void Execute(Document document)
    {
        _selection.Apply(_after);
    }

    public void Undo(Document document)
    {
        _selection.Apply(_before);
    }

    private static SelectionValue ToSelection(MeshObjectId? id) =>
        id is MeshObjectId objectId
            ? SelectionValue.ForObject(objectId)
            : SelectionValue.Empty;
}

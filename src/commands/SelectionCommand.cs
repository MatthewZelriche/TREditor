using System;

// A user-driven change to the transient selection, recorded in the shared history so it can be undone
// alongside document edits. Selection is editor state rather than document state, so this command keeps
// the application-owned SelectionState it was built with and ignores the document it is handed. Storing
// the exact before and after values keeps undo and redo deterministic regardless of intervening selections.
public sealed class SelectionCommand : IEditorCommand
{
    private readonly SelectionState _selection;
    // TODO: This will change in the future as we expand on the complexity of what SelectionState is capable
    // of holding - eg multiple selections, different type of selections (component vs object), etc.
    private readonly MeshObjectId? _before;
    private readonly MeshObjectId? _after;

    public SelectionCommand(SelectionState selection, MeshObjectId? before, MeshObjectId? after)
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

    public string DisplayName => _after is null ? "Clear Selection" : "Select Object";

    public void Execute(Document document)
    {
        _selection.Apply(_after);
    }

    public void Undo(Document document)
    {
        _selection.Apply(_before);
    }
}

using System;
using TRMesh.Mesh;

// Shared execute/undo for commands that record one MeshPatch on a single object,
// then apply a follow-up selection command.
internal abstract class MeshPatchCommandBase : IEditorCommand
{
    private readonly IEditorSelection _selection;
    private MeshPatch? _undo;
    private IEditorCommand? _selectionCommand;

    protected MeshPatchCommandBase(IEditorSelection selection)
    {
        _selection = selection;
    }

    public abstract string DisplayName { get; }

    protected abstract MeshObject Object { get; }

    protected abstract string StaleMessage { get; }

    protected abstract MeshPatch RecordEdit();

    protected abstract SelectionValue SelectionAfterEdit();

    public void Execute(Document document)
    {
        if (_undo is not null)
            throw new NonFatalCommandError(StaleMessage);

        MeshPatch? patch = null;
        try
        {
            if (!document.EditGeometry([Object], () => patch = RecordEdit()))
                throw new NonFatalCommandError(StaleMessage);
        }
        catch (ArgumentException exception)
        {
            throw new NonFatalCommandError(FirstLine(exception.Message));
        }

        _undo = patch;
        _selectionCommand = _selection.CreateCommand(SelectionAfterEdit());
        _selectionCommand.Execute(document);
    }

    public void Undo(Document document)
    {
        MeshPatch patch = _undo ?? throw new NonFatalCommandError(StaleMessage);
        if (!document.EditGeometry([Object], () => Object.Mesh.SpatialMesh.Revert(patch)))
            throw new NonFatalCommandError(StaleMessage);

        _undo = null;
        _selectionCommand!.Undo(document);
        _selectionCommand = null;
    }

    internal static string FirstLine(string message)
    {
        int lineBreak = message.IndexOfAny(['\r', '\n']);
        return lineBreak < 0 ? message : message[..lineBreak];
    }

    internal static EditorToolAction ActionOrReason(Func<IEditorCommand> create)
    {
        try
        {
            return new EditorToolAction(create(), null);
        }
        catch (ArgumentException exception)
        {
            return new EditorToolAction(null, FirstLine(exception.Message));
        }
    }
}

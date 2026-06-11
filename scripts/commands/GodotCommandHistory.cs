using Godot;

internal sealed class GodotCommandHistory : ICommandHistory
{
    private readonly UndoRedo _undoRedo = new();

    public bool CanUndo => _undoRedo.HasUndo();
    public bool CanRedo => _undoRedo.HasRedo();

    public void Execute(EditorCommand command)
    {
        _undoRedo.CreateAction(command.Name);
        _undoRedo.AddDoMethod(new Callable(command, nameof(EditorCommand.ExecuteDo)));
        _undoRedo.AddUndoMethod(new Callable(command, nameof(EditorCommand.ExecuteUndo)));
        _undoRedo.CommitAction();
    }

    public void Undo() => _undoRedo.Undo();

    public void Redo() => _undoRedo.Redo();

    public void Clear() => _undoRedo.ClearHistory();

    public void Dispose()
    {
        _undoRedo.ClearHistory();
        _undoRedo.Dispose();
    }
}

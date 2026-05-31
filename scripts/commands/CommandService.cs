using System;
using Godot;

public sealed partial class CommandService : GodotObject
{
    [Signal]
    public delegate void CommandHistoryChangedEventHandler();

    private readonly UndoRedo _undoRedo = new();

    public bool CanUndo => _undoRedo.HasUndo();
    public bool CanRedo => _undoRedo.HasRedo();

    public void Execute(IEditorCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        _undoRedo.CreateAction(command.Name);
        _undoRedo.AddDoMethod(Callable.From(command.Do));
        _undoRedo.AddUndoMethod(Callable.From(command.Undo));
        _undoRedo.CommitAction();
        EmitCommandHistoryChanged();
    }

    public void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        _undoRedo.Undo();
        EmitCommandHistoryChanged();
    }

    public void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        _undoRedo.Redo();
        EmitCommandHistoryChanged();
    }

    private void EmitCommandHistoryChanged()
    {
        EmitSignal(SignalName.CommandHistoryChanged);
    }
}

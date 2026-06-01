using System;
using System.Collections.Generic;
using Godot;

public sealed partial class CommandService : GodotObject
{
    [Signal]
    public delegate void CommandHistoryChangedEventHandler();

    private readonly UndoRedo _undoRedo = new();
    private readonly List<EditorCommand> _commands = [];

    public bool CanUndo => _undoRedo.HasUndo();
    public bool CanRedo => _undoRedo.HasRedo();

    public void Execute(EditorCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        _commands.Add(command);

        _undoRedo.CreateAction(command.Name);
        _undoRedo.AddDoMethod(new Callable(command, nameof(EditorCommand.Do)));
        _undoRedo.AddUndoMethod(new Callable(command, nameof(EditorCommand.Undo)));
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

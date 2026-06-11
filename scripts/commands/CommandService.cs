using System;
using System.Collections.Generic;

public sealed class CommandService : IDisposable
{
    private readonly ICommandHistory _history;
    private readonly List<EditorCommand> _commands = [];
    private readonly EditorCommandContext _context;
    private bool _disposed;

    public CommandService(EditorCommandContext context)
        : this(context, new GodotCommandHistory()) { }

    internal CommandService(EditorCommandContext context, ICommandHistory history)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(history);

        _context = context;
        _history = history;
    }

    public event Action CommandHistoryChanged;

    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;

    public void Execute(EditorCommand command)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(command);

        _commands.Add(command);
        command.SetContext(_context);
        _history.Execute(command);
        EmitCommandHistoryChanged();
    }

    public void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        _history.Undo();
        EmitCommandHistoryChanged();
    }

    public void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        _history.Redo();
        EmitCommandHistoryChanged();
    }

    /// <summary>
    /// Clear undo/redo and release every command-owned resource. Future bounded-history eviction
    /// or redo-branch pruning must call <see cref="EditorCommand.ReleaseResources"/> for each
    /// discarded command.
    /// </summary>
    public void ClearHistory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _history.Clear();
        ReleaseCommands();
        EmitCommandHistoryChanged();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _history.Clear();
        ReleaseCommands();
        _history.Dispose();
        _disposed = true;
    }

    private void ReleaseCommands()
    {
        foreach (EditorCommand command in _commands)
            command.ReleaseResources();
        _commands.Clear();
    }

    private void EmitCommandHistoryChanged()
    {
        CommandHistoryChanged?.Invoke();
    }
}

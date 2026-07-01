using System;
using System.Collections.Generic;

public sealed class CommandService : IDisposable
{
    public const int HistoryCapacity = 128;

    private readonly List<EditorCommand> _undo = [];
    private readonly List<EditorCommand> _redo = [];
    private readonly EditorCommandContext _context;
    private bool _disposed;

    public CommandService(EditorCommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public event Action CommandHistoryChanged;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Execute(EditorCommand command)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(command);

        if (!command.ExecuteInitial(_context))
            return;

        DisposeCommands(_redo);
        _redo.Clear();
        _undo.Add(command);
        if (_undo.Count > HistoryCapacity)
        {
            _undo[0].Dispose();
            _undo.RemoveAt(0);
        }

        EmitCommandHistoryChanged();
    }

    public void Undo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!CanUndo)
            return;

        EditorCommand command = _undo[^1];
        command.ExecuteUndo();
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(command);
        EmitCommandHistoryChanged();
    }

    public void Redo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!CanRedo)
            return;

        EditorCommand command = _redo[^1];
        command.ExecuteRedo();
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(command);
        EmitCommandHistoryChanged();
    }

    public void ClearHistory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_undo.Count == 0 && _redo.Count == 0)
            return;

        DisposeCommands(_undo);
        DisposeCommands(_redo);
        _undo.Clear();
        _redo.Clear();
        EmitCommandHistoryChanged();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        DisposeCommands(_undo);
        DisposeCommands(_redo);
        _undo.Clear();
        _redo.Clear();
        _disposed = true;
    }

    private static void DisposeCommands(IEnumerable<EditorCommand> commands)
    {
        foreach (EditorCommand command in commands)
            command.Dispose();
    }

    private void EmitCommandHistoryChanged()
    {
        CommandHistoryChanged?.Invoke();
    }
}

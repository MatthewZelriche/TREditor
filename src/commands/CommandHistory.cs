using System;
using System.Collections.Generic;

// Bounded, in-memory undo/redo history. Commands that execute successfully are owned by the history and
// are disposed when they are evicted, invalidated, cleared, or when the history itself is disposed.
public sealed class CommandHistory : IDisposable
{
    public const int DefaultCapacity = 100;

    // The last entry of each list is the top of that stack.
    private readonly List<IEditorCommand> _undo = [];
    private readonly List<IEditorCommand> _redo = [];
    private readonly int _capacity;
    private bool _operationInProgress;
    private bool _disposed;

    public CommandHistory()
        : this(DefaultCapacity)
    {
    }

    public CommandHistory(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    // Raised after a successful execute, undo, or redo, and after a clear that discarded commands.
    public event Action? Changed;

    public int Capacity => _capacity;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public string? UndoDisplayName => CanUndo ? _undo[^1].DisplayName : null;

    public string? RedoDisplayName => CanRedo ? _redo[^1].DisplayName : null;

    // Executes command and, on success, takes ownership of it and invalidates the redo stack. A command
    // that fails to execute stays owned by the caller and leaves the history untouched.
    public void Execute(IEditorCommand command)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(command);

        BeginOperation();
        try
        {
            command.Execute();
            DisposeAll(_redo);
            _undo.Add(command);
            EnforceCapacity();
        }
        finally
        {
            _operationInProgress = false;
        }

        NotifyChanged();
    }

    // Reverts the most recent command and moves it to the redo stack. Returns false when there is nothing
    // to undo.
    public bool Undo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        BeginOperation();
        try
        {
            if (_undo.Count == 0)
                return false;

            IEditorCommand command = _undo[^1];
            command.Undo();
            _undo.RemoveAt(_undo.Count - 1);
            _redo.Add(command);
        }
        finally
        {
            _operationInProgress = false;
        }

        NotifyChanged();
        return true;
    }

    // Re-executes the most recently undone command and moves it back to the undo stack. Returns false when
    // there is nothing to redo.
    public bool Redo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        BeginOperation();
        try
        {
            if (_redo.Count == 0)
                return false;

            IEditorCommand command = _redo[^1];
            command.Execute();
            _redo.RemoveAt(_redo.Count - 1);
            _undo.Add(command);
        }
        finally
        {
            _operationInProgress = false;
        }

        NotifyChanged();
        return true;
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        BeginOperation();
        try
        {
            if (_undo.Count == 0 && _redo.Count == 0)
                return;

            DisposeAll(_undo);
            DisposeAll(_redo);
        }
        finally
        {
            _operationInProgress = false;
        }

        NotifyChanged();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Changed = null;
        DisposeAll(_undo);
        DisposeAll(_redo);
    }

    private void BeginOperation()
    {
        if (_operationInProgress)
        {
            throw new InvalidOperationException(
                "The command history cannot be used while a command operation is in progress."
            );
        }

        _operationInProgress = true;
    }

    // Raised outside the reentrancy guard so subscribers observe a consistent, usable history.
    private void NotifyChanged() => Changed?.Invoke();

    private void EnforceCapacity()
    {
        while (_undo.Count > _capacity)
        {
            IEditorCommand evicted = _undo[0];
            _undo.RemoveAt(0);
            (evicted as IDisposable)?.Dispose();
        }
    }

    private static void DisposeAll(List<IEditorCommand> commands)
    {
        try
        {
            foreach (IEditorCommand command in commands)
                (command as IDisposable)?.Dispose();
        }
        finally
        {
            commands.Clear();
        }
    }
}

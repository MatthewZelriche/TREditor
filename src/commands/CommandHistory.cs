using System;
using System.Collections.Generic;

// Bounded, in-memory undo/redo history. Commands that execute successfully are owned by the history and
// are disposed when they are evicted, invalidated, cleared, or when the history itself is disposed.
// The history also holds the document commands act on and supplies it to them on every call.
public sealed class CommandHistory : IDisposable, IEditorCommandSink
{
    public const int DefaultCapacity = 100;

    // The last entry of each list is the top of that stack.
    private readonly List<IEditorCommand> _undo = [];
    private readonly List<IEditorCommand> _redo = [];
    private readonly int _capacity;
    private Document? _document;
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

    // Raised after a NonFatalCommandError was caught. Stacks are left unchanged.
    public event Action<string, Exception>? OperationFailed;

    public int Capacity => _capacity;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public string? UndoDisplayName => CanUndo ? _undo[^1].DisplayName : null;

    public string? RedoDisplayName => CanRedo ? _redo[^1].DisplayName : null;

    // Points the history at the document later commands will act on. Callers are responsible for clearing
    // history that belongs to a document being replaced.
    public void SetDocument(Document document)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(document);
        _document = document;
    }

    public void Submit(IEditorCommand command) => Execute(command);

    // Executes command and, on success, takes ownership of it and invalidates the redo stack. A command
    // that fails with NonFatalCommandError leaves the history untouched. Any
    // other failure propagates; the command stays owned by the caller.
    public void Execute(IEditorCommand command)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(command);

        Document document = RequireDocument();
        NonFatalCommandError? nonFatal = null;
        BeginOperation();
        try
        {
            try
            {
                command.Execute(document);
            }
            catch (NonFatalCommandError exception)
            {
                nonFatal = exception;
            }

            if (nonFatal is null)
            {
                DisposeAll(_redo);
                _undo.Add(command);
                EnforceCapacity();
            }
        }
        finally
        {
            _operationInProgress = false;
        }

        if (nonFatal is not null)
        {
            NotifyOperationFailed(command.DisplayName, nonFatal);
            return;
        }

        NotifyChanged();
    }

    // Reverts the most recent command and moves it to the redo stack. Returns false when there is nothing
    // to undo, or when undo fails with NonFatalCommandError (stacks stay put).
    public bool Undo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        IEditorCommand? command = null;
        NonFatalCommandError? nonFatal = null;
        BeginOperation();
        try
        {
            if (_undo.Count == 0)
                return false;

            command = _undo[^1];
            try
            {
                command.Undo(RequireDocument());
            }
            catch (NonFatalCommandError exception)
            {
                nonFatal = exception;
            }

            if (nonFatal is null)
            {
                _undo.RemoveAt(_undo.Count - 1);
                _redo.Add(command);
            }
        }
        finally
        {
            _operationInProgress = false;
        }

        if (nonFatal is not null)
        {
            NotifyOperationFailed("Undo", nonFatal);
            return false;
        }

        NotifyChanged();
        return true;
    }

    // Re-executes the most recently undone command and moves it back to the undo stack. Returns false when
    // there is nothing to redo, or when redo fails with NonFatalCommandError (stacks stay put).
    public bool Redo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        IEditorCommand? command = null;
        NonFatalCommandError? nonFatal = null;
        BeginOperation();
        try
        {
            if (_redo.Count == 0)
                return false;

            command = _redo[^1];
            try
            {
                command.Execute(RequireDocument());
            }
            catch (NonFatalCommandError exception)
            {
                nonFatal = exception;
            }

            if (nonFatal is null)
            {
                _redo.RemoveAt(_redo.Count - 1);
                _undo.Add(command);
            }
        }
        finally
        {
            _operationInProgress = false;
        }

        if (nonFatal is not null)
        {
            NotifyOperationFailed("Redo", nonFatal);
            return false;
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
        OperationFailed = null;
        _document = null;
        DisposeAll(_undo);
        DisposeAll(_redo);
    }

    private Document RequireDocument() =>
        _document
        ?? throw new InvalidOperationException("The command history has no document to act on.");

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

    private void NotifyOperationFailed(string operation, NonFatalCommandError exception) =>
        OperationFailed?.Invoke(operation, exception);

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

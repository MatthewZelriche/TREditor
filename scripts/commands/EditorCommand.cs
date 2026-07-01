using System;

public enum EditorCommandState
{
    New,
    Applied,
    Undone,
    Disposed,
}

public abstract class EditorCommand : IDisposable
{
    private EditorCommandContext _context;

    public abstract string Name { get; }

    public virtual bool AffectsDocument => true;

    public EditorCommandState State { get; private set; } = EditorCommandState.New;

    /// <summary>
    /// Attempts the first application before history accepts ownership of the command. Unlike
    /// redo, this attempt may validly fail or do nothing, in which case the command is disposed
    /// without changing history; success binds the context and transitions it to
    /// <see cref="EditorCommandState.Applied"/>.
    /// </summary>
    internal bool ExecuteInitial(EditorCommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        EnsureState(EditorCommandState.New);
        _context = context;

        bool applied;
        try
        {
            applied = Do(context);
        }
        catch
        {
            Dispose();
            throw;
        }

        if (!applied)
        {
            Dispose();
            return false;
        }

        State = EditorCommandState.Applied;
        return true;
    }

    internal void ExecuteUndo()
    {
        EnsureState(EditorCommandState.Applied);
        Undo(_context);
        State = EditorCommandState.Undone;
    }

    internal void ExecuteRedo()
    {
        EnsureState(EditorCommandState.Undone);
        if (!Do(_context))
            throw new InvalidOperationException($"Redo failed for command '{Name}'.");

        State = EditorCommandState.Applied;
    }

    public void Dispose()
    {
        if (State == EditorCommandState.Disposed)
            return;

        EditorCommandState discardedState = State;
        State = EditorCommandState.Disposed;
        OnDispose(_context, discardedState);
    }

    protected abstract bool Do(EditorCommandContext context);

    protected abstract void Undo(EditorCommandContext context);

    /// <summary>
    /// Releases resources retained for history. <paramref name="discardedState"/> identifies
    /// which side of the command is currently active.
    /// </summary>
    protected virtual void OnDispose(
        EditorCommandContext context,
        EditorCommandState discardedState
    ) { }

    private void EnsureState(EditorCommandState expected)
    {
        if (State != expected)
            throw new InvalidOperationException(
                $"Command '{Name}' is {State}; expected {expected}."
            );
    }
}

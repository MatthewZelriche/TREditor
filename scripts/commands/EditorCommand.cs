using Godot;

public abstract partial class EditorCommand : GodotObject
{
    private EditorCommandContext _context;
    private bool _resourcesReleased;

    public abstract string Name { get; }

    public void SetContext(EditorCommandContext context)
    {
        _context = context;
    }

    public void ExecuteDo()
    {
        Do(GetContext());
    }

    public void ExecuteUndo()
    {
        Undo(GetContext());
    }

    public abstract void Do(EditorCommandContext context);

    public abstract void Undo(EditorCommandContext context);

    /// <summary>
    /// Permanently release resources retained solely for future undo/redo. This method is
    /// idempotent because history shutdown and future history eviction may overlap.
    /// </summary>
    public void ReleaseResources()
    {
        if (_resourcesReleased)
            return;

        _resourcesReleased = true;
        OnReleaseResources();
    }

    protected virtual void OnReleaseResources() { }

    private EditorCommandContext GetContext()
    {
        if (_context == null)
        {
            throw new System.InvalidOperationException(
                $"{nameof(EditorCommand)} must be assigned a context before execution."
            );
        }

        return _context;
    }
}

using Godot;

public abstract partial class EditorCommand : GodotObject
{
    private EditorCommandContext _context;

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

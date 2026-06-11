internal interface ICommandHistory : System.IDisposable
{
    bool CanUndo { get; }
    bool CanRedo { get; }

    void Execute(EditorCommand command);
    void Undo();
    void Redo();
    void Clear();
}

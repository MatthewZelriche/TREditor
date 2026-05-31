public interface IEditorCommand
{
    string Name { get; }

    void Do();

    void Undo();
}

using Godot;

public abstract partial class EditorCommand : GodotObject
{
    public abstract string Name { get; }

    public abstract void Do();

    public abstract void Undo();
}

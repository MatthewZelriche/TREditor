using Godot;

public partial class EditorSession : Node3D
{
    public CommandService Commands { get; } = new();

    public override void _ExitTree()
    {
        Commands.Dispose();
    }
}

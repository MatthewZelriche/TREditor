using Godot;

public partial class EditorSession : Node3D
{
    public CommandService Commands { get; } = new();

    public RayPickingService RayPicking { get; private set; }

    public override void _EnterTree()
    {
        RayPicking = new RayPickingService(GetWorld3D());
    }

    public override void _ExitTree()
    {
        Commands.Dispose();
    }
}

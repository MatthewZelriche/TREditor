#nullable enable

using Godot;

public partial class Root : Node
{
    private static readonly Vector2I MinimumWindowSize = new(640, 360);

    private Application? _application;

    public override void _Ready()
    {
        GetWindow().MinSize = MinimumWindowSize;
        _application = new Application(GetNode<Node3D>("%MapRoot"));
    }

    public override void _Process(double delta)
    {
        _application?.Update(delta);
    }

    public override void _ExitTree()
    {
        _application?.Dispose();
        _application = null;
    }
}

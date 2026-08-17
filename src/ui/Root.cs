#nullable enable

using Godot;

public partial class Root : Control
{
    private static readonly Vector2I MinimumWindowSize = new(640, 360);

    public override void _Ready()
    {
        GetWindow().MinSize = MinimumWindowSize;
    }
}

#nullable enable

using Godot;

public partial class MainViewport : ColorRect
{
    private static readonly Color ViewportBackground = new(
        0.0901961f,
        0.0980392f,
        0.113725f,
        1.0f
    );

    public MainViewport()
    {
        Name = "Perspective";
        Color = ViewportBackground;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
    }
}

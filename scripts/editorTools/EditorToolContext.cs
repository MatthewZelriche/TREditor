using System;
using Godot;

public sealed class EditorToolContext
{
    public EditorToolContext(RayPickingService rayPicking, Func<float> getGridSnapSize)
    {
        ArgumentNullException.ThrowIfNull(rayPicking);
        ArgumentNullException.ThrowIfNull(getGridSnapSize);

        RayPicking = rayPicking;
        GetGridSnapSize = getGridSnapSize;
    }

    public RayPickingService RayPicking { get; }
    public Func<float> GetGridSnapSize { get; }
}

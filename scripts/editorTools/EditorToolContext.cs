using System;
using Godot;

public sealed class EditorToolContext
{
    public EditorToolContext(
        RayPickingService rayPicking,
        Node3D worldRoot,
        Func<float> getGridSnapSize
    )
    {
        ArgumentNullException.ThrowIfNull(rayPicking);
        ArgumentNullException.ThrowIfNull(worldRoot);
        ArgumentNullException.ThrowIfNull(getGridSnapSize);

        RayPicking = rayPicking;
        WorldRoot = worldRoot;
        GetGridSnapSize = getGridSnapSize;
    }

    public RayPickingService RayPicking { get; }
    public Node3D WorldRoot { get; }
    public Func<float> GetGridSnapSize { get; }
}

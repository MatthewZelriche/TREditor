using System;

// Every viewport shares one World3D, so a gizmo drawn for one pane would otherwise appear in all of
// them. Each host takes a private render layer from this pool and only the matching camera keeps that
// layer in its cull mask. Godot's inspector exposes layers 1-20 for ordinary scene rendering, while
// scripts can address the internal layers 21-32. The pool uses only that internal range so registering
// a viewport never hides a legitimate scene object.
public sealed class GizmoLayerPool
{
    public const int FirstLayer = 21;
    public const int LayerCount = 12;

    // The layers this pool may hand out. Cameras clear this whole range before receiving their own
    // layer, so a camera can never see another pane's gizmo.
    public static readonly uint ReservedMask = BuildReservedMask();

    private uint _allocated;

    public int AvailableCount => LayerCount - System.Numerics.BitOperations.PopCount(_allocated);

    // Returns the single-bit mask of a free layer. Allocation is deliberately fallible rather than
    // silently reusing a layer, because two hosts sharing a layer would show each other's gizmos.
    public uint Allocate()
    {
        for (int index = 0; index < LayerCount; index++)
        {
            uint mask = 1u << (FirstLayer - 1 + index);
            if ((_allocated & mask) != 0)
                continue;

            _allocated |= mask;
            return mask;
        }

        throw new InvalidOperationException(
            $"All {LayerCount} gizmo render layers are in use. Close a viewport before opening another."
        );
    }

    public void Release(uint mask)
    {
        if ((mask & ReservedMask) != mask)
            throw new ArgumentException("The mask is not a gizmo render layer.", nameof(mask));

        _allocated &= ~mask;
    }

    private static uint BuildReservedMask()
    {
        uint mask = 0;
        for (int index = 0; index < LayerCount; index++)
            mask |= 1u << (FirstLayer - 1 + index);

        return mask;
    }
}

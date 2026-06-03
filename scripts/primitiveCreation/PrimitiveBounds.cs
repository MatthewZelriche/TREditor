using Godot;

/// <summary>Axis-aligned world-space bounds for an interactively created primitive.</summary>
public readonly record struct PrimitiveBounds(Vector3 Min, Vector3 Max)
{
    /// <summary>Smallest allowed extent on any axis before building a mesh.</summary>
    public const float DefaultMinimumExtent = 0.01f;

    /// <summary>Midpoint of the normalized bounds.</summary>
    public Vector3 Center => (Min + Max) * 0.5f;

    /// <summary>Component-wise extent from <see cref="Min"/> to <see cref="Max"/>.</summary>
    public Vector3 Size => Max - Min;

    /// <summary>
    /// Builds normalized bounds from two footprint points on XZ plus the vertical Y range.
    /// </summary>
    public static PrimitiveBounds FromXzAndY(Vector3 firstXzPoint, Vector3 secondXzPoint, float baseY, float maxY)
    {
        return new PrimitiveBounds(
            new Vector3(
                Mathf.Min(firstXzPoint.X, secondXzPoint.X),
                Mathf.Min(baseY, maxY),
                Mathf.Min(firstXzPoint.Z, secondXzPoint.Z)
            ),
            new Vector3(
                Mathf.Max(firstXzPoint.X, secondXzPoint.X),
                Mathf.Max(baseY, maxY),
                Mathf.Max(firstXzPoint.Z, secondXzPoint.Z)
            )
        );
    }

    /// <summary>
    /// Expands any too-small axis around its center so TRMesh builders receive valid extents.
    /// </summary>
    public PrimitiveBounds WithMinimumExtents(float minimumExtent = DefaultMinimumExtent)
    {
        Vector3 min = Min;
        Vector3 max = Max;

        ClampAxis(ref min.X, ref max.X, minimumExtent);
        ClampAxis(ref min.Y, ref max.Y, minimumExtent);
        ClampAxis(ref min.Z, ref max.Z, minimumExtent);

        return new PrimitiveBounds(min, max);
    }

    // Preserve the axis center while enforcing the requested minimum span.
    private static void ClampAxis(ref float min, ref float max, float minimumExtent)
    {
        if (max - min >= minimumExtent)
        {
            return;
        }

        float center = (min + max) * 0.5f;
        float halfExtent = minimumExtent * 0.5f;
        min = center - halfExtent;
        max = center + halfExtent;
    }
}

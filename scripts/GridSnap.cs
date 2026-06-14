using Godot;

public static class GridSnap
{
    public const float Off = 0.0f;

    public static Vector3 Snap(Vector3 position, float cellSize)
    {
        if (cellSize <= 0.0f)
        {
            return position;
        }

        return new Vector3(
            SnapValue(position.X, cellSize),
            SnapValue(position.Y, cellSize),
            SnapValue(position.Z, cellSize)
        );
    }

    public static float SnapDistance(float distance, float cellSize, float maximum)
    {
        if (!(maximum > 0f))
            return 0f;

        distance = Mathf.Clamp(distance, 0f, maximum);
        if (!(cellSize > 0f))
            return distance;

        if (distance >= maximum - cellSize * 0.5f)
            return maximum;

        float snapped = SnapValue(distance, cellSize);
        return Mathf.Clamp(snapped > 0f ? snapped : cellSize, 0f, maximum);
    }

    private static float SnapValue(float value, float cellSize) =>
        Mathf.Round(value / cellSize) * cellSize;
}

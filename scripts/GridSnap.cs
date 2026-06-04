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

    private static float SnapValue(float value, float cellSize) =>
        Mathf.Round(value / cellSize) * cellSize;
}

using Godot;

namespace TREditor2026.Tests;

public class GridSnapTests
{
    [Fact]
    public void Snap_ZeroCellSize_ReturnsOriginalPosition()
    {
        Vector3 position = new(1.7f, -2.3f, 3.1f);

        Assert.Equal(position, GridSnap.Snap(position, GridSnap.Off));
        Assert.Equal(position, GridSnap.Snap(position, 0.0f));
        Assert.Equal(position, GridSnap.Snap(position, -1.0f));
    }

    [Fact]
    public void Snap_PositiveCellSize_RoundsEachAxis()
    {
        Vector3 position = new(1.4f, 2.6f, -0.3f);

        Vector3 snapped = GridSnap.Snap(position, 1.0f);

        Assert.Equal(new Vector3(1, 3, 0), snapped);
    }

    [Fact]
    public void Snap_FractionalCellSize_RoundsToGrid()
    {
        Vector3 position = new(1.11f, 0, 2.24f);

        Vector3 snapped = GridSnap.Snap(position, 0.25f);

        Assert.Equal(new Vector3(1.0f, 0, 2.25f), snapped);
    }
}

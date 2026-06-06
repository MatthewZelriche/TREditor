using Godot;

namespace TREditor2026.Tests;

public class PrimitiveBoundsTests
{
    [Fact]
    public void FromXzAndY_NormalizesSwappedFootprintPoints()
    {
        PrimitiveBounds bounds = PrimitiveBounds.FromXzAndY(
            new Vector3(5, 0, 8),
            new Vector3(1, 0, 2),
            baseY: 0,
            maxY: 4
        );

        Assert.Equal(new Vector3(1, 0, 2), bounds.Min);
        Assert.Equal(new Vector3(5, 4, 8), bounds.Max);
    }

    [Fact]
    public void FromXzAndY_NormalizesInvertedYRange()
    {
        PrimitiveBounds bounds = PrimitiveBounds.FromXzAndY(
            new Vector3(0, 0, 0),
            new Vector3(2, 0, 2),
            baseY: 5,
            maxY: 1
        );

        Assert.Equal(1, bounds.Min.Y);
        Assert.Equal(5, bounds.Max.Y);
    }

    [Fact]
    public void Center_ReturnsMidpoint()
    {
        PrimitiveBounds bounds = new(new Vector3(0, 0, 0), new Vector3(4, 6, 8));

        Assert.Equal(new Vector3(2, 3, 4), bounds.Center);
    }

    [Fact]
    public void Size_ReturnsMaxMinusMin()
    {
        PrimitiveBounds bounds = new(new Vector3(1, 2, 3), new Vector3(4, 8, 9));

        Assert.Equal(new Vector3(3, 6, 6), bounds.Size);
    }

    [Fact]
    public void WithMinimumExtents_ExpandsZeroWidthAxisAroundCenter()
    {
        PrimitiveBounds bounds = new(new Vector3(2, 3, 4), new Vector3(2, 7, 4));
        PrimitiveBounds expanded = bounds.WithMinimumExtents(0.5f);

        Assert.Equal(new Vector3(2, 5, 4), expanded.Center);
        Assert.Equal(0.5f, expanded.Size.X, precision: 5);
        Assert.Equal(4, expanded.Size.Y, precision: 5);
        Assert.Equal(0.5f, expanded.Size.Z, precision: 5);
    }

    [Fact]
    public void WithMinimumExtents_LeavesLargeAxesUnchanged()
    {
        PrimitiveBounds bounds = new(new Vector3(0, 0, 0), new Vector3(2, 3, 4));
        PrimitiveBounds expanded = bounds.WithMinimumExtents(0.01f);

        Assert.Equal(bounds.Min, expanded.Min);
        Assert.Equal(bounds.Max, expanded.Max);
    }

    [Fact]
    public void WithMinimumExtents_UsesDefaultMinimumExtent()
    {
        PrimitiveBounds bounds = new(Vector3.Zero, Vector3.Zero);
        PrimitiveBounds expanded = bounds.WithMinimumExtents();

        Assert.Equal(PrimitiveBounds.DefaultMinimumExtent, expanded.Size.X, precision: 5);
        Assert.Equal(PrimitiveBounds.DefaultMinimumExtent, expanded.Size.Y, precision: 5);
        Assert.Equal(PrimitiveBounds.DefaultMinimumExtent, expanded.Size.Z, precision: 5);
    }
}

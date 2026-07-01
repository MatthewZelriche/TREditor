using Godot;

namespace TREditor2026.Tests;

public sealed class RaySegmentGeometryTests
{
    [Fact]
    public void FindClosestPoints_IntersectingRayAndSegmentReturnsIntersection()
    {
        RaySegmentClosestPoints result = RaySegmentGeometry.FindClosestPoints(
            Vector3.Zero,
            Vector3.Forward,
            new Vector3(-1f, 0f, -2f),
            new Vector3(1f, 0f, -2f)
        );

        AssertVectorApproximately(new Vector3(0f, 0f, -2f), result.RayPoint);
        AssertVectorApproximately(result.RayPoint, result.SegmentPoint);
        Assert.Equal(2f, result.RayDistance, 5);
        Assert.Equal(0.5f, result.SegmentParameter, 5);
    }

    [Fact]
    public void FindClosestPoints_ClampsToRayOriginAndSegmentEnd()
    {
        RaySegmentClosestPoints result = RaySegmentGeometry.FindClosestPoints(
            Vector3.Zero,
            Vector3.Forward,
            new Vector3(2f, 0f, 1f),
            new Vector3(3f, 0f, 1f)
        );

        Assert.Equal(0f, result.RayDistance);
        Assert.Equal(0f, result.SegmentParameter);
        AssertVectorApproximately(Vector3.Zero, result.RayPoint);
        AssertVectorApproximately(new Vector3(2f, 0f, 1f), result.SegmentPoint);
    }

    [Fact]
    public void FindClosestPoints_DegenerateSegmentReturnsItsOnlyPoint()
    {
        Vector3 point = new(2f, 0f, -3f);

        RaySegmentClosestPoints result = RaySegmentGeometry.FindClosestPoints(
            Vector3.Zero,
            Vector3.Forward,
            point,
            point
        );

        Assert.Equal(3f, result.RayDistance, 5);
        Assert.Equal(0f, result.SegmentParameter);
        AssertVectorApproximately(point, result.SegmentPoint);
    }

    private static void AssertVectorApproximately(Vector3 expected, Vector3 actual)
    {
        Assert.InRange(expected.DistanceTo(actual), 0f, 0.00001f);
    }
}

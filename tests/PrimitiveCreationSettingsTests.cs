namespace TREditor2026.Tests;

public class PrimitiveCreationSettingsTests
{
    [Fact]
    public void Box_ReturnsBoxKindWithMinimumCylinderSegments()
    {
        PrimitiveCreationSettings settings = PrimitiveCreationSettings.Box();

        Assert.Equal(PrimitiveKind.Box, settings.Kind);
        Assert.Equal(3, settings.CylinderRadialSegments);
    }

    [Fact]
    public void Cylinder_ReturnsRequestedRadialSegments()
    {
        PrimitiveCreationSettings settings = PrimitiveCreationSettings.Cylinder(12);

        Assert.Equal(PrimitiveKind.Cylinder, settings.Kind);
        Assert.Equal(12, settings.CylinderRadialSegments);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Cylinder_BelowMinimumSegments_Throws(int radialSegments)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrimitiveCreationSettings.Cylinder(radialSegments)
        );
    }
}

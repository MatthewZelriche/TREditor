using System;

// TODO: Revisit this and maybe clean it up. Not a fan of PrimitiveCreationSettings always
// requiring cylinder info.
public readonly struct PrimitiveCreationSettings
{
    public PrimitiveKind Kind { get; }

    public int CylinderRadialSegments { get; }

    private PrimitiveCreationSettings(PrimitiveKind kind, int cylinderRadialSegments)
    {
        if (cylinderRadialSegments < 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cylinderRadialSegments),
                "Cylinder radial segments must be at least 3."
            );
        }

        Kind = kind;
        CylinderRadialSegments = cylinderRadialSegments;
    }

    public static PrimitiveCreationSettings Box() => new(PrimitiveKind.Box, 3);

    public static PrimitiveCreationSettings Cylinder(int radialSegments) =>
        new(PrimitiveKind.Cylinder, radialSegments);
}

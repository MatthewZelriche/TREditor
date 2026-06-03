using System;
using Godot;
using TREditorSharp;
using TREditorSharp.Builders;
using NumericsVector3 = System.Numerics.Vector3;

public static class PrimitiveMeshFactory
{
    public static SpatialMesh Build(PrimitiveCreationSettings settings, PrimitiveBounds bounds)
    {
        PrimitiveBounds clampedBounds = bounds.WithMinimumExtents();

        return settings.Kind switch
        {
            PrimitiveKind.Box => BuildBox(clampedBounds),
            PrimitiveKind.Cylinder => BuildCylinder(settings, clampedBounds),
            _ => throw new ArgumentOutOfRangeException(nameof(settings), settings.Kind, null),
        };
    }

    private static SpatialMesh BuildBox(PrimitiveBounds bounds)
    {
        return MeshBuilders.Build(
            new BlockOptions
            {
                Min = ToNumericsVector3(bounds.Min),
                Max = ToNumericsVector3(bounds.Max),
            }
        );
    }

    private static SpatialMesh BuildCylinder(
        PrimitiveCreationSettings settings,
        PrimitiveBounds bounds
    )
    {
        Vector3 size = bounds.Size;

        return MeshBuilders.Build(
            new CylinderOptions
            {
                Center = ToNumericsVector3(bounds.Center),
                RadiusX = size.X * 0.5f,
                RadiusZ = size.Z * 0.5f,
                Height = size.Y,
                RadialSegments = settings.CylinderRadialSegments,
            }
        );
    }

    private static NumericsVector3 ToNumericsVector3(Vector3 vector) =>
        new(vector.X, vector.Y, vector.Z);
}

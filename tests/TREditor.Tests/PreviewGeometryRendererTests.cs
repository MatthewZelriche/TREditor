using TRMesh.Mesh;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor.Tests;

public sealed class PreviewGeometryRendererTests
{
    [Fact]
    public void BoxWireframeContainsEachTopologicalEdgeOnce()
    {
        using SpatialMesh mesh = SpatialMeshBuilder.CreateAabb(
            NumericsVector3.Zero,
            NumericsVector3.One
        );

        Godot.Vector3[] vertices = PreviewGeometryRenderer.CreateWireVertices(mesh);

        Assert.Equal(mesh.EdgeCount * 2, vertices.Length);
        Assert.Equal(24, vertices.Length);
    }

    [Fact]
    public void CylinderWireframeDoesNotIncludeCapTriangulationDiagonals()
    {
        const int sides = 8;
        using SpatialMesh mesh = SpatialMeshBuilder.CreateCylinder(2.0f, 1.0f, sides);

        Godot.Vector3[] vertices = PreviewGeometryRenderer.CreateWireVertices(mesh);

        Assert.Equal(mesh.EdgeCount * 2, vertices.Length);
        Assert.Equal(sides * 3 * 2, vertices.Length);
    }
}

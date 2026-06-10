using System.Numerics;
using TREditorSharp;

namespace TREditor2026.Tests;

public sealed class InvalidFaceEdgeCollectorTests
{
    [Fact]
    public void Collect_ValidFaceProducesNoEdges()
    {
        using SpatialMesh mesh = BuildQuad(Vector3.Zero, Vector3.UnitX, Vector3.One, Vector3.UnitY);
        List<HalfEdgeHandle> invalidEdges = [];

        InvalidFaceEdgeCollector.Collect(mesh, invalidEdges, []);

        Assert.Empty(invalidEdges);
    }

    [Fact]
    public void Collect_CollapsedFaceProducesItsTopologicalBoundaryEdges()
    {
        using SpatialMesh mesh = BuildQuad(
            Vector3.Zero,
            Vector3.UnitX,
            Vector3.UnitX,
            Vector3.Zero
        );
        List<HalfEdgeHandle> invalidEdges = [];

        InvalidFaceEdgeCollector.Collect(mesh, invalidEdges, []);

        Assert.Equal(4, invalidEdges.Count);
        Assert.Equal(4, invalidEdges.Select(edge => edge.Index).Distinct().Count());
    }

    private static SpatialMesh BuildQuad(params Vector3[] positions)
    {
        SpatialMesh mesh = new();
        mesh.AddFace(positions.Select(mesh.AddVertex).ToArray());
        return mesh;
    }
}

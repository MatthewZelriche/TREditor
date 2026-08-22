using System.Numerics;
using TRMesh.Mesh;

namespace TREditor.Tests;

public class GDMeshSurfaceBuilderTests
{
    [Fact]
    public void Build_QuadUsesFourVerticesAndSixClockwiseIndices()
    {
        using var mesh = new SpatialMesh();
        int v0 = mesh.AddVertex(new Vector3(0, 0, 0));
        int v1 = mesh.AddVertex(new Vector3(2, 0, 0));
        int v2 = mesh.AddVertex(new Vector3(2, 2, 0));
        int v3 = mesh.AddVertex(new Vector3(0, 2, 0));
        Vector2[] uvs = [Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY];
        mesh.AddFace([v0, v1, v2, v3], uvs);

        var builder = new GDMeshSurfaceBuilder();
        MeshSurface surface = builder.Build(mesh);

        Assert.Equal(4, surface.Vertices.Count);
        Assert.Equal(4, surface.Normals.Count);
        Assert.Equal(4, surface.Uvs.Count);
        Assert.Equal([0, 1, 2, 0, 3, 1], surface.ElementIndices);
        Assert.Equal([uvs[0], uvs[2], uvs[1], uvs[3]], surface.Uvs);
        AssertClockwise(surface);
        Assert.Equal(0, surface.SkippedFaceCount);
    }

    [Fact]
    public void Build_AabbSplitsVerticesByFaceCorner()
    {
        using SpatialMesh mesh = SpatialMeshBuilder.CreateAabb(Vector3.Zero, Vector3.One);
        var builder = new GDMeshSurfaceBuilder();

        MeshSurface surface = builder.Build(mesh);

        Assert.Equal(24, surface.Vertices.Count);
        Assert.Equal(36, surface.ElementIndices.Count);
        Assert.Equal(surface.ElementIndices.Count / 3, surface.FaceSlots.Count);
        Assert.Equal(12, surface.FaceSlots.Count);
        Assert.Equal(6, surface.FaceSlots.Distinct().Count());
        Assert.All(
            surface.FaceSlots.GroupBy(slot => slot),
            slots => Assert.Equal(2, slots.Count())
        );
        AssertClockwise(surface);
    }

    [Fact]
    public void Build_SkipsDegenerateFaceAndKeepsValidFace()
    {
        using var mesh = new SpatialMesh();
        int v0 = mesh.AddVertex(new Vector3(0, 0, 0));
        int v1 = mesh.AddVertex(new Vector3(1, 0, 0));
        int v2 = mesh.AddVertex(new Vector3(0, 1, 0));
        mesh.AddFace([v0, v1, v2], [Vector2.Zero, Vector2.Zero, Vector2.Zero]);

        int v3 = mesh.AddVertex(new Vector3(0, 0, 1));
        int v4 = mesh.AddVertex(new Vector3(1, 0, 1));
        int v5 = mesh.AddVertex(new Vector3(2, 0, 1));
        int v6 = mesh.AddVertex(new Vector3(3, 0, 1));
        mesh.AddFace(
            [v3, v4, v5, v6],
            [Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero]
        );

        var builder = new GDMeshSurfaceBuilder();
        MeshSurface surface = builder.Build(mesh);

        Assert.Equal(3, surface.Vertices.Count);
        Assert.Equal(3, surface.ElementIndices.Count);
        Assert.Equal(1, surface.SkippedFaceCount);
        AssertClockwise(surface);
    }

    static void AssertClockwise(MeshSurface surface)
    {
        for (int i = 0; i < surface.ElementIndices.Count; i += 3)
        {
            int ia = surface.ElementIndices[i];
            int ib = surface.ElementIndices[i + 1];
            int ic = surface.ElementIndices[i + 2];
            Vector3 a = surface.Vertices[ia];
            Vector3 b = surface.Vertices[ib];
            Vector3 c = surface.Vertices[ic];
            Vector3 normal = surface.Normals[ia];
            float signedArea = Vector3.Dot(Vector3.Cross(b - a, c - a), normal);
            Assert.True(signedArea < 0f, $"Triangle at index {i} is not clockwise.");
        }
    }
}

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
        builder.Build(mesh);

        Assert.Equal(4, builder.Vertices.Count);
        Assert.Equal(4, builder.Normals.Count);
        Assert.Equal(4, builder.Uvs.Count);
        Assert.Equal([0, 1, 2, 0, 3, 1], builder.Indices);
        Assert.Equal([uvs[0], uvs[2], uvs[1], uvs[3]], builder.Uvs);
        AssertClockwise(builder);
        Assert.Equal(0, builder.SkippedFaceCount);
    }

    [Fact]
    public void Build_AabbSplitsVerticesByFaceCorner()
    {
        using SpatialMesh mesh = SpatialMeshBuilder.CreateAabb(Vector3.Zero, Vector3.One);
        var builder = new GDMeshSurfaceBuilder();

        builder.Build(mesh);

        Assert.Equal(24, builder.Vertices.Count);
        Assert.Equal(36, builder.Indices.Count);
        AssertClockwise(builder);
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
        builder.Build(mesh);

        Assert.Equal(3, builder.Vertices.Count);
        Assert.Equal(3, builder.Indices.Count);
        Assert.Equal(1, builder.SkippedFaceCount);
        AssertClockwise(builder);
    }

    static void AssertClockwise(GDMeshSurfaceBuilder builder)
    {
        for (int i = 0; i < builder.Indices.Count; i += 3)
        {
            int ia = builder.Indices[i];
            int ib = builder.Indices[i + 1];
            int ic = builder.Indices[i + 2];
            Vector3 a = builder.Vertices[ia];
            Vector3 b = builder.Vertices[ib];
            Vector3 c = builder.Vertices[ic];
            Vector3 normal = builder.Normals[ia];
            float signedArea = Vector3.Dot(Vector3.Cross(b - a, c - a), normal);
            Assert.True(signedArea < 0f, $"Triangle at index {i} is not clockwise.");
        }
    }
}

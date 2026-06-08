using System.Numerics;
using TREditorSharp;
using GodotVector2 = Godot.Vector2;
using GodotVector3 = Godot.Vector3;

namespace TREditor2026.Tests;

public sealed class MeshRenderDataBuilderTests
{
    [Fact]
    public void AppendTriangle_UsesSuppliedFaceCornersForPositionsAndUvs()
    {
        using SpatialMesh mesh = BuildTriangle();
        FaceHandle face = GetOnlyFace(mesh);
        List<FaceCornerHandle> corners = GetCorners(mesh, face);
        mesh.SetFaceCornerUv(corners[0], new Vector2(1f, 2f));
        mesh.SetFaceCornerUv(corners[1], new Vector2(3f, 4f));
        mesh.SetFaceCornerUv(corners[2], new Vector2(5f, 6f));
        MeshRenderData data = new();

        MeshRenderDataBuilder.AppendTriangle(mesh, data, corners[0], corners[2], corners[1]);

        Assert.Equal(
            [
                GetCornerPosition(mesh, corners[0]),
                GetCornerPosition(mesh, corners[2]),
                GetCornerPosition(mesh, corners[1]),
            ],
            data.Vertices
        );
        Assert.Equal(
            [new GodotVector2(1f, 2f), new GodotVector2(5f, 6f), new GodotVector2(3f, 4f)],
            data.Uvs
        );
        Assert.Equal([0, 1, 2], data.Indices);
        Assert.Equal(3, data.Normals.Count);
    }

    [Fact]
    public void AppendTriangle_AppendsIndicesAfterExistingRenderVertices()
    {
        using SpatialMesh mesh = BuildTriangle();
        FaceHandle face = GetOnlyFace(mesh);
        List<FaceCornerHandle> corners = GetCorners(mesh, face);
        MeshRenderData data = new();

        MeshRenderDataBuilder.AppendTriangle(mesh, data, corners[0], corners[2], corners[1]);
        MeshRenderDataBuilder.AppendTriangle(mesh, data, corners[0], corners[2], corners[1]);

        Assert.Equal([0, 1, 2, 3, 4, 5], data.Indices);
        Assert.Equal(6, data.Vertices.Count);
        Assert.Equal(6, data.Uvs.Count);
    }

    [Fact]
    public void AppendTriangle_SharedGeometricVertexRetainsEachFaceCornersUv()
    {
        using SpatialMesh mesh = BuildTwoTrianglesSharingVertex();
        List<FaceHandle> faces = GetFaces(mesh);
        FaceCornerHandle cornerA = FindCornerAtOrigin(mesh, faces[0]);
        FaceCornerHandle cornerB = FindCornerAtOrigin(mesh, faces[1]);
        mesh.SetFaceCornerUv(cornerA, new Vector2(1f, 2f));
        mesh.SetFaceCornerUv(cornerB, new Vector2(8f, 9f));
        MeshRenderData data = new();

        MeshRenderDataBuilder.AppendTriangle(
            mesh,
            data,
            cornerA,
            mesh.GetHalfEdge(cornerA).Next,
            mesh.GetHalfEdge(mesh.GetHalfEdge(cornerA).Next).Next
        );
        MeshRenderDataBuilder.AppendTriangle(
            mesh,
            data,
            cornerB,
            mesh.GetHalfEdge(cornerB).Next,
            mesh.GetHalfEdge(mesh.GetHalfEdge(cornerB).Next).Next
        );

        Assert.Equal(data.Vertices[0], data.Vertices[3]);
        Assert.Equal(new GodotVector2(1f, 2f), data.Uvs[0]);
        Assert.Equal(new GodotVector2(8f, 9f), data.Uvs[3]);
    }

    private static SpatialMesh BuildTriangle()
    {
        SpatialMesh mesh = new();
        VertexHandle a = mesh.AddVertex(new Vector3(0f, 0f, 0f));
        VertexHandle b = mesh.AddVertex(new Vector3(1f, 0f, 0f));
        VertexHandle c = mesh.AddVertex(new Vector3(0f, 0f, 1f));
        mesh.AddFace([a, c, b]);
        return mesh;
    }

    private static SpatialMesh BuildTwoTrianglesSharingVertex()
    {
        SpatialMesh mesh = new();
        VertexHandle shared = mesh.AddVertex(Vector3.Zero);
        VertexHandle a = mesh.AddVertex(new Vector3(1f, 0f, 0f));
        VertexHandle b = mesh.AddVertex(new Vector3(0f, 0f, 1f));
        VertexHandle c = mesh.AddVertex(new Vector3(-1f, 0f, 0f));
        mesh.AddFace([shared, b, a]);
        mesh.AddFace([shared, c, b]);
        return mesh;
    }

    private static FaceHandle GetOnlyFace(SpatialMesh mesh)
    {
        FaceHandle face = default;
        int count = 0;
        foreach (FaceHandle candidate in mesh.EnumerateLiveFaces())
        {
            face = candidate;
            count++;
        }

        Assert.Equal(1, count);
        return face;
    }

    private static List<FaceCornerHandle> GetCorners(SpatialMesh mesh, FaceHandle face)
    {
        List<FaceCornerHandle> corners = [];
        foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(face))
            corners.Add(corner);
        return corners;
    }

    private static List<FaceHandle> GetFaces(SpatialMesh mesh)
    {
        List<FaceHandle> faces = [];
        foreach (FaceHandle face in mesh.EnumerateLiveFaces())
            faces.Add(face);
        return faces;
    }

    private static FaceCornerHandle FindCornerAtOrigin(SpatialMesh mesh, FaceHandle face)
    {
        foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(face))
        {
            VertexHandle vertex = mesh.GetHalfEdge(corner).Origin;
            if (mesh.GetVertexPosition(vertex) == Vector3.Zero)
                return corner;
        }

        throw new InvalidOperationException($"Face {face} has no corner at the origin.");
    }

    private static GodotVector3 GetCornerPosition(SpatialMesh mesh, FaceCornerHandle corner)
    {
        Vector3 position = mesh.GetVertexPosition(mesh.GetHalfEdge(corner).Origin);
        return new GodotVector3(position.X, position.Y, position.Z);
    }
}

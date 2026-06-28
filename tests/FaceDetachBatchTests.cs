using System.Numerics;
using TREditorSharp;
using TREditorSharp.Builders;

namespace TREditor2026.Tests;

public sealed class FaceDetachBatchTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void Detach_SeparatesFaceVerticesFromNeighbors()
    {
        using SpatialMesh mesh = BuildBox(out FaceHandle sourceFace);
        HashSet<VertexHandle> originalVertices = CollectFaceVertices(mesh, sourceFace).ToHashSet();

        using FaceDetachBatch batch = AssertDetach(mesh, [sourceFace]);
        FaceHandle detachedFace = Assert.Single(batch.DetachedFaces);

        Assert.False(mesh.IsFaceAlive(sourceFace));
        Assert.DoesNotContain(
            CollectFaceVertices(mesh, detachedFace),
            vertex => originalVertices.Contains(vertex)
        );
    }

    [Fact]
    public void Detach_AdjacentFacesRemainConnectedToEachOther()
    {
        using SpatialMesh mesh = BuildBox(out FaceHandle first);
        FaceHandle second = FindAdjacentFace(mesh, first);

        using FaceDetachBatch batch = AssertDetach(mesh, [first, second]);

        FaceHandle detachedFirst = batch.DetachedFaces[0];
        FaceHandle detachedSecond = batch.DetachedFaces[1];
        Assert.Equal(
            2,
            CollectFaceVertices(mesh, detachedFirst)
                .Intersect(CollectFaceVertices(mesh, detachedSecond))
                .Count()
        );
    }

    [Fact]
    public void Detach_VertexTouchingRegionsRemainDisconnected()
    {
        using SpatialMesh mesh = BuildFourTriangleFan(
            out FaceHandle first,
            out FaceHandle opposite
        );

        using FaceDetachBatch batch = AssertDetach(mesh, [first, opposite]);

        Assert.Empty(
            CollectFaceVertices(mesh, batch.DetachedFaces[0])
                .Intersect(CollectFaceVertices(mesh, batch.DetachedFaces[1]))
        );
    }

    [Fact]
    public void Detach_PreservesMaterialAndCornerUvs()
    {
        using SpatialMesh mesh = BuildBox(out FaceHandle sourceFace);
        List<Vector2> expectedUvs = [];
        int uvIndex = 0;
        foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(sourceFace))
        {
            Vector2 uv = new(uvIndex, uvIndex * 0.5f);
            uvIndex++;
            mesh.SetFaceCornerUv(corner, uv);
            expectedUvs.Add(uv);
        }
        mesh.SetFaceUvsInitialized(sourceFace, true);
        mesh.SetFaceMaterialSlot(sourceFace, 12);

        using FaceDetachBatch batch = AssertDetach(mesh, [sourceFace]);
        FaceHandle detachedFace = Assert.Single(batch.DetachedFaces);

        Assert.Equal(12, mesh.GetFaceMaterialSlot(detachedFace));
        Assert.True(mesh.AreFaceUvsInitialized(detachedFace));
        Assert.Equal(expectedUvs, CollectFaceUvs(mesh, detachedFace));
    }

    [Fact]
    public void Detach_UndoRedoRestoresExactHandles()
    {
        using SpatialMesh mesh = BuildBox(out FaceHandle sourceFace);
        using FaceDetachBatch batch = AssertDetach(mesh, [sourceFace]);
        FaceHandle detachedFace = Assert.Single(batch.DetachedFaces);

        batch.ApplyBefore();

        Assert.True(mesh.IsFaceAlive(sourceFace));
        Assert.False(mesh.IsFaceAlive(detachedFace));

        batch.ApplyAfter();

        Assert.False(mesh.IsFaceAlive(sourceFace));
        Assert.True(mesh.IsFaceAlive(detachedFace));
    }

    private static FaceDetachBatch AssertDetach(SpatialMesh mesh, FaceHandle[] faces)
    {
        FaceDetachBatch? batch = FaceDetachBatch.Detach(ObjectId, mesh, faces);
        Assert.NotNull(batch);
        return batch;
    }

    private static SpatialMesh BuildBox(out FaceHandle top)
    {
        SpatialMesh mesh = MeshBuilders.Build(
            new BlockOptions { Min = new Vector3(-1), Max = new Vector3(1) }
        );
        top = FaceHandle.Null;
        foreach (FaceHandle face in mesh.EnumerateLiveFaces())
        {
            if (Vector3.Dot(mesh.ComputeFaceNormal(face), Vector3.UnitY) > 0.9f)
            {
                top = face;
                break;
            }
        }
        return mesh;
    }

    private static FaceHandle FindAdjacentFace(SpatialMesh mesh, FaceHandle face)
    {
        foreach (HalfEdgeHandle edge in mesh.HalfEdgesAroundFace(face))
        {
            FaceHandle adjacent = mesh.GetHalfEdge(mesh.GetHalfEdge(edge).Twin).Face;
            if (mesh.IsFaceAlive(adjacent))
                return adjacent;
        }
        throw new InvalidOperationException();
    }

    private static SpatialMesh BuildFourTriangleFan(out FaceHandle first, out FaceHandle opposite)
    {
        SpatialMesh mesh = new();
        VertexHandle center = mesh.AddVertex(Vector3.Zero);
        VertexHandle east = mesh.AddVertex(Vector3.UnitX);
        VertexHandle north = mesh.AddVertex(Vector3.UnitY);
        VertexHandle west = mesh.AddVertex(-Vector3.UnitX);
        VertexHandle south = mesh.AddVertex(-Vector3.UnitY);

        first = mesh.AddFace([center, east, north]);
        mesh.AddFace([center, south, east]);
        opposite = mesh.AddFace([center, west, south]);
        mesh.AddFace([center, north, west]);
        return mesh;
    }

    private static List<VertexHandle> CollectFaceVertices(SpatialMesh mesh, FaceHandle face)
    {
        List<VertexHandle> vertices = [];
        foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(face))
            vertices.Add(mesh.GetHalfEdge(corner).Origin);
        return vertices;
    }

    private static List<Vector2> CollectFaceUvs(SpatialMesh mesh, FaceHandle face)
    {
        List<Vector2> uvs = [];
        foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(face))
            uvs.Add(mesh.GetFaceCornerUv(corner));
        return uvs;
    }
}

using System.Numerics;
using TREditorSharp;

namespace TREditor2026.Tests;

public sealed class EdgeDeletionBatchTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void Delete_OneFaceBoundaryEdgeRemovesFaceAndEdgeAndUndoRestoresExactHandles()
    {
        using SpatialMesh mesh = BuildTriangle(
            out VertexHandle a,
            out VertexHandle b,
            out _,
            out FaceHandle face
        );
        HalfEdgeHandle edge = FindEdge(mesh, a, b);
        HalfEdgeHandle twin = mesh.GetHalfEdge(edge).Twin;

        EdgeDeletionBatch batch = AssertDeleted(mesh, edge);

        Assert.False(mesh.IsFaceAlive(face));
        Assert.False(mesh.IsHalfEdgeAlive(edge));
        Assert.False(mesh.IsHalfEdgeAlive(twin));
        Assert.Equal(3, CountLiveVertices(mesh));

        batch.ApplyBefore();

        Assert.True(mesh.IsFaceAlive(face));
        Assert.True(mesh.IsHalfEdgeAlive(edge));
        Assert.True(mesh.IsHalfEdgeAlive(twin));
        Assert.Equal(1, CountLiveFaces(mesh));
    }

    [Fact]
    public void Delete_TwoFaceSharedEdgeRemovesBothFaces()
    {
        using SpatialMesh mesh = BuildAdjacentTriangles(
            out VertexHandle a,
            out _,
            out VertexHandle c,
            out _,
            out FaceHandle first,
            out FaceHandle second
        );
        HalfEdgeHandle edge = FindEdge(mesh, a, c);

        EdgeDeletionBatch batch = AssertDeleted(mesh, edge);

        Assert.False(mesh.IsFaceAlive(first));
        Assert.False(mesh.IsFaceAlive(second));
        Assert.Equal(0, CountLiveFaces(mesh));
        Assert.Equal(4, CountLiveVertices(mesh));

        batch.ApplyBefore();

        Assert.True(mesh.IsFaceAlive(first));
        Assert.True(mesh.IsFaceAlive(second));
        Assert.Equal(2, CountLiveFaces(mesh));
    }

    [Fact]
    public void Delete_MultipleAdjacentEdgesRemovesSharedFaceOnce()
    {
        using SpatialMesh mesh = BuildTriangle(
            out VertexHandle a,
            out VertexHandle b,
            out VertexHandle c,
            out FaceHandle face
        );
        HalfEdgeHandle edgeAb = FindEdge(mesh, a, b);
        HalfEdgeHandle edgeBc = FindEdge(mesh, b, c);

        EdgeDeletionBatch batch = AssertDeleted(mesh, edgeAb, edgeBc);

        Assert.Single(batch.RemovedFaces);
        Assert.False(mesh.IsHalfEdgeAlive(edgeAb));
        Assert.False(mesh.IsHalfEdgeAlive(edgeBc));
        Assert.True(mesh.IsHalfEdgeAlive(FindEdge(mesh, c, a)));

        batch.ApplyBefore();

        Assert.True(mesh.IsFaceAlive(face));
        Assert.True(mesh.IsHalfEdgeAlive(edgeAb));
        Assert.True(mesh.IsHalfEdgeAlive(edgeBc));
    }

    [Fact]
    public void Delete_WireEdgesWithoutFacesAreRemovedAndRestored()
    {
        using SpatialMesh mesh = BuildTriangle(out _, out _, out _, out FaceHandle face);
        Assert.True(mesh.RemoveFace(face));
        HalfEdgeHandle[] wireEdges = UniqueEdges(mesh);

        EdgeDeletionBatch batch = AssertDeleted(mesh, wireEdges);

        Assert.Empty(batch.RemovedFaces);
        Assert.Equal(0, CountLiveHalfEdges(mesh));
        Assert.Equal(3, CountLiveVertices(mesh));

        batch.ApplyBefore();

        Assert.Equal(6, CountLiveHalfEdges(mesh));
        Assert.Equal(0, CountLiveFaces(mesh));
        Assert.All(wireEdges, edge => Assert.True(mesh.IsHalfEdgeAlive(edge)));
    }

    [Fact]
    public void Delete_TwinSelectionsAreDeduplicatedToOneEdge()
    {
        using SpatialMesh mesh = BuildTriangle(
            out VertexHandle a,
            out VertexHandle b,
            out _,
            out _
        );
        HalfEdgeHandle edge = FindEdge(mesh, a, b);
        HalfEdgeHandle twin = mesh.GetHalfEdge(edge).Twin;

        EdgeDeletionBatch batch = AssertDeleted(mesh, edge, twin);

        Assert.Single(batch.RemovedFaces);
        Assert.Equal(2, batch.RemovedEdges.Count);
        Assert.False(mesh.IsHalfEdgeAlive(edge));
        Assert.False(mesh.IsHalfEdgeAlive(twin));
    }

    [Fact]
    public void Delete_PreservesFaceTextureAndUvStateThroughUndo()
    {
        using SpatialMesh mesh = BuildQuad(
            out VertexHandle a,
            out VertexHandle b,
            out FaceHandle face
        );
        Vector2[] expectedUvs = CaptureUvs(mesh, face);
        for (int i = 0; i < expectedUvs.Length; i++)
            expectedUvs[i] = new Vector2(i + 1, (i + 1) * 2);
        int index = 0;
        foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(face))
            mesh.SetFaceCornerUv(corner, expectedUvs[index++]);
        mesh.SetFaceMaterialSlot(face, 5);
        mesh.SetFaceUvsInitialized(face, true);
        HalfEdgeHandle edge = FindEdge(mesh, a, b);

        EdgeDeletionBatch batch = AssertDeleted(mesh, edge);
        batch.ApplyBefore();

        Assert.True(mesh.IsFaceAlive(face));
        Assert.Equal(5, mesh.GetFaceMaterialSlot(face));
        Assert.True(mesh.AreFaceUvsInitialized(face));
        Assert.Equal(expectedUvs, CaptureUvs(mesh, face));
    }

    [Fact]
    public void ApplyBeforeAndAfter_RepeatRedoReusesStoredStateAndExactHandles()
    {
        using SpatialMesh mesh = BuildTriangle(
            out VertexHandle a,
            out VertexHandle b,
            out _,
            out FaceHandle face
        );
        HalfEdgeHandle edge = FindEdge(mesh, a, b);

        EdgeDeletionBatch batch = AssertDeleted(mesh, edge);

        for (int i = 0; i < 3; i++)
        {
            batch.ApplyBefore();
            Assert.True(mesh.IsFaceAlive(face));
            Assert.True(mesh.IsHalfEdgeAlive(edge));

            batch.ApplyAfter();
            Assert.False(mesh.IsFaceAlive(face));
            Assert.False(mesh.IsHalfEdgeAlive(edge));
        }
    }

    [Fact]
    public void Delete_NoLiveEdgesReturnsNullAndLeavesMeshUnchanged()
    {
        using SpatialMesh mesh = BuildTriangle(out _, out _, out _, out _);

        Assert.Null(EdgeDeletionBatch.Delete(ObjectId, mesh, [HalfEdgeHandle.Null]));
        Assert.Equal(1, CountLiveFaces(mesh));
    }

    private static EdgeDeletionBatch AssertDeleted(SpatialMesh mesh, params HalfEdgeHandle[] edges)
    {
        EdgeDeletionBatch? batch = EdgeDeletionBatch.Delete(ObjectId, mesh, edges);
        Assert.NotNull(batch);
        return batch;
    }

    private static SpatialMesh BuildTriangle(
        out VertexHandle a,
        out VertexHandle b,
        out VertexHandle c,
        out FaceHandle face
    )
    {
        SpatialMesh mesh = new();
        a = mesh.AddVertex(Vector3.Zero);
        b = mesh.AddVertex(Vector3.UnitX);
        c = mesh.AddVertex(Vector3.UnitY);
        face = mesh.AddFace([a, b, c]);
        return mesh;
    }

    private static SpatialMesh BuildAdjacentTriangles(
        out VertexHandle a,
        out VertexHandle b,
        out VertexHandle c,
        out VertexHandle d,
        out FaceHandle first,
        out FaceHandle second
    )
    {
        SpatialMesh mesh = new();
        a = mesh.AddVertex(Vector3.Zero);
        b = mesh.AddVertex(Vector3.UnitX);
        c = mesh.AddVertex(Vector3.One);
        d = mesh.AddVertex(Vector3.UnitY);
        first = mesh.AddFace([a, c, b]);
        second = mesh.AddFace([a, d, c]);
        return mesh;
    }

    private static SpatialMesh BuildQuad(
        out VertexHandle a,
        out VertexHandle b,
        out FaceHandle face
    )
    {
        SpatialMesh mesh = new();
        a = mesh.AddVertex(Vector3.Zero);
        b = mesh.AddVertex(Vector3.UnitX);
        VertexHandle c = mesh.AddVertex(Vector3.One);
        VertexHandle d = mesh.AddVertex(Vector3.UnitY);
        face = mesh.AddFace([a, b, c, d]);
        return mesh;
    }

    private static Vector2[] CaptureUvs(SpatialMesh mesh, FaceHandle face)
    {
        List<Vector2> uvs = [];
        foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(face))
            uvs.Add(mesh.GetFaceCornerUv(corner));
        return uvs.ToArray();
    }

    private static HalfEdgeHandle FindEdge(
        SpatialMesh mesh,
        VertexHandle origin,
        VertexHandle destination
    )
    {
        foreach (HalfEdgeHandle edge in mesh.EnumerateLiveHalfEdges())
        {
            HalfEdge halfEdge = mesh.GetHalfEdge(edge);
            if (halfEdge.Origin == origin && mesh.GetHalfEdge(halfEdge.Twin).Origin == destination)
            {
                return edge;
            }
        }

        throw new InvalidOperationException($"No live half-edge from {origin} to {destination}.");
    }

    private static HalfEdgeHandle[] UniqueEdges(SpatialMesh mesh)
    {
        HashSet<HalfEdgeHandle> visited = [];
        List<HalfEdgeHandle> edges = [];
        foreach (HalfEdgeHandle edge in mesh.EnumerateLiveHalfEdges())
        {
            if (!visited.Add(edge))
                continue;

            visited.Add(mesh.GetHalfEdge(edge).Twin);
            edges.Add(edge);
        }

        return edges.ToArray();
    }

    private static int CountLiveVertices(SpatialMesh mesh)
    {
        int count = 0;
        foreach (VertexHandle _ in mesh.EnumerateLiveVertices())
            count++;
        return count;
    }

    private static int CountLiveHalfEdges(SpatialMesh mesh)
    {
        int count = 0;
        foreach (HalfEdgeHandle _ in mesh.EnumerateLiveHalfEdges())
            count++;
        return count;
    }

    private static int CountLiveFaces(SpatialMesh mesh)
    {
        int count = 0;
        foreach (FaceHandle _ in mesh.EnumerateLiveFaces())
            count++;
        return count;
    }
}

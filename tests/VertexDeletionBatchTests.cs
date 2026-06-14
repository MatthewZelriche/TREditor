using System.Numerics;
using TREditorSharp;

namespace TREditor2026.Tests;

public sealed class VertexDeletionBatchTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void Delete_IsolatedVertexRemovesItAndUndoRestoresExactHandleAndPosition()
    {
        using SpatialMesh mesh = new();
        Vector3 position = new(1, 2, 3);
        VertexHandle vertex = mesh.AddVertex(position);

        using VertexDeletionBatch batch = AssertDeleted(mesh, vertex);

        Assert.False(mesh.IsVertexAlive(vertex));
        Assert.Single(batch.RemovedVertices);
        Assert.Empty(batch.RemovedEdges);
        Assert.Empty(batch.RemovedFaces);

        batch.ApplyBefore();

        Assert.True(mesh.IsVertexAlive(vertex));
        Assert.Equal(position, mesh.GetVertexPosition(vertex));
    }

    [Fact]
    public void Delete_QuadCornerRemovesIncidentFaceEdgesAndVertex()
    {
        using SpatialMesh mesh = BuildQuad(
            out VertexHandle a,
            out VertexHandle b,
            out VertexHandle c,
            out VertexHandle d,
            out FaceHandle face
        );
        HalfEdgeHandle oppositeEdge = FindEdge(mesh, b, c);

        using VertexDeletionBatch batch = AssertDeleted(mesh, a);

        Assert.False(mesh.IsVertexAlive(a));
        Assert.True(mesh.IsVertexAlive(b));
        Assert.True(mesh.IsVertexAlive(c));
        Assert.True(mesh.IsVertexAlive(d));
        Assert.False(mesh.IsFaceAlive(face));
        Assert.True(mesh.IsHalfEdgeAlive(oppositeEdge));
        Assert.Equal(4, batch.RemovedEdges.Count);
        Assert.Single(batch.RemovedFaces);

        batch.ApplyBefore();

        Assert.True(mesh.IsVertexAlive(a));
        Assert.True(mesh.IsFaceAlive(face));
    }

    [Fact]
    public void Delete_SharedVertexRemovesEveryIncidentFaceAndEdge()
    {
        using SpatialMesh mesh = BuildAdjacentTriangles(
            out VertexHandle shared,
            out _,
            out _,
            out _,
            out FaceHandle first,
            out FaceHandle second
        );

        using VertexDeletionBatch batch = AssertDeleted(mesh, shared);

        Assert.False(mesh.IsVertexAlive(shared));
        Assert.False(mesh.IsFaceAlive(first));
        Assert.False(mesh.IsFaceAlive(second));
        Assert.Equal(6, batch.RemovedEdges.Count);
        Assert.Equal(2, batch.RemovedFaces.Count);
        Assert.Equal(2, CountLiveEdges(mesh));
    }

    [Fact]
    public void Delete_AdjacentSelectedVerticesDeduplicatesSharedTopology()
    {
        using SpatialMesh mesh = BuildQuad(
            out VertexHandle a,
            out VertexHandle b,
            out _,
            out _,
            out _
        );

        using VertexDeletionBatch batch = AssertDeleted(mesh, a, b);

        Assert.Equal(2, batch.RemovedVertices.Count);
        Assert.Equal(6, batch.RemovedEdges.Count);
        Assert.Single(batch.RemovedFaces);
        Assert.Equal(1, CountLiveEdges(mesh));
        Assert.Equal(2, CountLiveVertices(mesh));
    }

    [Fact]
    public void Delete_WireVertexRemovesOnlyItsIncidentEdges()
    {
        using SpatialMesh mesh = BuildQuad(
            out VertexHandle a,
            out _,
            out _,
            out _,
            out FaceHandle face
        );
        Assert.True(mesh.RemoveFace(face));

        using VertexDeletionBatch batch = AssertDeleted(mesh, a);

        Assert.False(mesh.IsVertexAlive(a));
        Assert.Empty(batch.RemovedFaces);
        Assert.Equal(4, batch.RemovedEdges.Count);
        Assert.Equal(2, CountLiveEdges(mesh));

        batch.ApplyBefore();

        Assert.True(mesh.IsVertexAlive(a));
        Assert.Equal(4, CountLiveEdges(mesh));
    }

    [Fact]
    public void ApplyBeforeAndAfter_RepeatRedoRestoresExactTopologyAndTextureState()
    {
        using SpatialMesh mesh = BuildQuad(
            out VertexHandle a,
            out _,
            out _,
            out _,
            out FaceHandle face
        );
        mesh.SetFaceMaterialSlot(face, 7);
        mesh.SetFaceUvsInitialized(face, true);
        Vector2[] expectedUvs = [new(1, 2), new(3, 4), new(5, 6), new(7, 8)];
        int cornerIndex = 0;
        foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(face))
            mesh.SetFaceCornerUv(corner, expectedUvs[cornerIndex++]);

        using VertexDeletionBatch batch = AssertDeleted(mesh, a);

        for (int i = 0; i < 3; i++)
        {
            batch.ApplyBefore();
            Assert.True(mesh.IsVertexAlive(a));
            Assert.True(mesh.IsFaceAlive(face));
            Assert.Equal(7, mesh.GetFaceMaterialSlot(face));
            Assert.True(mesh.AreFaceUvsInitialized(face));
            Assert.Equal(expectedUvs, CaptureUvs(mesh, face));

            batch.ApplyAfter();
            Assert.False(mesh.IsVertexAlive(a));
            Assert.False(mesh.IsFaceAlive(face));
        }
    }

    [Fact]
    public void Delete_NoLiveVerticesReturnsNullAndLeavesMeshUnchanged()
    {
        using SpatialMesh mesh = BuildQuad(out _, out _, out _, out _, out _);

        Assert.Null(VertexDeletionBatch.Delete(ObjectId, mesh, [VertexHandle.Null]));
        Assert.Equal(4, CountLiveVertices(mesh));
        Assert.Equal(1, CountLiveFaces(mesh));
    }

    private static VertexDeletionBatch AssertDeleted(
        SpatialMesh mesh,
        params VertexHandle[] vertices
    )
    {
        VertexDeletionBatch? batch = VertexDeletionBatch.Delete(ObjectId, mesh, vertices);
        Assert.NotNull(batch);
        return batch;
    }

    private static SpatialMesh BuildQuad(
        out VertexHandle a,
        out VertexHandle b,
        out VertexHandle c,
        out VertexHandle d,
        out FaceHandle face
    )
    {
        SpatialMesh mesh = new();
        a = mesh.AddVertex(Vector3.Zero);
        b = mesh.AddVertex(Vector3.UnitX);
        c = mesh.AddVertex(Vector3.One);
        d = mesh.AddVertex(Vector3.UnitY);
        face = mesh.AddFace([a, b, c, d]);
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

        throw new InvalidOperationException();
    }

    private static int CountLiveVertices(SpatialMesh mesh)
    {
        int count = 0;
        foreach (VertexHandle _ in mesh.EnumerateLiveVertices())
            count++;
        return count;
    }

    private static int CountLiveEdges(SpatialMesh mesh)
    {
        int halfEdgeCount = 0;
        foreach (HalfEdgeHandle _ in mesh.EnumerateLiveHalfEdges())
            halfEdgeCount++;
        return halfEdgeCount / 2;
    }

    private static int CountLiveFaces(SpatialMesh mesh)
    {
        int count = 0;
        foreach (FaceHandle _ in mesh.EnumerateLiveFaces())
            count++;
        return count;
    }
}

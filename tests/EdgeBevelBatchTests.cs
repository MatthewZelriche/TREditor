using System.Numerics;
using TREditorSharp;
using TREditorSharp.Builders;

namespace TREditor2026.Tests;

public sealed class EdgeBevelBatchTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void Bevel_UndoRedoRestoresExactHandles()
    {
        using SpatialMesh mesh = BuildBox();
        HalfEdgeHandle edge = FindEdge(mesh, new(1, -1, 1), new(1, 1, 1));
        List<FaceHandle> originalFaces = [];
        foreach (FaceHandle face in mesh.EnumerateLiveFaces())
            originalFaces.Add(face);
        using EdgeBevelBatch batch = AssertBevel(mesh, [edge], 0.25f);
        FaceHandle bevelFace = Assert.Single(batch.BevelFaces);

        Assert.False(mesh.IsHalfEdgeAlive(edge));
        Assert.True(mesh.IsFaceAlive(bevelFace));

        batch.ApplyBefore();

        Assert.True(mesh.IsHalfEdgeAlive(edge));
        Assert.All(originalFaces, face => Assert.True(mesh.IsFaceAlive(face)));
        Assert.False(mesh.IsFaceAlive(bevelFace));

        batch.ApplyAfter();

        Assert.False(mesh.IsHalfEdgeAlive(edge));
        Assert.True(mesh.IsFaceAlive(bevelFace));
    }

    [Fact]
    public void TryGetMaximumWidth_RejectsEdgesSharingAnEndpoint()
    {
        using SpatialMesh mesh = BuildBox();
        HalfEdgeHandle first = FindEdge(mesh, new(1, -1, 1), new(1, 1, 1));
        HalfEdgeHandle second = FindEdge(mesh, new(1, -1, 1), new(-1, -1, 1));

        Assert.False(
            EdgeBevelBatch.TryGetMaximumWidth(mesh, [first, second], out float maximumWidth)
        );
        Assert.Equal(0f, maximumWidth);
    }

    [Fact]
    public void Bevel_AllowsMultipleNonTouchingEdgesInOnePatch()
    {
        using SpatialMesh mesh = BuildBox();
        HalfEdgeHandle first = FindEdge(mesh, new(1, -1, 1), new(1, 1, 1));
        HalfEdgeHandle second = FindEdge(mesh, new(-1, -1, -1), new(-1, 1, -1));

        using EdgeBevelBatch batch = AssertBevel(mesh, [first, second], 0.25f);

        Assert.Equal(2, batch.BevelFaces.Count);
        Assert.All(batch.BevelFaces, face => Assert.True(mesh.IsFaceAlive(face)));

        batch.ApplyBefore();

        Assert.True(mesh.IsHalfEdgeAlive(first));
        Assert.True(mesh.IsHalfEdgeAlive(second));
    }

    [Fact]
    public void Bevel_InitializedFacesProjectGeneratedUvs()
    {
        using SpatialMesh mesh = BuildBox();
        HalfEdgeHandle edge = FindEdge(mesh, new(1, -1, 1), new(1, 1, 1));
        FaceHandle sourceFace = mesh.GetHalfEdge(edge).Face;
        InitializeFaceUvs(mesh, sourceFace);

        using EdgeBevelBatch batch = AssertBevel(mesh, [edge], 0.25f);

        Assert.True(mesh.AreFaceUvsInitialized(Assert.Single(batch.BevelFaces)));
    }

    private static EdgeBevelBatch AssertBevel(SpatialMesh mesh, HalfEdgeHandle[] edges, float width)
    {
        EdgeBevelBatch? batch = EdgeBevelBatch.Bevel(ObjectId, mesh, edges, width);
        Assert.NotNull(batch);
        return batch;
    }

    private static SpatialMesh BuildBox() =>
        MeshBuilders.Build(new BlockOptions { Min = new(-1), Max = new(1) });

    private static HalfEdgeHandle FindEdge(SpatialMesh mesh, Vector3 origin, Vector3 destination)
    {
        foreach (HalfEdgeHandle edge in mesh.EnumerateLiveHalfEdges())
        {
            HalfEdge data = mesh.GetHalfEdge(edge);
            if (
                mesh.GetVertexPosition(data.Origin) == origin
                && mesh.GetVertexPosition(mesh.GetHalfEdge(data.Twin).Origin) == destination
            )
            {
                return edge;
            }
        }

        throw new InvalidOperationException("Expected edge was not found.");
    }

    private static void InitializeFaceUvs(SpatialMesh mesh, FaceHandle face)
    {
        List<ProjectedFaceCornerUv> projected = [];
        Assert.True(FaceUvProjector.TryProject(mesh, face, projected));
        foreach (ProjectedFaceCornerUv corner in projected)
            mesh.SetFaceCornerUv(corner.Corner, corner.Uv);
        mesh.SetFaceUvsInitialized(face, true);
    }
}

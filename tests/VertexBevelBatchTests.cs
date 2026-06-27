using System.Numerics;
using TREditorSharp;
using TREditorSharp.Builders;

namespace TREditor2026.Tests;

public sealed class VertexBevelBatchTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void Bevel_UndoRedoRestoresExactHandles()
    {
        using SpatialMesh mesh = BuildBox();
        VertexHandle vertex = FindVertex(mesh, new(1, 1, 1));
        List<FaceHandle> originalFaces = [];
        foreach (FaceHandle face in mesh.EnumerateLiveFaces())
            originalFaces.Add(face);
        using VertexBevelBatch batch = AssertBevel(mesh, [vertex], 0.25f);
        FaceHandle bevelFace = Assert.Single(batch.BevelFaces);

        Assert.False(mesh.IsVertexAlive(vertex));
        Assert.True(mesh.IsFaceAlive(bevelFace));

        batch.ApplyBefore();

        Assert.True(mesh.IsVertexAlive(vertex));
        Assert.All(originalFaces, face => Assert.True(mesh.IsFaceAlive(face)));
        Assert.False(mesh.IsFaceAlive(bevelFace));

        batch.ApplyAfter();

        Assert.False(mesh.IsVertexAlive(vertex));
        Assert.True(mesh.IsFaceAlive(bevelFace));
    }

    [Fact]
    public void TryGetMaximumWidth_RejectsAdjacentVertices()
    {
        using SpatialMesh mesh = BuildBox();
        VertexHandle first = FindVertex(mesh, new(1, 1, 1));
        VertexHandle second = FindVertex(mesh, new(-1, 1, 1));

        Assert.False(
            VertexBevelBatch.TryGetMaximumWidth(mesh, [first, second], out float maximumWidth)
        );
        Assert.Equal(0f, maximumWidth);
    }

    [Fact]
    public void Bevel_AllowsMultipleNonAdjacentVerticesInOnePatch()
    {
        using SpatialMesh mesh = BuildBox();
        VertexHandle first = FindVertex(mesh, new(1, 1, 1));
        // These vertices share the top face but not an edge.
        VertexHandle second = FindVertex(mesh, new(-1, 1, -1));

        using VertexBevelBatch batch = AssertBevel(mesh, [first, second], 0.25f);

        Assert.Equal(2, batch.BevelFaces.Count);
        Assert.All(batch.BevelFaces, face => Assert.True(mesh.IsFaceAlive(face)));

        batch.ApplyBefore();

        Assert.True(mesh.IsVertexAlive(first));
        Assert.True(mesh.IsVertexAlive(second));
    }

    [Fact]
    public void Bevel_InitializedFacesProjectGeneratedUvs()
    {
        using SpatialMesh mesh = BuildBox();
        VertexHandle vertex = FindVertex(mesh, new(1, 1, 1));
        foreach (HalfEdgeHandle edge in mesh.HalfEdgesAroundVertex(vertex))
            InitializeFaceUvs(mesh, mesh.GetHalfEdge(edge).Face);

        using VertexBevelBatch batch = AssertBevel(mesh, [vertex], 0.25f);

        Assert.True(mesh.AreFaceUvsInitialized(Assert.Single(batch.BevelFaces)));
    }

    private static VertexBevelBatch AssertBevel(
        SpatialMesh mesh,
        VertexHandle[] vertices,
        float width
    )
    {
        VertexBevelBatch? batch = VertexBevelBatch.Bevel(ObjectId, mesh, vertices, width);
        Assert.NotNull(batch);
        return batch;
    }

    private static SpatialMesh BuildBox() =>
        MeshBuilders.Build(new BlockOptions { Min = new(-1), Max = new(1) });

    private static VertexHandle FindVertex(SpatialMesh mesh, Vector3 position)
    {
        foreach (VertexHandle vertex in mesh.EnumerateLiveVertices())
        {
            if (mesh.GetVertexPosition(vertex) == position)
                return vertex;
        }

        throw new InvalidOperationException("Expected vertex was not found.");
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

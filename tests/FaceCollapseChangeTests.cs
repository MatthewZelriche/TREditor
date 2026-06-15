using System.Numerics;
using TREditorSharp;
using TREditorSharp.Builders;

namespace TREditor2026.Tests;

public sealed class FaceCollapseChangeTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void Collapse_MergesFaceVerticesAtCentroidAndUndoRedoRestoresExactHandles()
    {
        using SpatialMesh mesh = BuildQuad(out FaceHandle sourceFace, out VertexHandle[] vertices);
        Vector3 centroid = mesh.ComputeFaceCentroid(sourceFace);

        using FaceCollapseChange change = AssertCollapsed(mesh, sourceFace);

        Assert.False(mesh.IsFaceAlive(sourceFace));
        Assert.Equal(vertices[0], change.Survivor);
        Assert.Equal(centroid, mesh.GetVertexPosition(change.Survivor));
        Assert.Equal(1, CountLiveVertices(mesh));
        change.ApplyBefore();

        Assert.True(mesh.IsFaceAlive(sourceFace));
        Assert.All(vertices, vertex => Assert.True(mesh.IsVertexAlive(vertex)));
        Assert.Equal(4, CountLiveVertices(mesh));
        change.ApplyAfter();

        Assert.False(mesh.IsFaceAlive(sourceFace));
        Assert.Equal(centroid, mesh.GetVertexPosition(change.Survivor));
        Assert.Equal(1, CountLiveVertices(mesh));
    }

    [Fact]
    public void Collapse_BoxFacePreservesSurroundingFaces()
    {
        using SpatialMesh mesh = MeshBuilders.Build(
            new BlockOptions { Min = Vector3.Zero, Max = Vector3.One }
        );
        FaceHandle sourceFace = FindFace(mesh, face => mesh.ComputeFaceCentroid(face).Y == 1f);
        List<FaceHandle> surroundingFaces = [];
        foreach (FaceHandle face in mesh.EnumerateLiveFaces())
        {
            if (face != sourceFace)
                surroundingFaces.Add(face);
        }
        for (int i = 0; i < surroundingFaces.Count; i++)
        {
            mesh.SetFaceMaterialSlot(surroundingFaces[i], i + 1);
            InitializeProjectedUvs(mesh, surroundingFaces[i]);
        }
        Vector3 centroid = mesh.ComputeFaceCentroid(sourceFace);

        using FaceCollapseChange change = AssertCollapsed(mesh, sourceFace);

        Assert.False(mesh.IsFaceAlive(sourceFace));
        Assert.Equal(centroid, mesh.GetVertexPosition(change.Survivor));
        Assert.Equal(5, CountLiveFaces(mesh));
        Assert.Equal(5, CountLiveVertices(mesh));
        for (int i = 0; i < surroundingFaces.Count; i++)
        {
            Assert.True(mesh.IsFaceAlive(surroundingFaces[i]));
            Assert.Equal(i + 1, mesh.GetFaceMaterialSlot(surroundingFaces[i]));
            AssertProjectedUvs(mesh, surroundingFaces[i]);
        }
    }

    [Fact]
    public void CanCollapse_RestoresMeshAfterTestingOperation()
    {
        using SpatialMesh mesh = BuildQuad(out FaceHandle face, out VertexHandle[] vertices);
        Vector3[] positions = vertices.Select(mesh.GetVertexPosition).ToArray();

        Assert.True(FaceCollapseChange.CanCollapse(mesh, face));

        Assert.True(mesh.IsFaceAlive(face));
        Assert.All(vertices, vertex => Assert.True(mesh.IsVertexAlive(vertex)));
        Assert.Equal(positions, vertices.Select(mesh.GetVertexPosition));
    }

    [Fact]
    public void Collapse_DeadFaceReturnsNullWithoutMutation()
    {
        using SpatialMesh mesh = BuildQuad(out FaceHandle face, out _);
        Assert.True(mesh.RemoveFace(face));

        Assert.Null(FaceCollapseChange.Collapse(ObjectId, mesh, face));
        Assert.Equal(4, CountLiveVertices(mesh));
    }

    private static FaceCollapseChange AssertCollapsed(SpatialMesh mesh, FaceHandle face)
    {
        FaceCollapseChange? change = FaceCollapseChange.Collapse(ObjectId, mesh, face);
        Assert.NotNull(change);
        return change;
    }

    private static SpatialMesh BuildQuad(out FaceHandle face, out VertexHandle[] vertices)
    {
        SpatialMesh mesh = new();
        vertices =
        [
            mesh.AddVertex(Vector3.Zero),
            mesh.AddVertex(Vector3.UnitX),
            mesh.AddVertex(Vector3.One),
            mesh.AddVertex(Vector3.UnitY),
        ];
        face = mesh.AddFace(vertices);
        return mesh;
    }

    private static FaceHandle FindFace(SpatialMesh mesh, Func<FaceHandle, bool> predicate)
    {
        foreach (FaceHandle face in mesh.EnumerateLiveFaces())
        {
            if (predicate(face))
                return face;
        }

        throw new InvalidOperationException("Expected face was not found.");
    }

    private static void InitializeProjectedUvs(SpatialMesh mesh, FaceHandle face)
    {
        List<ProjectedFaceCornerUv> projected = [];
        Assert.True(FaceUvProjector.TryProject(mesh, face, projected));
        foreach (ProjectedFaceCornerUv corner in projected)
            mesh.SetFaceCornerUv(corner.Corner, corner.Uv);
        mesh.SetFaceUvsInitialized(face, true);
    }

    private static void AssertProjectedUvs(SpatialMesh mesh, FaceHandle face)
    {
        List<ProjectedFaceCornerUv> expected = [];
        Assert.True(FaceUvProjector.TryProject(mesh, face, expected));
        Assert.True(mesh.AreFaceUvsInitialized(face));
        Assert.Equal(
            expected.Select(corner => corner.Uv),
            expected.Select(corner => mesh.GetFaceCornerUv(corner.Corner))
        );
    }

    private static int CountLiveFaces(SpatialMesh mesh)
    {
        int count = 0;
        foreach (FaceHandle _ in mesh.EnumerateLiveFaces())
            count++;
        return count;
    }

    private static int CountLiveVertices(SpatialMesh mesh)
    {
        int count = 0;
        foreach (VertexHandle _ in mesh.EnumerateLiveVertices())
            count++;
        return count;
    }
}

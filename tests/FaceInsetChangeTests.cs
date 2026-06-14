using System.Numerics;
using TREditorSharp;

namespace TREditor2026.Tests;

public sealed class FaceInsetChangeTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void Inset_UndoRedoRestoresExactHandles()
    {
        using SpatialMesh mesh = BuildQuad(out FaceHandle sourceFace);
        using FaceInsetChange change = AssertInset(mesh, sourceFace, 0.25f);

        Assert.False(mesh.IsFaceAlive(sourceFace));
        Assert.True(mesh.IsFaceAlive(change.CapFace));
        Assert.Equal(4, change.RingFaces.Count);

        change.ApplyBefore();

        Assert.True(mesh.IsFaceAlive(sourceFace));
        Assert.False(mesh.IsFaceAlive(change.CapFace));

        change.ApplyAfter();

        Assert.False(mesh.IsFaceAlive(sourceFace));
        Assert.True(mesh.IsFaceAlive(change.CapFace));
    }

    [Fact]
    public void Inset_InitializedSourceProjectsGeneratedUvs()
    {
        using SpatialMesh mesh = BuildQuad(out FaceHandle sourceFace);
        List<ProjectedFaceCornerUv> sourceUvs = [];
        Assert.True(FaceUvProjector.TryProject(mesh, sourceFace, sourceUvs));
        foreach (ProjectedFaceCornerUv corner in sourceUvs)
            mesh.SetFaceCornerUv(corner.Corner, corner.Uv);
        mesh.SetFaceUvsInitialized(sourceFace, true);

        using FaceInsetChange change = AssertInset(mesh, sourceFace, 0.25f);

        Assert.All(
            change.RingFaces.Append(change.CapFace),
            face => Assert.True(mesh.AreFaceUvsInitialized(face))
        );
    }

    [Fact]
    public void Inset_DepthBeyondMaximumReturnsNullWithoutMutation()
    {
        using SpatialMesh mesh = BuildQuad(out FaceHandle sourceFace);

        Assert.Null(FaceInsetChange.Inset(ObjectId, mesh, sourceFace, 0.51f));
        Assert.True(mesh.IsFaceAlive(sourceFace));
    }

    private static FaceInsetChange AssertInset(SpatialMesh mesh, FaceHandle face, float depth)
    {
        FaceInsetChange? change = FaceInsetChange.Inset(ObjectId, mesh, face, depth);
        Assert.NotNull(change);
        return change;
    }

    private static SpatialMesh BuildQuad(out FaceHandle face)
    {
        SpatialMesh mesh = new();
        face = mesh.AddFace([
            mesh.AddVertex(Vector3.Zero),
            mesh.AddVertex(Vector3.UnitX),
            mesh.AddVertex(Vector3.UnitX + Vector3.UnitY),
            mesh.AddVertex(Vector3.UnitY),
        ]);
        return mesh;
    }
}

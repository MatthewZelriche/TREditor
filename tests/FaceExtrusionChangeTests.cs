using System.Numerics;
using TREditorSharp;

namespace TREditor2026.Tests;

public sealed class FaceExtrusionChangeTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void Extrude_TranslatesCapByFullDeltaAndUndoRedoRestoresExactHandles()
    {
        using SpatialMesh mesh = BuildQuad(
            out FaceHandle sourceFace,
            out Vector3[] sourcePositions
        );
        Vector3 delta = new(2, 3, 4);
        using FaceExtrusionChange change = AssertExtruded(mesh, sourceFace, delta);

        Assert.False(mesh.IsFaceAlive(sourceFace));
        Assert.True(mesh.IsFaceAlive(change.CapFace));
        Assert.Equal(5, CountLiveFaces(mesh));
        Assert.Equal(8, CountLiveVertices(mesh));
        Assert.Equal(
            sourcePositions.Select(position => position + delta),
            change.CapVertices.Select(mesh.GetVertexPosition)
        );

        change.ApplyBefore();

        Assert.True(mesh.IsFaceAlive(sourceFace));
        Assert.False(mesh.IsFaceAlive(change.CapFace));
        Assert.Equal(1, CountLiveFaces(mesh));
        Assert.Equal(4, CountLiveVertices(mesh));

        change.ApplyAfter();

        Assert.False(mesh.IsFaceAlive(sourceFace));
        Assert.True(mesh.IsFaceAlive(change.CapFace));
        Assert.Equal(
            sourcePositions.Select(position => position + delta),
            change.CapVertices.Select(mesh.GetVertexPosition)
        );
    }

    [Fact]
    public void Extrude_DeadFaceReturnsNullWithoutMutation()
    {
        using SpatialMesh mesh = BuildQuad(out FaceHandle face, out _);
        Assert.True(mesh.RemoveFace(face));

        Assert.Null(FaceExtrusionChange.Extrude(ObjectId, mesh, face, Vector3.UnitZ));
        Assert.Equal(0, CountLiveFaces(mesh));
        Assert.Equal(4, CountLiveVertices(mesh));
    }

    [Fact]
    public void Extrude_InitializedSourceProjectsUvsForCapAndSideFaces()
    {
        using SpatialMesh mesh = BuildQuad(out FaceHandle sourceFace, out _);
        mesh.SetFaceMaterialSlot(sourceFace, 7);
        InitializeProjectedUvs(mesh, sourceFace);
        Vector2[] sourceUvs = CaptureUvs(mesh, sourceFace);

        using FaceExtrusionChange change = AssertExtruded(mesh, sourceFace, new Vector3(0, 0, 2));

        Assert.Equal(7, mesh.GetFaceMaterialSlot(change.CapFace));
        Assert.All(
            change.SideFaces,
            face => Assert.Equal(SpatialMesh.UntexturedMaterialSlot, mesh.GetFaceMaterialSlot(face))
        );
        foreach (FaceHandle face in change.SideFaces.Append(change.CapFace))
        {
            Assert.True(mesh.AreFaceUvsInitialized(face));
            AssertProjectedUvs(mesh, face);
        }

        change.ApplyBefore();

        Assert.True(mesh.IsFaceAlive(sourceFace));
        Assert.Equal(7, mesh.GetFaceMaterialSlot(sourceFace));
        Assert.True(mesh.AreFaceUvsInitialized(sourceFace));
        Assert.Equal(sourceUvs, CaptureUvs(mesh, sourceFace));

        change.ApplyAfter();

        Assert.Equal(7, mesh.GetFaceMaterialSlot(change.CapFace));
        Assert.All(
            change.SideFaces,
            face => Assert.Equal(SpatialMesh.UntexturedMaterialSlot, mesh.GetFaceMaterialSlot(face))
        );
        foreach (FaceHandle face in change.SideFaces.Append(change.CapFace))
        {
            Assert.True(mesh.AreFaceUvsInitialized(face));
            AssertProjectedUvs(mesh, face);
        }
    }

    [Fact]
    public void Extrude_UninitializedSourceLeavesGeneratedUvsUninitialized()
    {
        using SpatialMesh mesh = BuildQuad(out FaceHandle sourceFace, out _);

        using FaceExtrusionChange change = AssertExtruded(mesh, sourceFace, new Vector3(0, 0, 2));

        Assert.All(
            change.SideFaces.Append(change.CapFace),
            face => Assert.False(mesh.AreFaceUvsInitialized(face))
        );
    }

    private static FaceExtrusionChange AssertExtruded(
        SpatialMesh mesh,
        FaceHandle face,
        Vector3 delta
    )
    {
        FaceExtrusionChange? change = FaceExtrusionChange.Extrude(ObjectId, mesh, face, delta);
        Assert.NotNull(change);
        return change;
    }

    private static SpatialMesh BuildQuad(out FaceHandle face, out Vector3[] positions)
    {
        SpatialMesh mesh = new();
        positions = [Vector3.Zero, Vector3.UnitX, Vector3.UnitX + Vector3.UnitY, Vector3.UnitY];
        VertexHandle[] vertices = positions.Select(mesh.AddVertex).ToArray();
        face = mesh.AddFace(vertices);
        return mesh;
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
        Assert.Equal(
            expected.Select(corner => corner.Uv),
            expected.Select(corner => mesh.GetFaceCornerUv(corner.Corner))
        );
    }

    private static Vector2[] CaptureUvs(SpatialMesh mesh, FaceHandle face)
    {
        List<Vector2> uvs = [];
        foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(face))
            uvs.Add(mesh.GetFaceCornerUv(corner));
        return uvs.ToArray();
    }
}

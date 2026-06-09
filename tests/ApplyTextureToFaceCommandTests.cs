using System.Numerics;
using TREditorSharp;

namespace TREditor2026.Tests;

public sealed class ApplyTextureToFaceCommandTests
{
    [Fact]
    public void FirstApplication_GeneratesAndStoresDefaultUvs()
    {
        using SpatialMesh mesh = BuildQuad();
        FaceHandle face = GetOnlyFace(mesh);

        FaceTextureChange? change = FaceTextureChange.Create(mesh, face, 4);
        Assert.NotNull(change);
        change.Apply(mesh);

        Assert.Equal(4, mesh.GetFaceMaterialSlot(face));
        Assert.True(mesh.AreFaceUvsInitialized(face));
        Assert.Equal(
            new Vector2[] { Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY },
            GetCornerUvs(mesh, face)
        );
    }

    [Fact]
    public void Replacement_PreservesExistingUvs()
    {
        using SpatialMesh mesh = BuildQuad();
        FaceHandle face = GetOnlyFace(mesh);
        SetTextureState(mesh, face, 2, true, [new(2, 3), new(4, 3), new(4, 7), new(2, 7)]);
        Vector2[] expectedUvs = GetCornerUvs(mesh, face);

        FaceTextureChange? change = FaceTextureChange.Create(mesh, face, 9);
        Assert.NotNull(change);
        change.Apply(mesh);

        Assert.Equal(9, mesh.GetFaceMaterialSlot(face));
        Assert.True(mesh.AreFaceUvsInitialized(face));
        Assert.Equal(expectedUvs, GetCornerUvs(mesh, face));
    }

    [Fact]
    public void Undo_RestoresPreviousMaterialUvStateAndCornerUvs()
    {
        using SpatialMesh mesh = BuildQuad();
        FaceHandle face = GetOnlyFace(mesh);
        Vector2[] previousUvs = [new(10, 11), new(12, 11), new(12, 13), new(10, 13)];
        SetTextureState(mesh, face, 0, false, previousUvs);
        FaceTextureChange? change = FaceTextureChange.Create(mesh, face, 3);
        Assert.NotNull(change);
        change.Apply(mesh);

        change.Revert(mesh);

        Assert.Equal(0, mesh.GetFaceMaterialSlot(face));
        Assert.False(mesh.AreFaceUvsInitialized(face));
        Assert.Equal(previousUvs, GetCornerUvs(mesh, face));
    }

    [Fact]
    public void Redo_ReusesStoredProjectionInsteadOfRecalculating()
    {
        using SpatialMesh mesh = BuildQuad();
        FaceHandle face = GetOnlyFace(mesh);
        FaceTextureChange? change = FaceTextureChange.Create(mesh, face, 6);
        Assert.NotNull(change);
        change.Apply(mesh);
        Vector2[] generatedUvs = GetCornerUvs(mesh, face);
        change.Revert(mesh);

        foreach (VertexHandle vertex in mesh.EnumerateLiveVertices())
            mesh.SetVertexPosition(vertex, mesh.GetVertexPosition(vertex) * 10);
        change.Apply(mesh);

        Assert.Equal(generatedUvs, GetCornerUvs(mesh, face));
    }

    [Fact]
    public void Create_DegenerateFaceReturnsNullWithoutMutatingMesh()
    {
        using SpatialMesh mesh = BuildFace(Vector3.Zero, Vector3.UnitX, Vector3.UnitX * 2);
        FaceHandle face = GetOnlyFace(mesh);

        Assert.Null(FaceTextureChange.Create(mesh, face, 1));
        Assert.Equal(0, mesh.GetFaceMaterialSlot(face));
        Assert.False(mesh.AreFaceUvsInitialized(face));
    }

    [Fact]
    public void Create_AlreadyAppliedMaterialReturnsNull()
    {
        using SpatialMesh mesh = BuildQuad();
        FaceHandle face = GetOnlyFace(mesh);
        SetTextureState(mesh, face, 5, true, [new(2, 3), new(4, 3), new(4, 7), new(2, 7)]);

        Assert.Null(FaceTextureChange.Create(mesh, face, 5));
    }

    [Fact]
    public void StoredStatesExposeReadOnlyCornerUvSnapshots()
    {
        using SpatialMesh mesh = BuildQuad();
        FaceHandle face = GetOnlyFace(mesh);
        FaceTextureChange? change = FaceTextureChange.Create(mesh, face, 1);

        Assert.NotNull(change);
        Assert.IsAssignableFrom<System.Collections.ObjectModel.ReadOnlyCollection<FaceCornerUvState>>(
            change.Before.CornerUvs
        );
        Assert.IsAssignableFrom<System.Collections.ObjectModel.ReadOnlyCollection<FaceCornerUvState>>(
            change.After.CornerUvs
        );
    }

    private static SpatialMesh BuildQuad() =>
        BuildFace(Vector3.Zero, Vector3.UnitX, Vector3.UnitX - Vector3.UnitZ, -Vector3.UnitZ);

    private static SpatialMesh BuildFace(params Vector3[] positions)
    {
        SpatialMesh mesh = new();
        mesh.AddFace(positions.Select(mesh.AddVertex).ToArray());
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

    private static Vector2[] GetCornerUvs(SpatialMesh mesh, FaceHandle face)
    {
        List<Vector2> uvs = [];
        foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(face))
            uvs.Add(mesh.GetFaceCornerUv(corner));
        return uvs.ToArray();
    }

    private static void SetTextureState(
        SpatialMesh mesh,
        FaceHandle face,
        int materialSlot,
        bool initialized,
        IReadOnlyList<Vector2> uvs
    )
    {
        int index = 0;
        foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(face))
            mesh.SetFaceCornerUv(corner, uvs[index++]);
        mesh.SetFaceMaterialSlot(face, materialSlot);
        mesh.SetFaceUvsInitialized(face, initialized);
    }
}

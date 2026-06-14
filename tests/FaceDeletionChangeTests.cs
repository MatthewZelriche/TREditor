using System.Numerics;
using TREditorSharp;

namespace TREditor2026.Tests;

public sealed class FaceDeletionChangeTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void DeleteAndRestore_PreservesFaceTextureState()
    {
        using SpatialMesh mesh = BuildQuad();
        FaceHandle face = GetOnlyFace(mesh);
        Vector2[] expectedUvs = [new(1, 2), new(3, 4), new(5, 6), new(7, 8)];
        int index = 0;
        foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(face))
            mesh.SetFaceCornerUv(corner, expectedUvs[index++]);
        mesh.SetFaceMaterialSlot(face, 7);
        mesh.SetFaceUvsInitialized(face, true);
        FaceDeletionChange? captured = FaceDeletionChange.Capture(ObjectId, mesh, face);
        Assert.NotNull(captured);
        FaceDeletionChange change = captured;

        Assert.True(change.Delete(mesh));
        change.Restore(mesh);

        Assert.Equal(1, CountFaces(mesh));
        Assert.Equal(7, mesh.GetFaceMaterialSlot(change.Face));
        Assert.True(mesh.AreFaceUvsInitialized(change.Face));
        Assert.Equal(expectedUvs, GetUvs(mesh, change.Face));
    }

    [Fact]
    public void DeleteAndRestore_CanRepeatForRedo()
    {
        using SpatialMesh mesh = BuildQuad();
        FaceDeletionChange? captured = FaceDeletionChange.Capture(
            ObjectId,
            mesh,
            GetOnlyFace(mesh)
        );
        Assert.NotNull(captured);
        FaceDeletionChange change = captured;

        Assert.True(change.Delete(mesh));
        change.Restore(mesh);
        Assert.True(change.Delete(mesh));
        change.Restore(mesh);

        Assert.Equal(1, CountFaces(mesh));
    }

    [Fact]
    public void DeleteAdjacentFaces_RestoreInReversePreservesBothFaces()
    {
        using SpatialMesh mesh = BuildTwoAdjacentFaces();
        List<FaceHandle> faces = [];
        foreach (FaceHandle face in mesh.EnumerateLiveFaces())
            faces.Add(face);
        mesh.SetFaceMaterialSlot(faces[0], 3);
        mesh.SetFaceMaterialSlot(faces[1], 5);
        FaceDeletionChange[] changes =
        [
            AssertCaptured(mesh, faces[0]),
            AssertCaptured(mesh, faces[1]),
        ];

        Assert.True(changes[0].Delete(mesh));
        Assert.True(changes[1].Delete(mesh));
        changes[1].Restore(mesh);
        changes[0].Restore(mesh);

        Assert.Equal(2, CountFaces(mesh));
        Assert.Equal(3, mesh.GetFaceMaterialSlot(changes[0].Face));
        Assert.Equal(5, mesh.GetFaceMaterialSlot(changes[1].Face));
    }

    private static SpatialMesh BuildQuad()
    {
        SpatialMesh mesh = new();
        mesh.AddFace(
            [
                mesh.AddVertex(Vector3.Zero),
                mesh.AddVertex(Vector3.UnitX),
                mesh.AddVertex(Vector3.One),
                mesh.AddVertex(Vector3.UnitY),
            ]
        );
        return mesh;
    }

    private static SpatialMesh BuildTwoAdjacentFaces()
    {
        SpatialMesh mesh = new();
        VertexHandle a = mesh.AddVertex(Vector3.Zero);
        VertexHandle b = mesh.AddVertex(Vector3.UnitX);
        VertexHandle c = mesh.AddVertex(Vector3.One);
        VertexHandle d = mesh.AddVertex(Vector3.UnitY);
        mesh.AddFace([a, c, b]);
        mesh.AddFace([a, d, c]);
        return mesh;
    }

    private static FaceDeletionChange AssertCaptured(SpatialMesh mesh, FaceHandle face)
    {
        FaceDeletionChange? captured = FaceDeletionChange.Capture(ObjectId, mesh, face);
        Assert.NotNull(captured);
        return captured;
    }

    private static FaceHandle GetOnlyFace(SpatialMesh mesh)
    {
        FaceHandle face = default;
        Assert.Equal(1, CountFaces(mesh, found => face = found));
        return face;
    }

    private static int CountFaces(SpatialMesh mesh, Action<FaceHandle>? visit = null)
    {
        int count = 0;
        foreach (FaceHandle face in mesh.EnumerateLiveFaces())
        {
            visit?.Invoke(face);
            count++;
        }
        return count;
    }

    private static Vector2[] GetUvs(SpatialMesh mesh, FaceHandle face)
    {
        List<Vector2> uvs = [];
        foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(face))
            uvs.Add(mesh.GetFaceCornerUv(corner));
        return uvs.ToArray();
    }
}

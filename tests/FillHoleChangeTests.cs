using System.Numerics;
using TREditorSharp;

namespace TREditor2026.Tests;

public sealed class FillHoleChangeTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void Fill_UndoRedoRestoresExactFaceHandle()
    {
        using SpatialMesh mesh = BuildTriangle(out FaceHandle original, out HalfEdgeHandle edge);
        Assert.True(mesh.RemoveFace(original));
        using FillHoleChange change = AssertFilled(mesh, edge);

        Assert.True(mesh.IsFaceAlive(change.Face));

        change.ApplyBefore();

        Assert.False(mesh.IsFaceAlive(change.Face));

        change.ApplyAfter();

        Assert.True(mesh.IsFaceAlive(change.Face));
    }

    [Fact]
    public void Fill_NewFaceIsUntexturedWithProjectedUvs()
    {
        using SpatialMesh mesh = BuildTriangle(out _, out HalfEdgeHandle edge);

        using FillHoleChange change = AssertFilled(mesh, edge);

        Assert.Equal(SpatialMesh.UntexturedMaterialSlot, mesh.GetFaceMaterialSlot(change.Face));
        Assert.True(mesh.AreFaceUvsInitialized(change.Face));
    }

    private static FillHoleChange AssertFilled(SpatialMesh mesh, HalfEdgeHandle edge)
    {
        FillHoleChange? change = FillHoleChange.Fill(ObjectId, mesh, edge);
        Assert.NotNull(change);
        return change;
    }

    private static SpatialMesh BuildTriangle(out FaceHandle face, out HalfEdgeHandle edge)
    {
        SpatialMesh mesh = new();
        face = mesh.AddFace(
            [
                mesh.AddVertex(Vector3.Zero),
                mesh.AddVertex(Vector3.UnitX),
                mesh.AddVertex(Vector3.UnitY),
            ]
        );
        foreach (HalfEdgeHandle faceEdge in mesh.HalfEdgesAroundFace(face))
        {
            edge = faceEdge;
            return mesh;
        }

        throw new InvalidOperationException();
    }
}

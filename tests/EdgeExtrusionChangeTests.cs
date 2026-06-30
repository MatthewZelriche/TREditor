using System.Numerics;
using TREditorSharp;

namespace TREditor2026.Tests;

public sealed class EdgeExtrusionChangeTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void Extrude_CreatesTranslatedQuadAndUndoRedoRestoresExactHandles()
    {
        using SpatialMesh mesh = BuildQuad(out _, out HalfEdgeHandle sourceEdge);
        HalfEdge source = mesh.GetHalfEdge(sourceEdge);
        Vector3[] expectedPositions =
        [
            mesh.GetVertexPosition(source.Origin) + Vector3.UnitZ,
            mesh.GetVertexPosition(mesh.GetHalfEdge(source.Twin).Origin) + Vector3.UnitZ,
        ];

        using EdgeExtrusionChange change = AssertExtruded(mesh, sourceEdge, Vector3.UnitZ);

        Assert.True(mesh.IsFaceAlive(change.Face));
        Assert.True(mesh.IsHalfEdgeAlive(change.OuterEdge));
        Assert.Equal(2, CountFaces(mesh));
        Assert.Equal(
            expectedPositions.OrderBy(position => position.X),
            change.NewVertices.Select(mesh.GetVertexPosition).OrderBy(position => position.X)
        );

        change.ApplyBefore();

        Assert.False(mesh.IsFaceAlive(change.Face));
        Assert.False(mesh.IsHalfEdgeAlive(change.OuterEdge));
        Assert.Equal(1, CountFaces(mesh));
        Assert.Equal(4, CountVertices(mesh));

        change.ApplyAfter();

        Assert.True(mesh.IsFaceAlive(change.Face));
        Assert.True(mesh.IsHalfEdgeAlive(change.OuterEdge));
        Assert.Equal(2, CountFaces(mesh));
        Assert.Equal(6, CountVertices(mesh));
    }

    [Fact]
    public void Extrude_InteriorEdgeReturnsNullWithoutMutation()
    {
        using SpatialMesh mesh = BuildAdjacentTriangles(out HalfEdgeHandle shared);

        Assert.Null(EdgeExtrusionChange.Extrude(ObjectId, mesh, shared, Vector3.UnitZ));
        Assert.Equal(2, CountFaces(mesh));
        Assert.Equal(4, CountVertices(mesh));
    }

    [Fact]
    public void Extrude_InitializedAdjacentFaceProjectsUvsForNewFace()
    {
        using SpatialMesh mesh = BuildQuad(
            out FaceHandle sourceFace,
            out HalfEdgeHandle sourceEdge
        );
        mesh.SetFaceMaterialSlot(sourceFace, 9);
        InitializeProjectedUvs(mesh, sourceFace);

        using EdgeExtrusionChange change = AssertExtruded(mesh, sourceEdge, Vector3.UnitZ);

        Assert.Equal(9, mesh.GetFaceMaterialSlot(change.Face));
        Assert.True(mesh.AreFaceUvsInitialized(change.Face));
        List<ProjectedFaceCornerUv> expected = [];
        Assert.True(FaceUvProjector.TryProject(mesh, change.Face, expected));
        Assert.All(
            expected,
            corner => Assert.Equal(corner.Uv, mesh.GetFaceCornerUv(corner.Corner))
        );
    }

    private static EdgeExtrusionChange AssertExtruded(
        SpatialMesh mesh,
        HalfEdgeHandle edge,
        Vector3 delta
    )
    {
        EdgeExtrusionChange? change = EdgeExtrusionChange.Extrude(ObjectId, mesh, edge, delta);
        Assert.NotNull(change);
        return change;
    }

    private static SpatialMesh BuildQuad(out FaceHandle face, out HalfEdgeHandle edge)
    {
        SpatialMesh mesh = new();
        VertexHandle a = mesh.AddVertex(Vector3.Zero);
        VertexHandle b = mesh.AddVertex(Vector3.UnitX);
        VertexHandle c = mesh.AddVertex(Vector3.UnitX + Vector3.UnitY);
        VertexHandle d = mesh.AddVertex(Vector3.UnitY);
        face = mesh.AddFace([a, b, c, d]);
        edge = HalfEdgeHandle.Null;
        foreach (HalfEdgeHandle faceEdge in mesh.HalfEdgesAroundFace(face))
        {
            edge = faceEdge;
            break;
        }
        return mesh;
    }

    private static SpatialMesh BuildAdjacentTriangles(out HalfEdgeHandle shared)
    {
        SpatialMesh mesh = new();
        VertexHandle a = mesh.AddVertex(Vector3.Zero);
        VertexHandle b = mesh.AddVertex(Vector3.UnitX);
        VertexHandle c = mesh.AddVertex(Vector3.UnitY);
        VertexHandle d = mesh.AddVertex(-Vector3.UnitY);
        FaceHandle first = mesh.AddFace([a, b, c]);
        mesh.AddFace([b, a, d]);
        shared = HalfEdgeHandle.Null;
        foreach (HalfEdgeHandle candidate in mesh.HalfEdgesAroundFace(first))
        {
            if (
                mesh.GetHalfEdge(candidate).Origin == a
                && mesh.GetHalfEdge(mesh.GetHalfEdge(candidate).Twin).Origin == b
            )
            {
                shared = candidate;
                break;
            }
        }
        return mesh;
    }

    private static void InitializeProjectedUvs(SpatialMesh mesh, FaceHandle face)
    {
        List<ProjectedFaceCornerUv> projected = [];
        Assert.True(FaceUvProjector.TryProject(mesh, face, projected));
        foreach (ProjectedFaceCornerUv corner in projected)
            mesh.SetFaceCornerUv(corner.Corner, corner.Uv);
        mesh.SetFaceUvsInitialized(face, true);
    }

    private static int CountFaces(SpatialMesh mesh)
    {
        int count = 0;
        foreach (FaceHandle _ in mesh.EnumerateLiveFaces())
            count++;
        return count;
    }

    private static int CountVertices(SpatialMesh mesh)
    {
        int count = 0;
        foreach (VertexHandle _ in mesh.EnumerateLiveVertices())
            count++;
        return count;
    }
}

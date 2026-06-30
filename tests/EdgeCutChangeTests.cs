using System.Numerics;
using TREditorSharp;

namespace TREditor2026.Tests;

public sealed class EdgeCutChangeTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void Cut_InsertsTwoVerticesAndSplitsFace()
    {
        using SpatialMesh mesh = BuildQuad(out FaceHandle sourceFace);
        FaceCornerHandle[] corners = CollectCorners(mesh, sourceFace);

        using EdgeCutChange change = AssertCut(
            mesh,
            sourceFace,
            corners[0],
            0.25f,
            corners[2],
            0.5f
        );

        Assert.False(mesh.IsFaceAlive(sourceFace));
        Assert.Equal(2, change.Faces.Count);
        Assert.All(change.Faces, face => Assert.True(mesh.IsFaceAlive(face)));
        Assert.Equal(6, CountVertices(mesh));
        Assert.Equal(new Vector3(0.5f, 0f, 0f), mesh.GetVertexPosition(change.FirstVertex));
        Assert.Equal(new Vector3(1f, 2f, 0f), mesh.GetVertexPosition(change.SecondVertex));
        AssertCutEdgeConnectsInsertedVertices(mesh, change);
    }

    [Fact]
    public void Cut_AllowsAdjacentEdgesOfTriangle()
    {
        using SpatialMesh mesh = BuildTriangle(out FaceHandle sourceFace);
        FaceCornerHandle[] corners = CollectCorners(mesh, sourceFace);

        using EdgeCutChange change = AssertCut(
            mesh,
            sourceFace,
            corners[0],
            0.5f,
            corners[1],
            0.5f
        );

        Assert.Equal(2, change.Faces.Count);
        AssertCutEdgeConnectsInsertedVertices(mesh, change);
    }

    [Fact]
    public void Cut_UsesExistingVerticesForBothEndpoints()
    {
        using SpatialMesh mesh = BuildQuad(out FaceHandle sourceFace);
        FaceCornerHandle[] corners = CollectCorners(mesh, sourceFace);
        VertexHandle first = mesh.GetHalfEdge(corners[0]).Origin;
        VertexHandle second = mesh.GetHalfEdge(corners[2]).Origin;

        using EdgeCutChange change = AssertCut(mesh, sourceFace, corners[0], 0f, corners[2], 0f);

        Assert.Equal(4, CountVertices(mesh));
        Assert.Equal(first, change.FirstVertex);
        Assert.Equal(second, change.SecondVertex);
        AssertCutEdgeConnectsInsertedVertices(mesh, change);

        change.ApplyBefore();

        Assert.True(mesh.IsVertexAlive(first));
        Assert.True(mesh.IsVertexAlive(second));
    }

    [Fact]
    public void Cut_UsesExistingVertexForEitherEndpoint()
    {
        using SpatialMesh firstMesh = BuildQuad(out FaceHandle firstFace);
        FaceCornerHandle[] firstCorners = CollectCorners(firstMesh, firstFace);
        VertexHandle existingFirst = firstMesh.GetHalfEdge(firstCorners[0]).Origin;

        using EdgeCutChange firstChange = AssertCut(
            firstMesh,
            firstFace,
            firstCorners[0],
            0f,
            firstCorners[2],
            0.5f
        );

        Assert.Equal(existingFirst, firstChange.FirstVertex);
        Assert.Equal(5, CountVertices(firstMesh));

        using SpatialMesh secondMesh = BuildQuad(out FaceHandle secondFace);
        FaceCornerHandle[] secondCorners = CollectCorners(secondMesh, secondFace);
        VertexHandle existingSecond = secondMesh.GetHalfEdge(secondCorners[2]).Origin;

        using EdgeCutChange secondChange = AssertCut(
            secondMesh,
            secondFace,
            secondCorners[0],
            0.5f,
            secondCorners[2],
            0f
        );

        Assert.Equal(existingSecond, secondChange.SecondVertex);
        Assert.Equal(5, CountVertices(secondMesh));
    }

    [Fact]
    public void Cut_SplitsSharedBoundaryInNeighborFace()
    {
        using SpatialMesh mesh = BuildAdjacentQuads(
            out FaceHandle sourceFace,
            out FaceHandle neighborFace
        );
        FaceCornerHandle[] corners = CollectCorners(mesh, sourceFace);

        using EdgeCutChange change = AssertCut(
            mesh,
            sourceFace,
            corners[1],
            0.5f,
            corners[3],
            0.5f
        );

        Assert.True(mesh.IsFaceAlive(neighborFace));
        Assert.Contains(change.FirstVertex, CollectFaceVertices(mesh, neighborFace));
    }

    [Fact]
    public void Cut_PreservesMaterialAndInterpolatedUvs()
    {
        using SpatialMesh mesh = BuildQuad(out FaceHandle sourceFace);
        foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(sourceFace))
        {
            Vector3 position = mesh.GetVertexPosition(mesh.GetHalfEdge(corner).Origin);
            mesh.SetFaceCornerUv(corner, new Vector2(position.X, position.Y));
        }
        mesh.SetFaceMaterialSlot(sourceFace, 7);
        mesh.SetFaceUvsInitialized(sourceFace, true);
        FaceCornerHandle[] corners = CollectCorners(mesh, sourceFace);

        using EdgeCutChange change = AssertCut(
            mesh,
            sourceFace,
            corners[0],
            0.25f,
            corners[2],
            0.5f
        );

        foreach (FaceHandle face in change.Faces)
        {
            Assert.Equal(7, mesh.GetFaceMaterialSlot(face));
            Assert.True(mesh.AreFaceUvsInitialized(face));
            foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(face))
            {
                Vector3 position = mesh.GetVertexPosition(mesh.GetHalfEdge(corner).Origin);
                Assert.Equal(new Vector2(position.X, position.Y), mesh.GetFaceCornerUv(corner));
            }
        }
    }

    [Fact]
    public void Cut_UndoRedoRestoresExactHandles()
    {
        using SpatialMesh mesh = BuildQuad(out FaceHandle sourceFace);
        FaceCornerHandle[] corners = CollectCorners(mesh, sourceFace);
        using EdgeCutChange change = AssertCut(
            mesh,
            sourceFace,
            corners[0],
            0.25f,
            corners[2],
            0.5f
        );

        change.ApplyBefore();

        Assert.True(mesh.IsFaceAlive(sourceFace));
        Assert.All(change.Faces, face => Assert.False(mesh.IsFaceAlive(face)));
        Assert.False(mesh.IsVertexAlive(change.FirstVertex));
        Assert.False(mesh.IsVertexAlive(change.SecondVertex));

        change.ApplyAfter();

        Assert.False(mesh.IsFaceAlive(sourceFace));
        Assert.All(change.Faces, face => Assert.True(mesh.IsFaceAlive(face)));
        Assert.True(mesh.IsHalfEdgeAlive(change.CutEdge));
    }

    [Fact]
    public void CanCut_AcceptsNonAdjacentVerticesAndRejectsInvalidEndpoints()
    {
        using SpatialMesh mesh = BuildQuad(out FaceHandle sourceFace);
        FaceCornerHandle[] corners = CollectCorners(mesh, sourceFace);

        Assert.False(EdgeCutChange.CanCut(mesh, sourceFace, corners[0], 0.5f, corners[0], 0.5f));
        Assert.True(EdgeCutChange.CanCut(mesh, sourceFace, corners[0], 0f, corners[2], 0f));
        Assert.False(EdgeCutChange.CanCut(mesh, sourceFace, corners[0], 0f, corners[1], 0f));
        Assert.False(EdgeCutChange.CanCut(mesh, sourceFace, corners[0], 0f, corners[0], 1f));
        Assert.True(EdgeCutChange.CanCut(mesh, sourceFace, corners[0], 0.5f, corners[2], 0.5f));
    }

    private static EdgeCutChange AssertCut(
        SpatialMesh mesh,
        FaceHandle face,
        HalfEdgeHandle firstEdge,
        float firstParameter,
        HalfEdgeHandle secondEdge,
        float secondParameter
    )
    {
        EdgeCutChange? change = EdgeCutChange.Cut(
            ObjectId,
            mesh,
            face,
            firstEdge,
            firstParameter,
            secondEdge,
            secondParameter
        );
        Assert.NotNull(change);
        return change;
    }

    private static SpatialMesh BuildQuad(out FaceHandle face)
    {
        SpatialMesh mesh = new();
        VertexHandle a = mesh.AddVertex(new Vector3(0f, 0f, 0f));
        VertexHandle b = mesh.AddVertex(new Vector3(2f, 0f, 0f));
        VertexHandle c = mesh.AddVertex(new Vector3(2f, 2f, 0f));
        VertexHandle d = mesh.AddVertex(new Vector3(0f, 2f, 0f));
        face = mesh.AddFace([a, b, c, d]);
        return mesh;
    }

    private static SpatialMesh BuildAdjacentQuads(
        out FaceHandle sourceFace,
        out FaceHandle neighborFace
    )
    {
        SpatialMesh mesh = new();
        VertexHandle a = mesh.AddVertex(new Vector3(0f, 0f, 0f));
        VertexHandle b = mesh.AddVertex(new Vector3(1f, 0f, 0f));
        VertexHandle c = mesh.AddVertex(new Vector3(1f, 1f, 0f));
        VertexHandle d = mesh.AddVertex(new Vector3(0f, 1f, 0f));
        VertexHandle e = mesh.AddVertex(new Vector3(2f, 0f, 0f));
        VertexHandle f = mesh.AddVertex(new Vector3(2f, 1f, 0f));
        sourceFace = mesh.AddFace([a, b, c, d]);
        neighborFace = mesh.AddFace([b, e, f, c]);
        return mesh;
    }

    private static SpatialMesh BuildTriangle(out FaceHandle face)
    {
        SpatialMesh mesh = new();
        VertexHandle a = mesh.AddVertex(new Vector3(0f, 0f, 0f));
        VertexHandle b = mesh.AddVertex(new Vector3(2f, 0f, 0f));
        VertexHandle c = mesh.AddVertex(new Vector3(1f, 2f, 0f));
        face = mesh.AddFace([a, b, c]);
        return mesh;
    }

    private static void AssertCutEdgeConnectsInsertedVertices(
        SpatialMesh mesh,
        EdgeCutChange change
    )
    {
        HalfEdge cut = mesh.GetHalfEdge(change.CutEdge);
        HalfEdge twin = mesh.GetHalfEdge(cut.Twin);
        Assert.Equal(change.FirstVertex, cut.Origin);
        Assert.Equal(change.SecondVertex, twin.Origin);
    }

    private static FaceCornerHandle[] CollectCorners(SpatialMesh mesh, FaceHandle face)
    {
        List<FaceCornerHandle> corners = [];
        foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(face))
            corners.Add(corner);
        return corners.ToArray();
    }

    private static List<VertexHandle> CollectFaceVertices(SpatialMesh mesh, FaceHandle face)
    {
        List<VertexHandle> vertices = [];
        foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(face))
            vertices.Add(mesh.GetHalfEdge(corner).Origin);
        return vertices;
    }

    private static int CountVertices(SpatialMesh mesh)
    {
        int count = 0;
        foreach (VertexHandle _ in mesh.EnumerateLiveVertices())
            count++;
        return count;
    }
}

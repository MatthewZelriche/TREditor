using System.Numerics;
using TREditorSharp;

namespace TREditor2026.Tests;

public sealed class BridgeEdgesChangeTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void Bridge_UndoRedoRestoresExactFaceHandles()
    {
        using SpatialMesh mesh = BuildWalls(out HalfEdgeHandle first, out HalfEdgeHandle second);
        using BridgeEdgesChange change = AssertBridge(mesh, first, second, 4, 180f);
        FaceHandle[] faces = change.Faces.ToArray();

        Assert.Equal(4, faces.Length);
        Assert.All(faces, face => Assert.True(mesh.IsFaceAlive(face)));

        change.ApplyBefore();

        Assert.All(faces, face => Assert.False(mesh.IsFaceAlive(face)));
        Assert.True(mesh.IsHalfEdgeAlive(first));
        Assert.True(mesh.IsHalfEdgeAlive(second));

        change.ApplyAfter();

        Assert.All(faces, face => Assert.True(mesh.IsFaceAlive(face)));
    }

    [Fact]
    public void Bridge_InitializedSourceProjectsGeneratedUvs()
    {
        using SpatialMesh mesh = BuildWalls(out HalfEdgeHandle first, out HalfEdgeHandle second);
        FaceHandle sourceFace = GetLiveFace(mesh, first);
        Assert.True(FaceUvProjector.TryProjectAndApply(mesh, sourceFace));

        using BridgeEdgesChange change = AssertBridge(mesh, first, second, 2, 90f);

        Assert.All(change.Faces, face => Assert.True(mesh.AreFaceUvsInitialized(face)));
    }

    private static BridgeEdgesChange AssertBridge(
        SpatialMesh mesh,
        HalfEdgeHandle first,
        HalfEdgeHandle second,
        int segments,
        float angle
    )
    {
        BridgeEdgesChange? change = BridgeEdgesChange.Bridge(
            ObjectId,
            mesh,
            first,
            second,
            segments,
            angle
        );
        Assert.NotNull(change);
        return change;
    }

    private static SpatialMesh BuildWalls(out HalfEdgeHandle firstTop, out HalfEdgeHandle secondTop)
    {
        SpatialMesh mesh = new();
        VertexHandle a = mesh.AddVertex(new Vector3(-1, 0, 0));
        VertexHandle b = mesh.AddVertex(new Vector3(-1, 0, 2));
        VertexHandle c = mesh.AddVertex(new Vector3(-1, 2, 2));
        VertexHandle d = mesh.AddVertex(new Vector3(-1, 2, 0));
        mesh.AddFace([a, b, c, d]);

        VertexHandle e = mesh.AddVertex(new Vector3(1, 0, 2));
        VertexHandle f = mesh.AddVertex(new Vector3(1, 0, 0));
        VertexHandle g = mesh.AddVertex(new Vector3(1, 2, 0));
        VertexHandle h = mesh.AddVertex(new Vector3(1, 2, 2));
        mesh.AddFace([e, f, g, h]);

        firstTop = FindEdge(mesh, d, c);
        secondTop = FindEdge(mesh, h, g);
        return mesh;
    }

    private static HalfEdgeHandle FindEdge(
        SpatialMesh mesh,
        VertexHandle first,
        VertexHandle second
    )
    {
        foreach (HalfEdgeHandle edge in mesh.EnumerateLiveHalfEdges())
        {
            HalfEdge data = mesh.GetHalfEdge(edge);
            VertexHandle destination = mesh.GetHalfEdge(data.Twin).Origin;
            if (
                (data.Origin == first && destination == second)
                || (data.Origin == second && destination == first)
            )
                return edge;
        }
        throw new InvalidOperationException();
    }

    private static FaceHandle GetLiveFace(SpatialMesh mesh, HalfEdgeHandle edge)
    {
        HalfEdge data = mesh.GetHalfEdge(edge);
        return mesh.IsFaceAlive(data.Face) ? data.Face : mesh.GetHalfEdge(data.Twin).Face;
    }
}

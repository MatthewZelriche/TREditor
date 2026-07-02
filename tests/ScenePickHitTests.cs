using Godot;

namespace TREditor2026.Tests;

public class ScenePickHitTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void None_HasNoHit()
    {
        ScenePickHit hit = ScenePickHit.None;

        Assert.False(hit.HasHit);
        Assert.Equal(ScenePickElementKind.None, hit.Kind);
        Assert.Equal(default, hit.ObjectId);
    }

    [Fact]
    public void ObjectHit_SetsKindAndDistance()
    {
        Vector3 position = new(1, 2, 3);
        ScenePickHit hit = ScenePickHit.ObjectHit(ObjectId, position, 4.5f);

        Assert.True(hit.HasHit);
        Assert.Equal(ScenePickElementKind.Object, hit.Kind);
        Assert.Equal(ObjectId, hit.ObjectId);
        Assert.Equal(position, hit.Position);
        Assert.Equal(4.5f, hit.Distance);
    }

    [Fact]
    public void VertexHit_SetsVertexHandle()
    {
        VertexHandle vertex = new(1, 2);
        ScenePickHit hit = ScenePickHit.VertexHit(ObjectId, vertex, Vector3.Zero, 1.0f);

        Assert.Equal(ScenePickElementKind.Vertex, hit.Kind);
        Assert.Equal(vertex, hit.Vertex);
        Assert.True(hit.Element.TryGetVertex(out VertexHandle actual));
        Assert.Equal(vertex, actual);
        Assert.False(hit.Element.TryGetEdge(out _));
    }

    [Fact]
    public void EdgeHit_SetsEdgeHandle()
    {
        HalfEdgeHandle edge = new(3, 4);
        ScenePickHit hit = ScenePickHit.EdgeHit(ObjectId, edge, Vector3.Zero, 1.0f);

        Assert.Equal(ScenePickElementKind.Edge, hit.Kind);
        Assert.Equal(edge, hit.Edge);
    }

    [Fact]
    public void FaceHit_SetsFaceHandle()
    {
        FaceHandle face = new(5, 6);
        ScenePickHit hit = ScenePickHit.FaceHit(ObjectId, face, Vector3.Zero, 1.0f);

        Assert.Equal(ScenePickElementKind.Face, hit.Kind);
        Assert.Equal(face, hit.Face);
    }
}

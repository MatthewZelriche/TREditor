using Godot;

namespace TREditor2026.Tests;

public class SelectionTargetTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void ForObject_SetsKindAndObjectId()
    {
        SelectionTarget target = SelectionTarget.ForObject(ObjectId);

        Assert.Equal(ScenePickElementKind.Object, target.Kind);
        Assert.Equal(ObjectId, target.ObjectId);
    }

    [Fact]
    public void ForVertex_SetsKindAndHandle()
    {
        VertexHandle vertex = new(2, 5);
        SelectionTarget target = SelectionTarget.ForVertex(ObjectId, vertex);

        Assert.Equal(ScenePickElementKind.Vertex, target.Kind);
        Assert.Equal(vertex, target.Vertex);
    }

    [Fact]
    public void ForEdge_SetsKindAndHandle()
    {
        HalfEdgeHandle edge = new(3, 1);
        SelectionTarget target = SelectionTarget.ForEdge(ObjectId, edge);

        Assert.Equal(ScenePickElementKind.Edge, target.Kind);
        Assert.Equal(edge, target.Edge);
    }

    [Fact]
    public void ForFace_SetsKindAndHandle()
    {
        FaceHandle face = new(4, 0);
        SelectionTarget target = SelectionTarget.ForFace(ObjectId, face);

        Assert.Equal(ScenePickElementKind.Face, target.Kind);
        Assert.Equal(face, target.Face);
    }

    [Fact]
    public void TryFromHit_NoHit_ReturnsFalse()
    {
        bool success = SelectionTarget.TryFromHit(ScenePickHit.None, out SelectionTarget target);

        Assert.False(success);
        Assert.Equal(default, target);
    }

    [Fact]
    public void TryFromHit_NullMesh_ReturnsFalse()
    {
        ScenePickHit hit = new(
            ScenePickElementKind.Object,
            null!,
            default,
            default,
            default,
            Vector3.Zero,
            1.0f
        );

        bool success = SelectionTarget.TryFromHit(hit, out SelectionTarget target);

        Assert.False(success);
        Assert.Equal(default, target);
    }

    [Theory]
    [InlineData(ScenePickElementKind.Object)]
    [InlineData(ScenePickElementKind.Vertex)]
    [InlineData(ScenePickElementKind.Edge)]
    [InlineData(ScenePickElementKind.Face)]
    public void ToString_FormatsKnownKinds(ScenePickElementKind kind)
    {
        SelectionTarget target = kind switch
        {
            ScenePickElementKind.Object => SelectionTarget.ForObject(ObjectId),
            ScenePickElementKind.Vertex => SelectionTarget.ForVertex(
                ObjectId,
                new VertexHandle(1, 0)
            ),
            ScenePickElementKind.Edge => SelectionTarget.ForEdge(
                ObjectId,
                new HalfEdgeHandle(2, 0)
            ),
            ScenePickElementKind.Face => SelectionTarget.ForFace(ObjectId, new FaceHandle(3, 0)),
            _ => throw new InvalidOperationException(),
        };

        string text = target.ToString();

        Assert.Contains(ObjectId.ToString(), text);
        Assert.Contains(kind.ToString(), text, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_UnsupportedKind_Throws()
    {
        SelectionTarget target = default;

        Assert.Throws<InvalidOperationException>(() => target.ToString());
    }
}

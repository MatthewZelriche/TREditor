namespace TREditor2026.Tests;

public sealed class BevelVertexCommandTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void CanCreate_RequiresOnlyVertices()
    {
        SelectionSnapshot vertices = SelectionSnapshot.From(
            [
                SelectionTarget.ForVertex(ObjectId, new VertexHandle(1, 0)),
                SelectionTarget.ForVertex(ObjectId, new VertexHandle(2, 0)),
            ]
        );
        SelectionSnapshot mixed = vertices.Add(
            SelectionTarget.ForEdge(ObjectId, new HalfEdgeHandle(1, 0))
        );

        Assert.True(BevelVertexCommand.CanCreate(vertices));
        Assert.False(BevelVertexCommand.CanCreate(mixed));
        Assert.False(BevelVertexCommand.CanCreate(SelectionSnapshot.Empty));
    }
}

namespace TREditor2026.Tests;

public sealed class BevelEdgeCommandTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void CanCreate_RequiresOnlyEdges()
    {
        SelectionSnapshot edges = SelectionSnapshot.From(
            [
                SelectionTarget.ForEdge(ObjectId, new HalfEdgeHandle(1, 0)),
                SelectionTarget.ForEdge(ObjectId, new HalfEdgeHandle(2, 0)),
            ]
        );
        SelectionSnapshot mixed = edges.Add(
            SelectionTarget.ForFace(ObjectId, new FaceHandle(1, 0))
        );

        Assert.True(BevelEdgeCommand.CanCreate(edges));
        Assert.False(BevelEdgeCommand.CanCreate(mixed));
        Assert.False(BevelEdgeCommand.CanCreate(SelectionSnapshot.Empty));
    }
}

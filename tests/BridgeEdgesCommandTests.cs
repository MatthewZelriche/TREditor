namespace TREditor2026.Tests;

public sealed class BridgeEdgesCommandTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void CanCreate_RequiresExactlyTwoEdgesOnOneObject()
    {
        SelectionSnapshot edges = SelectionSnapshot.From(
            [
                SelectionTarget.ForEdge(ObjectId, new HalfEdgeHandle(1, 0)),
                SelectionTarget.ForEdge(ObjectId, new HalfEdgeHandle(2, 0)),
            ]
        );

        Assert.True(BridgeEdgesCommand.CanCreate(edges));
        Assert.False(
            BridgeEdgesCommand.CanCreate(
                edges.Add(SelectionTarget.ForEdge(ObjectId, new HalfEdgeHandle(3, 0)))
            )
        );
        Assert.False(
            BridgeEdgesCommand.CanCreate(
                SelectionSnapshot.From(
                    [
                        edges.Targets[0],
                        SelectionTarget.ForEdge(
                            new EditorObjectId(Guid.NewGuid()),
                            new HalfEdgeHandle(2, 0)
                        ),
                    ]
                )
            )
        );
    }
}

namespace TREditor2026.Tests;

public sealed class FillHoleCommandTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void CanCreate_RequiresExactlyOneSelectedEdge()
    {
        SelectionTarget edge = SelectionTarget.ForEdge(ObjectId, new HalfEdgeHandle(1, 0));

        Assert.True(FillHoleCommand.CanCreate(SelectionSnapshot.From([edge])));
        Assert.False(FillHoleCommand.CanCreate(SelectionSnapshot.Empty));
        Assert.False(
            FillHoleCommand.CanCreate(
                SelectionSnapshot.From(
                    [edge, SelectionTarget.ForEdge(ObjectId, new HalfEdgeHandle(2, 0))]
                )
            )
        );
    }
}

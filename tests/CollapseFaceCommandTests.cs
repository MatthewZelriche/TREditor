namespace TREditor2026.Tests;

public sealed class CollapseFaceCommandTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void CanCreate_RequiresExactlyOneSelectedFace()
    {
        SelectionTarget face = SelectionTarget.ForFace(ObjectId, new FaceHandle(1, 0));

        Assert.True(CollapseFaceCommand.CanCreate(SelectionSnapshot.From([face])));
        Assert.False(CollapseFaceCommand.CanCreate(SelectionSnapshot.Empty));
        Assert.False(
            CollapseFaceCommand.CanCreate(
                SelectionSnapshot.From(
                    [face, SelectionTarget.ForFace(ObjectId, new FaceHandle(2, 0))]
                )
            )
        );
        Assert.False(
            CollapseFaceCommand.CanCreate(
                SelectionSnapshot.From(
                    [SelectionTarget.ForEdge(ObjectId, new HalfEdgeHandle(1, 0))]
                )
            )
        );
    }
}

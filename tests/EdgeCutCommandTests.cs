namespace TREditor2026.Tests;

public sealed class EdgeCutCommandTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void CanCreate_RequiresExactlyOneSelectedFace()
    {
        SelectionSnapshot face = SelectionSnapshot.From(
            [SelectionTarget.ForFace(ObjectId, new FaceHandle(1, 0))]
        );
        SelectionSnapshot edge = SelectionSnapshot.From(
            [SelectionTarget.ForEdge(ObjectId, new HalfEdgeHandle(1, 0))]
        );

        Assert.True(EdgeCutCommand.CanCreate(face));
        Assert.False(EdgeCutCommand.CanCreate(edge));
        Assert.False(EdgeCutCommand.CanCreate(SelectionSnapshot.Empty));
    }
}

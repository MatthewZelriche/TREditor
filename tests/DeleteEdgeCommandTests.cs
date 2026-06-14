namespace TREditor2026.Tests;

public sealed class DeleteEdgeCommandTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void GetSelectedEdges_ReturnsOnlyUniqueEdgeSelections()
    {
        HalfEdgeHandle edge = new(1, 0);
        SelectionSnapshot selection = SelectionSnapshot.From(
            [
                SelectionTarget.ForEdge(ObjectId, edge),
                SelectionTarget.ForEdge(ObjectId, edge),
                SelectionTarget.ForFace(ObjectId, new FaceHandle(2, 0)),
            ]
        );

        SelectionTarget selectedEdge = Assert.Single(DeleteEdgeCommand.GetSelectedEdges(selection));

        Assert.Equal(ScenePickElementKind.Edge, selectedEdge.Kind);
        Assert.Equal(edge, selectedEdge.Edge);
    }

    [Fact]
    public void CreateIfAny_SelectionWithoutEdgesReturnsNull()
    {
        SelectionSnapshot selection = SelectionSnapshot.From(
            [SelectionTarget.ForFace(ObjectId, new FaceHandle(1, 0))]
        );

        Assert.Null(DeleteEdgeCommand.CreateIfAny(selection));
    }
}

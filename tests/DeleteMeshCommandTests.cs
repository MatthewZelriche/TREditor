namespace TREditor2026.Tests;

public class DeleteMeshCommandTests
{
    private static readonly EditorObjectId FirstId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );
    private static readonly EditorObjectId SecondId = new(
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
    );

    [Fact]
    public void GetSelectedObjectIds_ReturnsOnlyUniqueObjectSelections()
    {
        SelectionSnapshot selection = SelectionSnapshot.From(
            [
                SelectionTarget.ForObject(FirstId),
                SelectionTarget.ForObject(FirstId),
                SelectionTarget.ForObject(SecondId),
                SelectionTarget.ForVertex(SecondId, new VertexHandle(1, 0)),
            ]
        );

        EditorObjectId[] objectIds = DeleteMeshCommand.GetSelectedObjectIds(selection).ToArray();

        Assert.Equal(new[] { FirstId, SecondId }, objectIds);
    }

    [Fact]
    public void GetSelectedObjectIds_ComponentOnlySelection_ReturnsEmpty()
    {
        SelectionSnapshot selection = SelectionSnapshot.From(
            [SelectionTarget.ForVertex(FirstId, new VertexHandle(1, 0))]
        );

        Assert.Empty(DeleteMeshCommand.GetSelectedObjectIds(selection));
    }
}

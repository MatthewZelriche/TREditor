namespace TREditor2026.Tests;

public sealed class CollapseVerticesCommandTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void CanCreate_RequiresAtLeastTwoVerticesOnOneObject()
    {
        SelectionSnapshot vertices = SelectionSnapshot.From(
            [
                SelectionTarget.ForVertex(ObjectId, new VertexHandle(1, 0)),
                SelectionTarget.ForVertex(ObjectId, new VertexHandle(2, 0)),
            ]
        );

        Assert.True(CollapseVerticesCommand.CanCreate(vertices));
        Assert.False(
            CollapseVerticesCommand.CanCreate(
                vertices.Add(SelectionTarget.ForEdge(ObjectId, new HalfEdgeHandle(1, 0)))
            )
        );
        Assert.False(
            CollapseVerticesCommand.CanCreate(
                SelectionSnapshot.From(
                    [
                        vertices.Targets[0],
                        SelectionTarget.ForVertex(
                            new EditorObjectId(Guid.NewGuid()),
                            new VertexHandle(2, 0)
                        ),
                    ]
                )
            )
        );
        Assert.False(
            CollapseVerticesCommand.CanCreate(SelectionSnapshot.From([vertices.Targets[0]]))
        );
    }
}

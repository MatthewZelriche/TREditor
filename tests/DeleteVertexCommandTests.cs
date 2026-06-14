namespace TREditor2026.Tests;

public sealed class DeleteVertexCommandTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void GetSelectedVertices_ReturnsOnlyUniqueVertexSelections()
    {
        VertexHandle vertex = new(1, 0);
        SelectionSnapshot selection = SelectionSnapshot.From(
            [
                SelectionTarget.ForVertex(ObjectId, vertex),
                SelectionTarget.ForVertex(ObjectId, vertex),
                SelectionTarget.ForEdge(ObjectId, new HalfEdgeHandle(2, 0)),
            ]
        );

        SelectionTarget selectedVertex = Assert.Single(
            DeleteVertexCommand.GetSelectedVertices(selection)
        );

        Assert.Equal(ScenePickElementKind.Vertex, selectedVertex.Kind);
        Assert.Equal(vertex, selectedVertex.Vertex);
    }

    [Fact]
    public void CreateIfAny_SelectionWithoutVerticesReturnsNull()
    {
        SelectionSnapshot selection = SelectionSnapshot.From(
            [SelectionTarget.ForEdge(ObjectId, new HalfEdgeHandle(1, 0))]
        );

        Assert.Null(DeleteVertexCommand.CreateIfAny(selection));
    }
}

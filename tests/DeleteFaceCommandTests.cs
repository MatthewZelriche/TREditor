namespace TREditor2026.Tests;

public sealed class DeleteFaceCommandTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void GetSelectedFaces_ReturnsOnlyUniqueFaceSelections()
    {
        FaceHandle face = new(1, 0);
        SelectionSnapshot selection = SelectionSnapshot.From(
            [
                SelectionTarget.ForFace(ObjectId, face),
                SelectionTarget.ForFace(ObjectId, face),
                SelectionTarget.ForVertex(ObjectId, new VertexHandle(2, 0)),
            ]
        );

        SelectionTarget selectedFace = Assert.Single(DeleteFaceCommand.GetSelectedFaces(selection));

        Assert.Equal(ScenePickElementKind.Face, selectedFace.Kind);
        Assert.Equal(face, selectedFace.Face);
    }

    [Fact]
    public void CreateIfAny_SelectionWithoutFacesReturnsNull()
    {
        SelectionSnapshot selection = SelectionSnapshot.From(
            [SelectionTarget.ForVertex(ObjectId, new VertexHandle(2, 0))]
        );

        Assert.Null(DeleteFaceCommand.CreateIfAny(selection));
    }
}

namespace TREditor2026.Tests;

public sealed class DetachFaceCommandTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void CanCreate_RequiresOnlyFaces()
    {
        SelectionSnapshot faces = SelectionSnapshot.From(
            [
                SelectionTarget.ForFace(ObjectId, new FaceHandle(1, 0)),
                SelectionTarget.ForFace(ObjectId, new FaceHandle(2, 0)),
            ]
        );

        Assert.True(DetachFaceCommand.CanCreate(faces));
        Assert.False(
            DetachFaceCommand.CanCreate(
                faces.Add(SelectionTarget.ForEdge(ObjectId, new HalfEdgeHandle(1, 0)))
            )
        );
        Assert.False(DetachFaceCommand.CanCreate(SelectionSnapshot.Empty));
    }
}

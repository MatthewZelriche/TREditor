namespace TREditor2026.Tests;

public sealed class InsetFaceCommandTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );
    private static readonly SelectionTarget Face = SelectionTarget.ForFace(
        ObjectId,
        new FaceHandle(1, 0)
    );

    [Fact]
    public void CanCreate_RequiresExactlyOneFace()
    {
        SelectionSnapshot faceSelection = SelectionSnapshot.From([Face]);

        Assert.True(InsetFaceCommand.CanCreate(faceSelection));
        Assert.Null(InsetFaceCommand.Create(faceSelection, 0f));
        Assert.False(InsetFaceCommand.CanCreate(SelectionSnapshot.Empty));
    }
}

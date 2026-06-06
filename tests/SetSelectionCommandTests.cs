namespace TREditor2026.Tests;

public class SetSelectionCommandTests
{
    private static readonly SelectionTarget Target = SelectionTarget.ForObject(
        new EditorObjectId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))
    );

    [Fact]
    public void CreateIfChanged_EqualSnapshots_ReturnsNull()
    {
        SelectionSnapshot snapshot = SelectionSnapshot.From([Target]);

        Assert.Null(SetSelectionCommand.CreateIfChanged(snapshot, snapshot));
    }

    [Fact]
    public void CreateIfChanged_EmptyToEmpty_ReturnsNull()
    {
        Assert.Null(
            SetSelectionCommand.CreateIfChanged(SelectionSnapshot.Empty, SelectionSnapshot.Empty)
        );
    }

    // Non-null paths construct EditorCommand (GodotObject) and require the Godot runtime.
    // Command creation with a changed snapshot is covered once IEditorCommandContext lands.
}

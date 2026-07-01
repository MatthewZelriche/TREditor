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

    [Fact]
    public void CreateIfChanged_ChangedSnapshotCreatesNonDocumentCommand()
    {
        SelectionSnapshot snapshot = SelectionSnapshot.From([Target]);

        SetSelectionCommand command = SetSelectionCommand.CreateIfChanged(
            SelectionSnapshot.Empty,
            snapshot
        );

        Assert.NotNull(command);
        Assert.False(command.AffectsDocument);
        Assert.Equal(EditorCommandState.New, command.State);
    }
}

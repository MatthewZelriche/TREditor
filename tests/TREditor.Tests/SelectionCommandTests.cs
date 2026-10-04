using System;

namespace TREditor.Tests;

public sealed class SelectionCommandTests
{
    private static readonly MeshObjectId First = new(1);
    private static readonly MeshObjectId Second = new(2);

    [Fact]
    public void SubmittingASelectionAppliesItAndRecordsHistory()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        var selection = new SelectionState();
        IEditorCommandSink sink = history;

        sink.Submit(new SelectionCommand(selection, null, First));

        Assert.Equal([First], selection.Current.ObjectIds);
        Assert.True(history.CanUndo);
        Assert.Equal("Select Object", history.UndoDisplayName);
    }

    [Fact]
    public void ToolSelectionCapabilityCreatesACommandWithoutApplyingIt()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        var selection = new SelectionState();
        IEditorSelection toolSelection = selection;

        IEditorCommand command = toolSelection.CreateCommand(SelectionValue.ForObject(First));

        Assert.True(selection.Current.IsEmpty);

        history.Submit(command);

        Assert.Equal([First], selection.Current.ObjectIds);
        Assert.True(history.CanUndo);
    }

    [Fact]
    public void ReplacingASelectionRestoresThePreviousObjectOnUndo()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        var selection = new SelectionState();
        history.Submit(new SelectionCommand(selection, null, First));

        history.Submit(new SelectionCommand(selection, First, Second));
        Assert.Equal([Second], selection.Current.ObjectIds);

        Assert.True(history.Undo());
        Assert.Equal([First], selection.Current.ObjectIds);

        Assert.True(history.Redo());
        Assert.Equal([Second], selection.Current.ObjectIds);
    }

    [Fact]
    public void ClearingASelectionUndoesBackToTheExactPreviousObject()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        var selection = new SelectionState();
        selection.Select(Second);

        history.Submit(new SelectionCommand(selection, Second, null));

        Assert.True(selection.Current.IsEmpty);
        Assert.Equal("Clear Selection", history.UndoDisplayName);

        Assert.True(history.Undo());
        Assert.Equal([Second], selection.Current.ObjectIds);
    }

    [Fact]
    public void ANoOpSelectionChangeCannotBecomeACommand()
    {
        var selection = new SelectionState();

        Assert.Throws<ArgumentException>(() => new SelectionCommand(selection, First, First));
        Assert.Throws<ArgumentException>(() => new SelectionCommand(selection, null, null));
    }

    [Fact]
    public void UndoAndRedoRaiseSelectionChangedOnce()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        var selection = new SelectionState();
        int changedCount = 0;
        selection.Changed += () => changedCount++;

        history.Submit(new SelectionCommand(selection, null, First));
        history.Undo();
        history.Redo();

        Assert.Equal(3, changedCount);
    }

    [Fact]
    public void HistoryChangeNotificationsAccompanySelectionCommands()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        var selection = new SelectionState();
        int historyChangedCount = 0;
        history.Changed += () => historyChangedCount++;

        history.Submit(new SelectionCommand(selection, null, First));
        history.Undo();
        history.Redo();

        Assert.Equal(3, historyChangedCount);
    }

    [Fact]
    public void SelectionCommandsIgnoreTheDocumentTheyAreGiven()
    {
        using var first = new Document();
        using var second = new Document();
        using var history = new CommandHistory();
        history.SetDocument(first);
        var selection = new SelectionState();

        history.Submit(new SelectionCommand(selection, null, First));
        history.Clear();
        history.SetDocument(second);
        history.Submit(new SelectionCommand(selection, First, Second));

        Assert.Equal([Second], selection.Current.ObjectIds);
        Assert.Equal(0, first.ObjectCount);
        Assert.Equal(0, second.ObjectCount);
    }

    [Fact]
    public void ANullSelectionStateIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new SelectionCommand(null!, null, First));
    }

    [Fact]
    public void ComponentSelectionParticipatesInUndoAndRedo()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        var selection = new SelectionState();
        var component = new MeshComponentId(First, MeshComponentType.Vertex, 3);

        history.Submit(
            new SelectionCommand(
                selection,
                SelectionValue.Empty,
                SelectionValue.ForComponent(component)
            )
        );

        Assert.Equal([component], selection.Current.Components);
        Assert.Equal("Select Component", history.UndoDisplayName);
        Assert.True(history.Undo());
        Assert.True(selection.Current.IsEmpty);
        Assert.True(history.Redo());
        Assert.Equal([component], selection.Current.Components);
    }

    [Fact]
    public void UndoAndRedoRestoreCompleteMixedSelections()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        var selection = new SelectionState();
        var component = new MeshComponentId(First, MeshComponentType.Edge, 3);
        SelectionValue before = SelectionValue.ForSelections([First], [component]);
        SelectionValue after = before.Toggle(Second);
        selection.Apply(before);

        history.Submit(new SelectionCommand(selection, before, after));

        Assert.Equal(2, selection.Current.ObjectIds.Count);
        Assert.Contains(component, selection.Current.Components);
        Assert.Equal("Select Object", history.UndoDisplayName);
        Assert.True(history.Undo());
        Assert.Equal(before, selection.Current);
        Assert.True(history.Redo());
        Assert.Equal(after, selection.Current);
    }
}

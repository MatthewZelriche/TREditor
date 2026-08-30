using System;

namespace TREditor.Tests;

public sealed class SelectionCommandTests
{
    private static readonly MeshObjectId First = new(1);
    private static readonly MeshObjectId Second = new(2);

    [Fact]
    public void SubmittingASelectionAppliesItAndRecordsHistory()
    {
        using var scene = new Scene();
        using var history = new CommandHistory();
        history.SetScene(scene);
        var selection = new SelectionState();
        IEditorCommandSink sink = history;

        sink.Submit(new SelectionCommand(selection, null, First));

        Assert.Equal(First, selection.Selected);
        Assert.True(history.CanUndo);
        Assert.Equal("Select Object", history.UndoDisplayName);
    }

    [Fact]
    public void ReplacingASelectionRestoresThePreviousObjectOnUndo()
    {
        using var scene = new Scene();
        using var history = new CommandHistory();
        history.SetScene(scene);
        var selection = new SelectionState();
        history.Submit(new SelectionCommand(selection, null, First));

        history.Submit(new SelectionCommand(selection, First, Second));
        Assert.Equal(Second, selection.Selected);

        Assert.True(history.Undo());
        Assert.Equal(First, selection.Selected);

        Assert.True(history.Redo());
        Assert.Equal(Second, selection.Selected);
    }

    [Fact]
    public void ClearingASelectionUndoesBackToTheExactPreviousObject()
    {
        using var scene = new Scene();
        using var history = new CommandHistory();
        history.SetScene(scene);
        var selection = new SelectionState();
        selection.Select(Second);

        history.Submit(new SelectionCommand(selection, Second, null));

        Assert.Null(selection.Selected);
        Assert.Equal("Clear Selection", history.UndoDisplayName);

        Assert.True(history.Undo());
        Assert.Equal(Second, selection.Selected);
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
        using var scene = new Scene();
        using var history = new CommandHistory();
        history.SetScene(scene);
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
        using var scene = new Scene();
        using var history = new CommandHistory();
        history.SetScene(scene);
        var selection = new SelectionState();
        int historyChangedCount = 0;
        history.Changed += () => historyChangedCount++;

        history.Submit(new SelectionCommand(selection, null, First));
        history.Undo();
        history.Redo();

        Assert.Equal(3, historyChangedCount);
    }

    [Fact]
    public void SelectionCommandsIgnoreTheSceneTheyAreGiven()
    {
        using var first = new Scene();
        using var second = new Scene();
        using var history = new CommandHistory();
        history.SetScene(first);
        var selection = new SelectionState();

        history.Submit(new SelectionCommand(selection, null, First));
        history.Clear();
        history.SetScene(second);
        history.Submit(new SelectionCommand(selection, First, Second));

        Assert.Equal(Second, selection.Selected);
        Assert.Equal(0, first.ObjectCount);
        Assert.Equal(0, second.ObjectCount);
    }

    [Fact]
    public void ANullSelectionStateIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new SelectionCommand(null!, null, First));
    }
}

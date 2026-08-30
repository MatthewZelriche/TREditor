namespace TREditor.Tests;

public sealed class SelectionStateTests
{
    [Fact]
    public void StartsWithNothingSelected()
    {
        var selection = new SelectionState();

        Assert.Null(selection.Selected);
    }

    [Fact]
    public void SelectStoresTheObjectAndRaisesChanged()
    {
        var selection = new SelectionState();
        var id = new MeshObjectId(5);
        int changedCount = 0;
        selection.Changed += () => changedCount++;

        selection.Select(id);

        Assert.Equal(id, selection.Selected);
        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void SelectingTheSameObjectAgainRaisesNothing()
    {
        var selection = new SelectionState();
        var id = new MeshObjectId(5);
        selection.Select(id);
        int changedCount = 0;
        selection.Changed += () => changedCount++;

        selection.Select(id);

        Assert.Equal(id, selection.Selected);
        Assert.Equal(0, changedCount);
    }

    [Fact]
    public void SelectingADifferentObjectReplacesTheSelection()
    {
        var selection = new SelectionState();
        var replacement = new MeshObjectId(6);
        selection.Select(new MeshObjectId(5));
        int changedCount = 0;
        selection.Changed += () => changedCount++;

        selection.Select(replacement);

        Assert.Equal(replacement, selection.Selected);
        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void ClearEmptiesTheSelectionOnlyWhenSomethingWasSelected()
    {
        var selection = new SelectionState();
        selection.Select(new MeshObjectId(5));
        int changedCount = 0;
        selection.Changed += () => changedCount++;

        selection.Clear();
        selection.Clear();

        Assert.Null(selection.Selected);
        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void ClearChangedListenersStopsFurtherNotifications()
    {
        var selection = new SelectionState();
        int changedCount = 0;
        selection.Changed += () => changedCount++;

        selection.ClearChangedListeners();
        selection.Select(new MeshObjectId(5));

        Assert.Equal(0, changedCount);
    }
}

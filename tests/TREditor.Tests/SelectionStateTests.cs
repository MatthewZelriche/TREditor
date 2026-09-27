namespace TREditor.Tests;

public sealed class SelectionStateTests
{
    [Fact]
    public void StartsWithNothingSelected()
    {
        var selection = new SelectionState();

        Assert.True(selection.Current.IsEmpty);
    }

    [Fact]
    public void SelectStoresTheObjectAndRaisesChanged()
    {
        var selection = new SelectionState();
        var id = new MeshObjectId(5);
        int changedCount = 0;
        selection.Changed += () => changedCount++;

        selection.Select(id);

        Assert.Equal(id, selection.Current.ObjectId);
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

        Assert.Equal(id, selection.Current.ObjectId);
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

        Assert.Equal(replacement, selection.Current.ObjectId);
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

        Assert.True(selection.Current.IsEmpty);
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

    [Fact]
    public void ComponentSelectionIsDistinctFromObjectSelection()
    {
        var selection = new SelectionState();
        var component = new MeshComponentId(
            new MeshObjectId(5),
            MeshComponentType.Edge,
            12
        );

        selection.Select(component);

        Assert.Equal(component, selection.Current.Component);
        Assert.Null(selection.Current.ObjectId);

        selection.Select(component.ObjectId);

        Assert.Equal(component.ObjectId, selection.Current.ObjectId);
        Assert.Null(selection.Current.Component);
    }
}

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

        Assert.Equal([id], selection.Current.ObjectIds);
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

        Assert.Equal([id], selection.Current.ObjectIds);
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

        Assert.Equal([replacement], selection.Current.ObjectIds);
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

        Assert.Equal([component], selection.Current.Components);
        Assert.Empty(selection.Current.ObjectIds);

        selection.Select(component.ObjectId);

        Assert.Equal([component.ObjectId], selection.Current.ObjectIds);
        Assert.Empty(selection.Current.Components);
    }

    [Fact]
    public void ToggleBuildsMixedObjectAndComponentSelections()
    {
        var selection = new SelectionState();
        var firstObject = new MeshObjectId(5);
        var secondObject = new MeshObjectId(6);
        var component = new MeshComponentId(firstObject, MeshComponentType.Edge, 12);
        int changedCount = 0;
        selection.Changed += () => changedCount++;

        selection.Toggle(firstObject);
        selection.Toggle(secondObject);
        selection.Toggle(component);

        Assert.Equal(2, selection.Current.ObjectIds.Count);
        Assert.Contains(firstObject, selection.Current.ObjectIds);
        Assert.Contains(secondObject, selection.Current.ObjectIds);
        Assert.Contains(component, selection.Current.Components);
        Assert.Equal(3, changedCount);

        selection.Toggle(firstObject);
        selection.Toggle(component);

        Assert.Equal([secondObject], selection.Current.ObjectIds);
        Assert.Empty(selection.Current.Components);
        Assert.Equal(5, changedCount);
    }

    [Fact]
    public void SelectionValuesUseSetEqualityRegardlessOfInsertionOrder()
    {
        var firstObject = new MeshObjectId(5);
        var secondObject = new MeshObjectId(6);
        var firstComponent = new MeshComponentId(firstObject, MeshComponentType.Vertex, 1);
        var secondComponent = new MeshComponentId(secondObject, MeshComponentType.Face, 2);

        SelectionValue first = SelectionValue.ForSelections(
            [firstObject, secondObject],
            [firstComponent, secondComponent]
        );
        SelectionValue second = SelectionValue.ForSelections(
            [secondObject, firstObject],
            [secondComponent, firstComponent]
        );

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void ComponentSelectionAlsoRetainsInsertionOrder()
    {
        var objectId = new MeshObjectId(5);
        var first = new MeshComponentId(objectId, MeshComponentType.Vertex, 1);
        var second = new MeshComponentId(objectId, MeshComponentType.Vertex, 2);

        SelectionValue selection = SelectionValue.ForSelections([], [second, first, second]);

        Assert.Equal([second, first], selection.OrderedComponents);
        selection = selection.Toggle(first);
        Assert.Equal([second], selection.OrderedComponents);
        Assert.Equal([second, first], selection.Toggle(first).OrderedComponents);
    }

    [Fact]
    public void HoverOnlyNotifiesWhenTheComponentChanges()
    {
        var selection = new SelectionState();
        var component = new MeshComponentId(
            new MeshObjectId(2),
            MeshComponentType.Vertex,
            4
        );
        int selectionChangedCount = 0;
        int hoverChangedCount = 0;
        selection.Changed += () => selectionChangedCount++;
        selection.HoverChanged += () => hoverChangedCount++;

        selection.SetHoveredComponent(component);
        selection.SetHoveredComponent(component);
        selection.ClearHover();
        selection.ClearHover();

        Assert.Null(selection.HoveredComponent);
        Assert.Equal(0, selectionChangedCount);
        Assert.Equal(2, hoverChangedCount);
    }
}

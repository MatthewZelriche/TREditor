namespace TREditor.Tests;

public sealed class SelectionModeStateTests
{
    [Fact]
    public void DefaultsToObjectModeWithNoComponents()
    {
        var state = new SelectionModeState();

        Assert.Equal(EditorMode.Object, state.Mode);
        Assert.Equal(ComponentMask.None, state.Components);
        Assert.False(state.IsAllComponents);
    }

    [Fact]
    public void SelectAllComponentsEntersComponentModeWithAllMask()
    {
        var state = new SelectionModeState();
        int changedCount = 0;
        state.Changed += () => changedCount++;

        state.SelectAllComponents();

        Assert.Equal(EditorMode.Component, state.Mode);
        Assert.Equal(ComponentMask.All, state.Components);
        Assert.True(state.IsAllComponents);
        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void EnterObjectModeClearsComponentSelection()
    {
        var state = new SelectionModeState();
        state.SelectAllComponents();
        int changedCount = 0;
        state.Changed += () => changedCount++;

        state.EnterObjectMode();

        Assert.Equal(EditorMode.Object, state.Mode);
        Assert.Equal(ComponentMask.None, state.Components);
        Assert.False(state.IsAllComponents);
        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void ToggleComponentFromObjectSelectsThatComponent()
    {
        var state = new SelectionModeState();

        state.ToggleComponent(ComponentMask.Vertex);

        Assert.Equal(EditorMode.Component, state.Mode);
        Assert.Equal(ComponentMask.Vertex, state.Components);
        Assert.False(state.IsAllComponents);
    }

    [Fact]
    public void ToggleComponentFromAllReplacesWithThatComponent()
    {
        var state = new SelectionModeState();
        state.SelectAllComponents();

        state.ToggleComponent(ComponentMask.Edge);

        Assert.Equal(EditorMode.Component, state.Mode);
        Assert.Equal(ComponentMask.Edge, state.Components);
        Assert.False(state.IsAllComponents);
    }

    [Fact]
    public void ToggleComponentAllowsNonEmptyCombinations()
    {
        var state = new SelectionModeState();

        state.ToggleComponent(ComponentMask.Vertex);
        state.ToggleComponent(ComponentMask.Face);

        Assert.Equal(ComponentMask.Vertex | ComponentMask.Face, state.Components);
        Assert.False(state.IsAllComponents);

        state.ToggleComponent(ComponentMask.Edge);

        Assert.Equal(ComponentMask.All, state.Components);
        Assert.False(state.IsAllComponents);
    }

    [Fact]
    public void ToggleComponentRefusesToClearTheLastBit()
    {
        var state = new SelectionModeState();
        state.ToggleComponent(ComponentMask.Vertex);
        int changedCount = 0;
        state.Changed += () => changedCount++;

        state.ToggleComponent(ComponentMask.Vertex);

        Assert.Equal(ComponentMask.Vertex, state.Components);
        Assert.Equal(EditorMode.Component, state.Mode);
        Assert.Equal(0, changedCount);
    }

    [Fact]
    public void SelectAllComponentsIsMutuallyExclusiveWithExplicitCombination()
    {
        var state = new SelectionModeState();
        state.ToggleComponent(ComponentMask.Vertex);
        state.ToggleComponent(ComponentMask.Edge);

        state.SelectAllComponents();

        Assert.True(state.IsAllComponents);
        Assert.Equal(ComponentMask.All, state.Components);

        state.ToggleComponent(ComponentMask.Face);

        Assert.False(state.IsAllComponents);
        Assert.Equal(ComponentMask.Face, state.Components);
    }

    [Fact]
    public void NoOpTransitionsDoNotNotify()
    {
        var state = new SelectionModeState();
        int changedCount = 0;
        state.Changed += () => changedCount++;

        state.EnterObjectMode();
        state.SelectAllComponents();
        state.SelectAllComponents();

        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void ToggleComponentRejectsNonSingleFlags()
    {
        var state = new SelectionModeState();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => state.ToggleComponent(ComponentMask.None)
        );
        Assert.Throws<ArgumentOutOfRangeException>(
            () => state.ToggleComponent(ComponentMask.All)
        );
        Assert.Throws<ArgumentOutOfRangeException>(
            () => state.ToggleComponent(ComponentMask.Vertex | ComponentMask.Edge)
        );
    }

    [Fact]
    public void XRayNotifiesOnlyWhenItsGlobalStateChanges()
    {
        var state = new SelectionModeState();
        int changedCount = 0;
        state.Changed += () => changedCount++;

        state.SetXRayEnabled(true);
        state.SetXRayEnabled(true);

        Assert.True(state.XRayEnabled);
        Assert.Equal(1, changedCount);

        state.SetXRayEnabled(false);

        Assert.False(state.XRayEnabled);
        Assert.Equal(2, changedCount);
    }
}

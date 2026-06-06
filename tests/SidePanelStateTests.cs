namespace TREditor2026.Tests;

public class SidePanelStateTests
{
    [Fact]
    public void Open_AddsAndActivatesTabAndExpandsPanel()
    {
        SidePanelState state = new();

        state.Open("Create");

        Assert.Equal(["Create"], state.OpenTabs);
        Assert.Equal("Create", state.ActiveTabId);
        Assert.False(state.IsCollapsed);
    }

    [Fact]
    public void Open_ExistingTabOnlyActivatesIt()
    {
        SidePanelState state = new();
        state.Open("Create");
        state.Open("Inspector");

        state.Open("Create");

        Assert.Equal(["Create", "Inspector"], state.OpenTabs);
        Assert.Equal("Create", state.ActiveTabId);
    }

    [Fact]
    public void Close_ActiveTabSelectsAdjacentTab()
    {
        SidePanelState state = new();
        state.Open("Create");
        state.Open("Inspector");

        state.Close("Inspector");

        Assert.Equal("Create", state.ActiveTabId);
        Assert.False(state.IsCollapsed);
    }

    [Fact]
    public void Close_LastTabCollapsesPanel()
    {
        SidePanelState state = new();
        state.Open("Create");

        state.Close("Create");

        Assert.Empty(state.OpenTabs);
        Assert.Null(state.ActiveTabId);
        Assert.True(state.IsCollapsed);
    }

    [Fact]
    public void CollapseAndExpand_PreserveOpenTabs()
    {
        SidePanelState state = new();
        state.Open("Create");

        state.Collapse();
        Assert.True(state.IsCollapsed);

        state.Expand();
        Assert.False(state.IsCollapsed);
        Assert.Equal(["Create"], state.OpenTabs);
    }
}

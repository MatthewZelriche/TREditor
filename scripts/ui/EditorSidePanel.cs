using Godot;

public partial class EditorSidePanel : PanelContainer
{
    private readonly SidePanelState _state = new();

    private TabContainer _tabs;
    private Button _collapseButton;
    private Button _closeTabButton;
    private Button _expandButton;
    private bool _syncingView;

    public override void _Ready()
    {
        _tabs = GetNode<TabContainer>("Column/Tabs");
        _collapseButton = GetNode<Button>("Column/Header/CollapseButton");
        _closeTabButton = GetNode<Button>("Column/Header/CloseTabButton");
        _expandButton = GetNode<Button>("../SidePanelExpandButton");

        _collapseButton.Pressed += Collapse;
        _closeTabButton.Pressed += CloseActiveTab;
        _expandButton.Pressed += Expand;
        _tabs.TabChanged += OnTabChanged;

        OpenInitialTab();
    }

    public void OpenTab(string tabId)
    {
        if (FindTab(tabId) < 0)
        {
            GD.PushWarning($"EditorSidePanel could not find tab '{tabId}'.");
            return;
        }

        _state.Open(tabId);
        SyncView();
    }

    public void CloseTab(string tabId)
    {
        _state.Close(tabId);
        SyncView();
    }

    public void Collapse()
    {
        _state.Collapse();
        SyncView();
    }

    public void Expand()
    {
        _state.Expand();
        SyncView();
    }

    private void CloseActiveTab()
    {
        if (_state.ActiveTabId != null)
        {
            CloseTab(_state.ActiveTabId);
        }
    }

    private void OnTabChanged(long tabIndex)
    {
        if (!_syncingView && tabIndex >= 0 && tabIndex < _tabs.GetTabCount())
        {
            _state.Open(_tabs.GetTabControl((int)tabIndex).Name);
            SyncView();
        }
    }

    private void SyncView()
    {
        _syncingView = true;

        try
        {
            for (int i = 0; i < _tabs.GetTabCount(); i++)
            {
                string tabId = _tabs.GetTabControl(i).Name;
                _tabs.SetTabHidden(i, !_state.IsOpen(tabId));
            }

            int activeTab = FindTab(_state.ActiveTabId);
            if (activeTab >= 0)
            {
                _tabs.CurrentTab = activeTab;
            }

            bool hasOpenTabs = _state.OpenTabs.Count > 0;
            Visible = hasOpenTabs && !_state.IsCollapsed;
            _expandButton.Visible = hasOpenTabs && _state.IsCollapsed;
        }
        finally
        {
            _syncingView = false;
        }
    }

    private void OpenInitialTab()
    {
        if (_tabs.GetTabCount() > 0)
        {
            OpenTab(_tabs.GetTabControl(0).Name);
            return;
        }

        SyncView();
    }

    private int FindTab(string tabId)
    {
        if (tabId == null)
        {
            return -1;
        }

        for (int i = 0; i < _tabs.GetTabCount(); i++)
        {
            if (_tabs.GetTabControl(i).Name == tabId)
            {
                return i;
            }
        }

        return -1;
    }
}

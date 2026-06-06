using System;
using System.Collections.Generic;

public sealed class SidePanelState
{
    private readonly List<string> _openTabs = new();

    public IReadOnlyList<string> OpenTabs => _openTabs;
    public string ActiveTabId { get; private set; }
    public bool IsCollapsed { get; private set; } = true;

    public bool IsOpen(string tabId) => _openTabs.Contains(tabId);

    public void Open(string tabId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabId);

        if (!IsOpen(tabId))
        {
            _openTabs.Add(tabId);
        }

        ActiveTabId = tabId;
        IsCollapsed = false;
    }

    public void Close(string tabId)
    {
        int closedIndex = _openTabs.IndexOf(tabId);
        if (closedIndex < 0)
        {
            return;
        }

        _openTabs.RemoveAt(closedIndex);

        if (_openTabs.Count == 0)
        {
            ActiveTabId = null;
            IsCollapsed = true;
            return;
        }

        if (ActiveTabId == tabId)
        {
            ActiveTabId = _openTabs[Math.Min(closedIndex, _openTabs.Count - 1)];
        }
    }

    public void Collapse()
    {
        if (_openTabs.Count > 0)
        {
            IsCollapsed = true;
        }
    }

    public void Expand()
    {
        if (_openTabs.Count > 0)
        {
            IsCollapsed = false;
        }
    }
}

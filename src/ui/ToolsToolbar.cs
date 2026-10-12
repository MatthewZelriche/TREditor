#nullable enable

using System;
using Godot;

public partial class ToolsToolbar : HBoxContainer
{
    private SelectionModeState? _selectionMode;
    private EditorToolRegistry? _tools;
    private (Button Button, string ToolId, Func<SelectionModeState, bool> Visible)[] _entries =
        null!;

    public override void _Ready()
    {
        _entries =
        [
            Entry("NewPrimitiveButton", PrimitiveTool.ToolId, mode => mode.Mode == EditorMode.Object),
            ComponentEntry("MergeVerticesButton", MergeVerticesTool.ToolId, ComponentMask.Vertex),
            ComponentEntry("CollapseEdgesButton", CollapseEdgesTool.ToolId, ComponentMask.Edge),
            ComponentEntry("DissolveEdgesButton", DissolveEdgesTool.ToolId, ComponentMask.Edge),
            ComponentEntry("ExtendEdgesButton", ExtendEdgesTool.ToolId, ComponentMask.Edge),
            ComponentEntry("CombineFacesButton", CombineFacesTool.ToolId, ComponentMask.Face),
            ComponentEntry("CollapseFacesButton", CollapseFacesTool.ToolId, ComponentMask.Face),
            ComponentEntry("ExtrudeFacesButton", ExtrudeFacesTool.ToolId, ComponentMask.Face),
        ];
    }

    public override void _ExitTree() => Unbind();

    public void Bind(SelectionModeState selectionMode, EditorToolRegistry tools)
    {
        ArgumentNullException.ThrowIfNull(selectionMode);
        ArgumentNullException.ThrowIfNull(tools);
        Unbind();
        _selectionMode = selectionMode;
        _tools = tools;
        _selectionMode.Changed += SyncVisibility;
        _tools.ActiveToolChanged += SyncActiveTool;
        SyncVisibility();
        SyncActiveTool();
    }

    private void Unbind()
    {
        if (_selectionMode is not null)
            _selectionMode.Changed -= SyncVisibility;
        if (_tools is not null)
            _tools.ActiveToolChanged -= SyncActiveTool;
        _selectionMode = null;
        _tools = null;
    }

    private void SyncVisibility()
    {
        if (_selectionMode is null)
            return;

        foreach (var (button, toolId, visible) in _entries)
        {
            button.Visible = visible(_selectionMode);
            if (!button.Visible && _tools?.ActiveTool?.Id == toolId)
                _tools.Deactivate();
        }
    }

    private void SyncActiveTool()
    {
        if (_tools is null)
            return;

        foreach (var (button, toolId, _) in _entries)
            button.ButtonPressed = _tools.ActiveTool?.Id == toolId;
    }

    private (Button Button, string ToolId, Func<SelectionModeState, bool> Visible) ComponentEntry(
        string path,
        string toolId,
        ComponentMask mask
    ) =>
        Entry(
            path,
            toolId,
            mode => mode.Mode == EditorMode.Component && mode.Components.HasFlag(mask)
        );

    private (Button Button, string ToolId, Func<SelectionModeState, bool> Visible) Entry(
        string path,
        string toolId,
        Func<SelectionModeState, bool> visible
    )
    {
        Button button = GetNode<Button>(path);
        button.ToggleMode = true;
        button.Pressed += () => ToggleTool(toolId);
        return (button, toolId, visible);
    }

    private void ToggleTool(string toolId)
    {
        if (_tools is null)
            return;

        if (_tools.ActiveTool?.Id == toolId)
            _tools.Deactivate();
        else
            _tools.Activate(toolId);

        SyncActiveTool();
    }
}

#nullable enable

using System;
using Godot;

public partial class ToolsToolbar : HBoxContainer
{
    private SelectionModeState? _selectionMode;
    private EditorToolRegistry? _tools;
    private Button _newPrimitiveButton = null!;
    private Button _mergeVerticesButton = null!;
    private Button _collapseEdgesButton = null!;

    public override void _Ready()
    {
        _newPrimitiveButton = BindToggleButton("NewPrimitiveButton", () => ToggleTool(PrimitiveTool.ToolId));
        _mergeVerticesButton = BindToggleButton(
            "MergeVerticesButton",
            () => ToggleTool(MergeVerticesTool.ToolId)
        );
        _collapseEdgesButton = BindToggleButton(
            "CollapseEdgesButton",
            () => ToggleTool(CollapseEdgesTool.ToolId)
        );
    }

    public override void _ExitTree()
    {
        Unbind();
    }

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

        _newPrimitiveButton.Visible = _selectionMode.Mode == EditorMode.Object;
        _mergeVerticesButton.Visible =
            _selectionMode.Mode == EditorMode.Component
            && _selectionMode.Components.HasFlag(ComponentMask.Vertex);
        _collapseEdgesButton.Visible =
            _selectionMode.Mode == EditorMode.Component
            && _selectionMode.Components.HasFlag(ComponentMask.Edge);
        DeactivateIfHidden(_newPrimitiveButton, PrimitiveTool.ToolId);
        DeactivateIfHidden(_mergeVerticesButton, MergeVerticesTool.ToolId);
        DeactivateIfHidden(_collapseEdgesButton, CollapseEdgesTool.ToolId);
    }

    private void SyncActiveTool()
    {
        if (_tools is null)
            return;

        _newPrimitiveButton.ButtonPressed = _tools.ActiveTool?.Id == PrimitiveTool.ToolId;
        _mergeVerticesButton.ButtonPressed = _tools.ActiveTool?.Id == MergeVerticesTool.ToolId;
        _collapseEdgesButton.ButtonPressed = _tools.ActiveTool?.Id == CollapseEdgesTool.ToolId;
    }

    private Button BindToggleButton(string path, Action pressed)
    {
        Button button = GetNode<Button>(path);
        button.ToggleMode = true;
        button.Pressed += pressed;
        return button;
    }

    private void DeactivateIfHidden(Button button, string toolId)
    {
        if (!button.Visible && _tools?.ActiveTool?.Id == toolId)
            _tools.Deactivate();
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

#nullable enable

using System;
using Godot;

public partial class ToolsToolbar : HBoxContainer
{
    private SelectionModeState? _selectionMode;
    private EditorToolRegistry? _tools;
    private Button _newPrimitiveButton = null!;

    public override void _Ready()
    {
        _newPrimitiveButton = GetNode<Button>("NewPrimitiveButton");
        _newPrimitiveButton.ToggleMode = true;
        _newPrimitiveButton.Pressed += OnNewPrimitivePressed;
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
        if (!_newPrimitiveButton.Visible && _tools?.ActiveTool?.Id == PrimitiveTool.ToolId)
            _tools.Deactivate();
    }

    private void SyncActiveTool()
    {
        if (_tools is not null)
            _newPrimitiveButton.ButtonPressed = _tools.ActiveTool?.Id == PrimitiveTool.ToolId;
    }

    private void OnNewPrimitivePressed()
    {
        if (_tools is null)
            return;

        if (_tools.ActiveTool?.Id == PrimitiveTool.ToolId)
            _tools.Deactivate();
        else
            _tools.Activate(PrimitiveTool.ToolId);

        SyncActiveTool();
    }
}

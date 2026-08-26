#nullable enable

using System;
using Godot;

public partial class ToolsToolbar : HBoxContainer
{
    private SelectionModeState? _selectionMode;
    private Button _newPrimitiveButton = null!;

    public override void _Ready()
    {
        _newPrimitiveButton = GetNode<Button>("NewPrimitiveButton");
    }

    public override void _ExitTree()
    {
        Unbind();
    }

    public void Bind(SelectionModeState selectionMode)
    {
        ArgumentNullException.ThrowIfNull(selectionMode);
        Unbind();
        _selectionMode = selectionMode;
        _selectionMode.Changed += SyncVisibility;
        SyncVisibility();
    }

    private void Unbind()
    {
        if (_selectionMode is null)
            return;

        _selectionMode.Changed -= SyncVisibility;
        _selectionMode = null;
    }

    private void SyncVisibility()
    {
        if (_selectionMode is null)
            return;

        _newPrimitiveButton.Visible = _selectionMode.Mode == EditorMode.Object;
    }
}

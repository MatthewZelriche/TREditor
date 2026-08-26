#nullable enable

using System;
using Godot;

public partial class SelectionModeToolbar : HBoxContainer
{
    private SelectionModeState? _selectionMode;
    private Button _objectButton = null!;
    private Button _allButton = null!;
    private Button _vertexButton = null!;
    private Button _edgeButton = null!;
    private Button _faceButton = null!;
    private bool _syncing;

    public override void _Ready()
    {
        _objectButton = GetNode<Button>("ObjectButton");
        _allButton = GetNode<Button>("AllButton");
        _vertexButton = GetNode<Button>("VertexButton");
        _edgeButton = GetNode<Button>("EdgeButton");
        _faceButton = GetNode<Button>("FaceButton");

        _objectButton.Pressed += OnObjectPressed;
        _allButton.Pressed += OnAllPressed;
        _vertexButton.Pressed += () => OnComponentPressed(ComponentMask.Vertex);
        _edgeButton.Pressed += () => OnComponentPressed(ComponentMask.Edge);
        _faceButton.Pressed += () => OnComponentPressed(ComponentMask.Face);
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
        _selectionMode.Changed += SyncButtons;
        SyncButtons();
    }

    private void Unbind()
    {
        if (_selectionMode is null)
            return;

        _selectionMode.Changed -= SyncButtons;
        _selectionMode = null;
    }

    private void OnObjectPressed()
    {
        _selectionMode?.EnterObjectMode();
        SyncButtons();
    }

    private void OnAllPressed()
    {
        _selectionMode?.SelectAllComponents();
        SyncButtons();
    }

    private void OnComponentPressed(ComponentMask component)
    {
        _selectionMode?.ToggleComponent(component);
        SyncButtons();
    }

    private void SyncButtons()
    {
        if (_selectionMode is null || _syncing)
            return;

        _syncing = true;
        try
        {
            bool isObject = _selectionMode.Mode == EditorMode.Object;
            bool isAll = _selectionMode.IsAllComponents;
            ComponentMask components = _selectionMode.Components;

            _objectButton.SetPressedNoSignal(isObject);
            _allButton.SetPressedNoSignal(isAll);
            _vertexButton.SetPressedNoSignal(
                !isObject && !isAll && components.HasFlag(ComponentMask.Vertex)
            );
            _edgeButton.SetPressedNoSignal(
                !isObject && !isAll && components.HasFlag(ComponentMask.Edge)
            );
            _faceButton.SetPressedNoSignal(
                !isObject && !isAll && components.HasFlag(ComponentMask.Face)
            );
        }
        finally
        {
            _syncing = false;
        }
    }
}

using Godot;

public partial class EditorToolBar : PanelContainer
{
    private Button _selectButton;
    private Button _editButton;
    private Button _createButton;
    private PrimitiveCreatePopup _primitiveCreatePopup;
    private EditorSession _session;

    public override void _Ready()
    {
        _selectButton = GetNode<Button>("VBoxContainer/Select");
        _editButton = GetNode<Button>("VBoxContainer/Edit");
        _createButton = GetNode<Button>("VBoxContainer/Create");
        _primitiveCreatePopup = GetNode<PrimitiveCreatePopup>("PrimitiveCreatePopup");
        _session = GetNodeOrNull<EditorSession>("%WORLD_ROOT");

        _selectButton.Pressed += OnSelectPressed;
        _editButton.Pressed += OnEditPressed;
        _createButton.Pressed += OnCreatePressed;
    }

    private void OnSelectPressed()
    {
        ActivatePersistentTool(EditorToolId.Select);
    }

    private void OnEditPressed()
    {
        ActivatePersistentTool(EditorToolId.Edit);
    }

    private void OnCreatePressed()
    {
        _primitiveCreatePopup.ToggleBeside(_createButton);
    }

    private void ActivatePersistentTool(EditorToolId toolId)
    {
        if (_session == null)
        {
            GD.PushWarning("EditorToolBar could not find EditorSession.");
            return;
        }

        _session.ActivatePersistentTool(toolId);
    }
}

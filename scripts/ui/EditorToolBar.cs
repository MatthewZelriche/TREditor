using Godot;

public partial class EditorToolBar : PanelContainer
{
    private Button _selectButton;
    private Button _editButton;
    private Button _createButton;
    private EditorSession _session;

    public override void _Ready()
    {
        _selectButton = GetNode<Button>("VBoxContainer/Select");
        _editButton = GetNode<Button>("VBoxContainer/Edit");
        _createButton = GetNode<Button>("VBoxContainer/Create");
        _session = GetNodeOrNull<EditorSession>("%WORLD_ROOT");

        _selectButton.Pressed += OnSelectPressed;
        _editButton.Pressed += OnEditPressed;
        _createButton.Pressed += OnCreatePressed;

        SelectButton(_selectButton);
    }

    private void OnSelectPressed()
    {
        ActivatePersistentTool(EditorToolId.Select, _selectButton);
    }

    private void OnEditPressed()
    {
        ActivatePersistentTool(EditorToolId.Edit, _editButton);
    }

    private void OnCreatePressed()
    {
        EditorSidePanel sidePanel = GetNodeOrNull<EditorSidePanel>("../EditorSidePanel");
        if (sidePanel == null)
        {
            GD.PushWarning("EditorToolBar could not find EditorSidePanel.");
            return;
        }

        sidePanel.OpenTab("Create");
        ActivatePersistentTool(EditorToolId.Create, _createButton);
    }

    private void ActivatePersistentTool(EditorToolId toolId, Button button)
    {
        if (_session == null)
        {
            GD.PushWarning("EditorToolBar could not find EditorSession.");
            return;
        }

        _session.ActivatePersistentTool(toolId);
        SelectButton(button);
    }

    private void SelectButton(Button selectedButton)
    {
        _selectButton.ButtonPressed = selectedButton == _selectButton;
        _editButton.ButtonPressed = selectedButton == _editButton;
        _createButton.ButtonPressed = selectedButton == _createButton;
    }
}

using Godot;

public partial class EditorToolBar : PanelContainer
{
    private Button _selectButton;
    private Button _editButton;
    private Button _createButton;
    private Button _textureButton;
    private EditorSession _session;

    public override void _Ready()
    {
        _selectButton = GetNode<Button>("VBoxContainer/Select");
        _editButton = GetNode<Button>("VBoxContainer/Edit");
        _createButton = GetNode<Button>("VBoxContainer/Create");
        _textureButton = GetNode<Button>("VBoxContainer/Texture");
        _session = GetNodeOrNull<EditorSession>("%WORLD_ROOT");

        _selectButton.Pressed += OnSelectPressed;
        _editButton.Pressed += OnEditPressed;
        _createButton.Pressed += OnCreatePressed;
        _textureButton.Pressed += OnTexturePressed;

        if (_session == null)
        {
            GD.PushWarning("EditorToolBar could not find EditorSession.");
            return;
        }

        _session.PersistentToolChanged += SelectTool;
        SelectTool(_session.ActivePersistentTool);
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
        OpenSidePanelTab("Create");
        ActivatePersistentTool(EditorToolId.Create);
    }

    private void OnTexturePressed()
    {
        OpenSidePanelTab("Textures");
        ActivatePersistentTool(EditorToolId.Texture);
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

    private void SelectTool(EditorToolId toolId)
    {
        _selectButton.SetPressedNoSignal(toolId == EditorToolId.Select);
        _editButton.SetPressedNoSignal(toolId == EditorToolId.Edit);
        _createButton.SetPressedNoSignal(toolId == EditorToolId.Create);
        _textureButton.SetPressedNoSignal(toolId == EditorToolId.Texture);
    }

    private void OpenSidePanelTab(string tabId)
    {
        EditorSidePanel sidePanel = GetNodeOrNull<EditorSidePanel>("../EditorSidePanel");
        if (sidePanel == null)
        {
            GD.PushWarning("EditorToolBar could not find EditorSidePanel.");
            return;
        }

        sidePanel.OpenTab(tabId);
    }

    public override void _ExitTree()
    {
        if (_session != null)
            _session.PersistentToolChanged -= SelectTool;
    }
}

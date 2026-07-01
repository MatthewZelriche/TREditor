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
        if (KeybindingService.Instance != null)
            KeybindingService.Instance.BindingChanged += OnBindingChanged;
        RefreshShortcutTooltips();
        SelectTool(_session.ActivePersistentTool);
    }

    private void OnSelectPressed()
    {
        ActivatePersistentTool(EditorToolId.Select);
    }

    private void OnEditPressed()
    {
        OpenSidePanelTab("Edit");
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
        if (KeybindingService.Instance != null)
            KeybindingService.Instance.BindingChanged -= OnBindingChanged;
    }

    private void OnBindingChanged(string actionId)
    {
        if (
            actionId
            is KeybindingActions.ToolSelect
                or KeybindingActions.ToolEdit
                or KeybindingActions.ToolCreate
                or KeybindingActions.ToolTexture
        )
        {
            RefreshShortcutTooltips();
        }
    }

    private void RefreshShortcutTooltips()
    {
        _selectButton.TooltipText = GetToolTooltip("Select Tool", KeybindingActions.ToolSelect);
        _editButton.TooltipText = GetToolTooltip("Edit Tool", KeybindingActions.ToolEdit);
        _createButton.TooltipText = GetToolTooltip("Create Tool", KeybindingActions.ToolCreate);
        _textureButton.TooltipText = GetToolTooltip("Texture Tool", KeybindingActions.ToolTexture);
    }

    private static string GetToolTooltip(string name, string actionId)
    {
        string binding = KeybindingService.Instance?.GetBindingDisplayText(actionId) ?? "Unbound";
        return $"{name} — {binding}";
    }
}

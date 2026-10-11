#nullable enable

using System;
using Godot;

public partial class ToolSettingsSidebar : Control
{
    private const float DefaultWidthRatio = 0.185f;
    private const float MinimumDefaultWidth = 220.0f;
    private const float MaximumDefaultWidth = 310.0f;
    private const float MinimumWidth = 180.0f;
    private const float MaximumWidth = 480.0f;
    private const float MinimumAdjacentContentWidth = 320.0f;
    private const float SeparatorWidth = 2.0f;

    [Export]
    public NodePath AdjacentContentPath { get; set; } = null!;

    [Export]
    public NodePath ToggleButtonPath { get; set; } = null!;

    private Control _layoutParent = null!;
    private Control _adjacentContent = null!;
    private Control _resizeHandle = null!;
    private Button _toggleButton = null!;
    private bool _isExpanded = true;
    private bool _hasCustomWidth;
    private bool _isResizing;
    private float _sidebarWidth;
    private float _resizeStartMouseX;
    private float _resizeStartWidth;
    private EditorToolRegistry? _tools;
    private Label _emptyLabel = null!;
    private VBoxContainer _settingsHost = null!;
    private HBoxContainer _primaryActionRow = null!;
    private Button _primaryActionButton = null!;
    private Label _primaryActionWarning = null!;
    private Control? _settingsControl;

    public override void _Ready()
    {
        _layoutParent = GetParent<Control>();
        _adjacentContent = GetNode<Control>(AdjacentContentPath);
        _toggleButton = GetNode<Button>(ToggleButtonPath);
        _resizeHandle = GetNode<Control>("ResizeHandle");
        BuildSettingsUi();

        _sidebarWidth = GetDefaultWidth();
        _toggleButton.Pressed += Toggle;
        _resizeHandle.GuiInput += OnResizeHandleGuiInput;
        _layoutParent.Resized += OnLayoutParentResized;
        ApplyExpandedState();
    }

    public override void _Input(InputEvent @event)
    {
        if (!_isResizing)
        {
            return;
        }

        if (@event is InputEventMouseMotion)
        {
            float mouseDelta = GetGlobalMousePosition().X - _resizeStartMouseX;
            _sidebarWidth = Mathf.Clamp(
                _resizeStartWidth - mouseDelta,
                MinimumWidth,
                GetMaximumWidth()
            );
            _hasCustomWidth = true;
            UpdateLayout();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (
            @event is InputEventMouseButton mouseButton
            && mouseButton.ButtonIndex == MouseButton.Left
            && !mouseButton.Pressed
        )
        {
            _isResizing = false;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _ExitTree()
    {
        Unbind();
    }

    public void Bind(EditorToolRegistry tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        Unbind();
        _tools = tools;
        _tools.ActiveToolChanged += OnActiveToolChanged;
        _tools.PrimaryActionChanged += SyncPrimaryAction;
        OnActiveToolChanged();
    }

    public void Toggle()
    {
        _isExpanded = !_isExpanded;
        ApplyExpandedState();
    }

    private void ApplyExpandedState()
    {
        _isResizing = false;
        Visible = _isExpanded;
        _toggleButton.Text = _isExpanded ? ">" : "<";
        _toggleButton.TooltipText = _isExpanded
            ? "Hide tool settings"
            : "Show tool settings";
        UpdateLayout();
    }

    private void OnResizeHandleGuiInput(InputEvent @event)
    {
        if (
            @event is not InputEventMouseButton mouseButton
            || mouseButton.ButtonIndex != MouseButton.Left
            || !mouseButton.Pressed
        )
        {
            return;
        }

        _isResizing = true;
        _resizeStartMouseX = GetGlobalMousePosition().X;
        _resizeStartWidth = _sidebarWidth;
        _resizeHandle.AcceptEvent();
    }

    private void OnLayoutParentResized()
    {
        _sidebarWidth = _hasCustomWidth
            ? Mathf.Clamp(_sidebarWidth, MinimumWidth, GetMaximumWidth())
            : GetDefaultWidth();
        UpdateLayout();
    }

    private void UpdateLayout()
    {
        if (!_isExpanded)
        {
            _adjacentContent.OffsetRight = 0.0f;
            return;
        }

        _sidebarWidth = Mathf.Clamp(_sidebarWidth, MinimumWidth, GetMaximumWidth());
        float totalWidth = _sidebarWidth + SeparatorWidth;
        OffsetLeft = -totalWidth;
        _adjacentContent.OffsetRight = -totalWidth;
    }

    private float GetDefaultWidth()
    {
        return Mathf.Clamp(
            _layoutParent.Size.X * DefaultWidthRatio,
            MinimumDefaultWidth,
            MaximumDefaultWidth
        );
    }

    private float GetMaximumWidth()
    {
        return Mathf.Max(
            MinimumWidth,
            Mathf.Min(
                MaximumWidth,
                _layoutParent.Size.X - MinimumAdjacentContentWidth - SeparatorWidth
            )
        );
    }

    // Creates the initial container for tool settings - tools provide the contents themselves
    private void BuildSettingsUi()
    {
        PanelContainer panel = GetNode<PanelContainer>("Panel");
        var margin = new MarginContainer { Name = "Margin" };
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        panel.AddChild(margin);

        var root = new VBoxContainer { Name = "Contents" };
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);
        root.AddChild(new Label { Text = "Tool Settings" });

        _emptyLabel = new Label { Text = "No settings for the active tool." };
        root.AddChild(_emptyLabel);

        _settingsHost = new VBoxContainer { Name = "SettingsHost" };
        _settingsHost.AddThemeConstantOverride("separation", 8);
        root.AddChild(_settingsHost);

        _primaryActionRow = new HBoxContainer { Name = "PrimaryActionRow" };
        _primaryActionRow.AddThemeConstantOverride("separation", 8);
        _primaryActionButton = new Button
        {
            Name = "PrimaryActionButton",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _primaryActionButton.Pressed += OnPrimaryActionPressed;
        _primaryActionRow.AddChild(_primaryActionButton);
        _primaryActionWarning = new Label
        {
            Name = "PrimaryActionWarning",
            Text = "\u26A0",
            MouseFilter = MouseFilterEnum.Stop,
            MouseDefaultCursorShape = CursorShape.Help,
        };
        _primaryActionWarning.AddThemeColorOverride("font_color", new Color("e5b94b"));
        _primaryActionRow.AddChild(_primaryActionWarning);
        root.AddChild(_primaryActionRow);
        _primaryActionRow.Visible = false;
    }

    private void OnActiveToolChanged()
    {
        ClearSettingsControl();
        if (_tools?.ActiveTool is IEditorToolSettingsProvider provider)
        {
            Control settingsControl = provider.CreateSettingsControl(_tools.Context)
                ?? throw new InvalidOperationException(
                    "A tool settings provider returned a null control."
                );
            try
            {
                _settingsHost.AddChild(settingsControl);
            }
            catch
            {
                settingsControl.Free();
                throw;
            }

            _settingsControl = settingsControl;
        }

        _emptyLabel.Visible =
            _settingsControl is null
            && _tools?.ActiveTool is not IEditorToolPrimaryAction;
        SyncPrimaryAction();
    }

    private void SyncPrimaryAction()
    {
        if (_tools?.ActiveTool is not IEditorToolPrimaryAction action)
        {
            _primaryActionRow.Visible = false;
            return;
        }

        EditorToolAction primaryAction = action.CreatePrimaryAction(_tools.Context);
        string tooltip = primaryAction.UnavailableReason ?? string.Empty;
        _primaryActionButton.Text = action.PrimaryActionText;
        _primaryActionButton.Disabled = primaryAction.Command is null;
        _primaryActionButton.TooltipText = tooltip;
        _primaryActionWarning.Visible = primaryAction.Command is null;
        _primaryActionWarning.TooltipText = tooltip;
        _primaryActionRow.Visible = true;
    }

    private void OnPrimaryActionPressed()
    {
        if (_tools?.TryExecutePrimaryAction() is { } failure)
            GetNode<Root>("../..").ShowWarningToast(failure);
    }

    private void Unbind()
    {
        if (_tools is not null)
        {
            _tools.ActiveToolChanged -= OnActiveToolChanged;
            _tools.PrimaryActionChanged -= SyncPrimaryAction;
        }

        ClearSettingsControl();
        _primaryActionRow.Visible = false;
        _tools = null;
    }

    private void ClearSettingsControl()
    {
        if (_settingsControl is null)
            return;

        if (_settingsControl.GetParent() is not null)
            _settingsHost.RemoveChild(_settingsControl);
        _settingsControl.Free();
        _settingsControl = null;
    }
}

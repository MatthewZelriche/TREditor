#nullable enable

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

    public override void _Ready()
    {
        _layoutParent = GetParent<Control>();
        _adjacentContent = GetNode<Control>(AdjacentContentPath);
        _toggleButton = GetNode<Button>(ToggleButtonPath);
        _resizeHandle = GetNode<Control>("ResizeHandle");

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
}

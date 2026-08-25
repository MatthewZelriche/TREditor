#nullable enable

using Godot;

public partial class WindowTitleBar : PanelContainer
{
    private const int WindowDragThreshold = 4;

    private string _titleOverride = string.Empty;
    private bool _showTitle = true;
    private bool _resizable = true;
    private bool _showMinimizeButton = true;
    private bool _showMaximizeButton = true;
    private bool _isMaximized;
    private bool _hasRestoreBounds;
    private Vector2I _restorePosition;
    private Vector2I _restoreSize;
    private bool _windowDragPending;
    private Vector2I _windowDragStart;

    private Window? _window;
    private Label? _titleLabel;
    private Button? _minimizeButton;
    private Button? _maximizeButton;

    [Export]
    public string TitleOverride
    {
        get => _titleOverride;
        set
        {
            _titleOverride = value;
            RefreshTitle();
        }
    }

    [Export]
    public bool ShowTitle
    {
        get => _showTitle;
        set
        {
            _showTitle = value;
            RefreshControlVisibility();
        }
    }

    [Export]
    public bool Resizable
    {
        get => _resizable;
        set
        {
            _resizable = value;
            ApplyWindowConfiguration();
        }
    }

    [Export]
    public bool ShowMinimizeButton
    {
        get => _showMinimizeButton;
        set
        {
            _showMinimizeButton = value;
            ApplyWindowConfiguration();
        }
    }

    [Export]
    public bool ShowMaximizeButton
    {
        get => _showMaximizeButton;
        set
        {
            _showMaximizeButton = value;
            ApplyWindowConfiguration();
        }
    }

    public override void _Ready()
    {
        _window = GetWindow();
        _titleLabel = GetNode<Label>("%WindowTitle");
        _minimizeButton = GetNode<Button>("%MinimizeButton");
        _maximizeButton = GetNode<Button>("%MaximizeButton");
        _isMaximized = _window.Mode == Window.ModeEnum.Maximized;

        _window.TitleChanged += RefreshTitle;
        _window.SizeChanged += RefreshWindowState;

        RefreshTitle();
        ApplyWindowConfiguration();
        RefreshWindowState();
    }

    public override void _ExitTree()
    {
        if (_window is not null)
        {
            _window.TitleChanged -= RefreshTitle;
            _window.SizeChanged -= RefreshWindowState;
        }

        _window = null;
        _titleLabel = null;
        _minimizeButton = null;
        _maximizeButton = null;
    }

    // Wired from .tscn.
    private void OnDragRegionGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex != MouseButton.Left)
                return;

            if (!mouseButton.Pressed)
            {
                _windowDragPending = false;
                return;
            }

            if (mouseButton.DoubleClick)
            {
                _windowDragPending = false;
                ToggleMaximizeWindow();
                AcceptEvent();
                return;
            }

            _windowDragPending = true;
            _windowDragStart = DisplayServer.MouseGetPosition();
            AcceptEvent();
            return;
        }

        if (
            @event is not InputEventMouseMotion
            || !_windowDragPending
            || !Input.IsMouseButtonPressed(MouseButton.Left)
        )
        {
            return;
        }

        Vector2I dragDistance = DisplayServer.MouseGetPosition() - _windowDragStart;
        if (dragDistance.LengthSquared() < WindowDragThreshold * WindowDragThreshold)
            return;

        _windowDragPending = false;

        if (
            _window is null
            || !DisplayServer.HasFeature(DisplayServer.Feature.WindowDrag)
        )
            return;

        if (_isMaximized)
            RestoreWindowForDrag();

        DisplayServer.WindowStartDrag(_window.GetWindowId());
        AcceptEvent();
    }

    private void MinimizeWindow()
    {
        if (_window is not null && !_window.MinimizeDisabled)
            _window.Mode = Window.ModeEnum.Minimized;
    }

    private void ToggleMaximizeWindow()
    {
        if (_window is null || _window.MaximizeDisabled)
            return;

        if (_isMaximized)
        {
            RestoreWindow();
            return;
        }

        _restorePosition = _window.Position;
        _restoreSize = _window.Size;
        _hasRestoreBounds = true;
        _isMaximized = true;
        _window.Mode = Window.ModeEnum.Maximized;
        RefreshWindowState();
    }

    private void RestoreWindow()
    {
        if (_window is null)
            return;

        _isMaximized = false;
        _window.Mode = Window.ModeEnum.Windowed;

        if (_hasRestoreBounds)
        {
            _window.Size = _restoreSize;
            _window.Position = _restorePosition;
        }

        RefreshWindowState();
    }

    private void RestoreWindowForDrag()
    {
        if (_window is null)
            return;

        Vector2 pointerInWindow = GetViewport().GetMousePosition();
        Vector2I pointerOnScreen = DisplayServer.MouseGetPosition();
        Vector2I restoredSize = _hasRestoreBounds ? _restoreSize : new Vector2I(960, 540);
        float horizontalRatio = Mathf.Clamp(
            pointerInWindow.X / Mathf.Max(_window.Size.X, 1),
            0.0f,
            1.0f
        );
        int titleBarOffset = Mathf.Clamp(Mathf.RoundToInt(pointerInWindow.Y), 0, 35);

        _isMaximized = false;
        _window.Mode = Window.ModeEnum.Windowed;
        _window.Size = restoredSize;
        _window.Position = new Vector2I(
            pointerOnScreen.X - Mathf.RoundToInt(restoredSize.X * horizontalRatio),
            pointerOnScreen.Y - titleBarOffset
        );
        RefreshWindowState();
    }

    private void RequestClose() => _window?.EmitSignal(Window.SignalName.CloseRequested);

    private void RefreshTitle()
    {
        if (_titleLabel is null)
            return;

        string windowTitle = _window?.Title ?? string.Empty;
        _titleLabel.Text = string.IsNullOrWhiteSpace(_titleOverride)
            ? windowTitle
            : _titleOverride;
    }

    private void ApplyWindowConfiguration()
    {
        if (_window is not null)
        {
            _window.Unresizable = !_resizable;
            _window.MinimizeDisabled = !_resizable || !_showMinimizeButton;
            _window.MaximizeDisabled = !_resizable || !_showMaximizeButton;
        }

        RefreshControlVisibility();
        RefreshWindowState();
    }

    private void RefreshControlVisibility()
    {
        if (_titleLabel is not null)
            _titleLabel.Visible = _showTitle;

        if (_minimizeButton is not null)
            _minimizeButton.Visible = _resizable && _showMinimizeButton;

        if (_maximizeButton is not null)
            _maximizeButton.Visible = _resizable && _showMaximizeButton;
    }

    private void RefreshWindowState()
    {
        if (_window is null || _minimizeButton is null || _maximizeButton is null)
            return;

        _maximizeButton.Text = _isMaximized ? "❐" : "□";
        _maximizeButton.TooltipText = _isMaximized ? "Restore" : "Maximize";
        _maximizeButton.Disabled = _window.MaximizeDisabled;
        _minimizeButton.Disabled = _window.MinimizeDisabled;
    }

}

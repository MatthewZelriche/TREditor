#nullable enable

using System;
using System.Collections.Generic;
using Godot;

public partial class AppTitleBar : PanelContainer
{
    private const int WindowDragThreshold = 4;

    public enum AppMenuAction
    {
        NewDocument = 100,
        OpenDocument = 101,
        SaveDocument = 102,
        SaveDocumentAs = 103,
        Quit = 104,

        Undo = 200,
        Redo = 201,

        Keybindings = 300,
        Preferences = 301,

        OpenSourceLicenses = 400,
        About = 401,
    }

    [Signal]
    public delegate void MenuActionRequestedEventHandler(int actionId);

    private readonly Dictionary<AppMenuAction, (PopupMenu Menu, int Index)> _menuItems = [];

    private string _titleOverride = string.Empty;
    private Texture2D? _applicationIcon;
    private bool _showWindowControls = true;
    private bool _isMaximized;
    private bool _hasRestoreBounds;
    private Vector2I _restorePosition;
    private Vector2I _restoreSize;
    private bool _windowDragPending;
    private Vector2I _windowDragStart;

    private Window? _window;
    private TextureRect? _icon;
    private Label? _titleLabel;
    private Control? _dragRegion;
    private HBoxContainer? _windowControls;
    private Button? _minimizeButton;
    private Button? _maximizeButton;
    private Button? _closeButton;

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
    public Texture2D? ApplicationIcon
    {
        get => _applicationIcon;
        set
        {
            _applicationIcon = value;
            RefreshApplicationIcon();
        }
    }

    [Export]
    public bool ShowWindowControls
    {
        get => _showWindowControls;
        set
        {
            _showWindowControls = value;
            RefreshWindowControlsVisibility();
        }
    }

    public override void _Ready()
    {
        _window = GetWindow();
        _icon = GetNode<TextureRect>("%ApplicationIcon");
        _titleLabel = GetNode<Label>("%ApplicationTitle");
        _dragRegion = GetNode<Control>("%DragRegion");
        _windowControls = GetNode<HBoxContainer>("%WindowControls");
        _minimizeButton = GetNode<Button>("%MinimizeButton");
        _maximizeButton = GetNode<Button>("%MaximizeButton");
        _closeButton = GetNode<Button>("%CloseButton");

        IndexMenuItems();
        ConfigureMenuAccelerators();

        _window.TitleChanged += RefreshTitle;
        _window.SizeChanged += RefreshWindowState;

        RefreshTitle();
        RefreshApplicationIcon();
        RefreshWindowControlsVisibility();
        RefreshWindowState();
    }

    public override void _ExitTree()
    {
        if (_window is not null)
        {
            _window.TitleChanged -= RefreshTitle;
            _window.SizeChanged -= RefreshWindowState;
        }

        _menuItems.Clear();
    }

    public void SetMenuActionDisabled(AppMenuAction action, bool disabled)
    {
        if (_menuItems.TryGetValue(action, out var item))
        {
            item.Menu.SetItemDisabled(item.Index, disabled);
        }
    }

    private void IndexMenuItems()
    {
        _menuItems.Clear();

        IndexMenuItems(GetNode<PopupMenu>("%FileMenu"));
        IndexMenuItems(GetNode<PopupMenu>("%EditMenu"));
        IndexMenuItems(GetNode<PopupMenu>("%SettingsMenu"));
        IndexMenuItems(GetNode<PopupMenu>("%AboutMenu"));
    }

    private void IndexMenuItems(PopupMenu menu)
    {
        for (int index = 0; index < menu.ItemCount; index++)
        {
            int id = menu.GetItemId(index);
            if (Enum.IsDefined(typeof(AppMenuAction), id))
            {
                _menuItems.Add((AppMenuAction)id, (menu, index));
            }
        }
    }

    private void ConfigureMenuAccelerators()
    {
        Key control = (Key)KeyModifierMask.MaskCtrl;
        Key controlShift = (Key)(KeyModifierMask.MaskCtrl | KeyModifierMask.MaskShift);

        SetMenuActionAccelerator(AppMenuAction.NewDocument, control | Key.N);
        SetMenuActionAccelerator(AppMenuAction.OpenDocument, control | Key.O);
        SetMenuActionAccelerator(AppMenuAction.SaveDocument, control | Key.S);
        SetMenuActionAccelerator(AppMenuAction.SaveDocumentAs, controlShift | Key.S);
        SetMenuActionAccelerator(AppMenuAction.Undo, control | Key.Z);
        SetMenuActionAccelerator(AppMenuAction.Redo, control | Key.Y);
    }

    private void SetMenuActionAccelerator(AppMenuAction action, Key accelerator)
    {
        if (_menuItems.TryGetValue(action, out var item))
        {
            item.Menu.SetItemAccelerator(item.Index, accelerator);
        }
    }

    // Wired from .tscn
    private void OnMenuIdPressed(long id)
    {
        if (Enum.IsDefined(typeof(AppMenuAction), (int)id))
        {
            EmitSignal(SignalName.MenuActionRequested, (int)id);
        }
    }

    // Handles clicks on drag area to window drag or maximize
    private void OnDragRegionGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex != MouseButton.Left)
            {
                return;
            }

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
        {
            return;
        }

        _windowDragPending = false;

        if (!DisplayServer.HasFeature(DisplayServer.Feature.WindowDrag))
        {
            return;
        }

        if (_isMaximized)
        {
            RestoreWindowForDrag();
        }

        DisplayServer.WindowStartDrag();
        AcceptEvent();
    }

    private void MinimizeWindow()
    {
        if (_window is not null && !_window.MinimizeDisabled)
        {
            _window.Mode = Window.ModeEnum.Minimized;
        }
    }

    private void ToggleMaximizeWindow()
    {
        if (_window is null || _window.MaximizeDisabled)
        {
            return;
        }

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
        {
            return;
        }

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
        {
            return;
        }

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

    private void RequestClose()
    {
        _window?.EmitSignal(Window.SignalName.CloseRequested);
    }

    private void RefreshTitle()
    {
        if (_titleLabel is null)
        {
            return;
        }

        string windowTitle = _window?.Title ?? string.Empty;
        _titleLabel.Text = string.IsNullOrWhiteSpace(_titleOverride)
            ? windowTitle
            : _titleOverride;
    }

    private void RefreshApplicationIcon()
    {
        if (_icon is null)
        {
            return;
        }

        _icon.Texture = _applicationIcon;
        _icon.Visible = _applicationIcon is not null;
    }

    private void RefreshWindowControlsVisibility()
    {
        if (_windowControls is not null)
        {
            _windowControls.Visible = _showWindowControls;
        }
    }

    private void RefreshWindowState()
    {
        if (
            _window is null
            || _minimizeButton is null
            || _maximizeButton is null
            || _closeButton is null
        )
        {
            return;
        }

        bool isMaximized = _isMaximized;
        _maximizeButton.Text = isMaximized ? "❐" : "□";
        _maximizeButton.TooltipText = isMaximized ? "Restore" : "Maximize";
        _maximizeButton.Disabled = _window.MaximizeDisabled;
        _minimizeButton.Disabled = _window.MinimizeDisabled;
        _closeButton.Disabled = false;
    }
}

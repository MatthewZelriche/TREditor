using System;
using Godot;

public partial class ViewportPane : Control
{
    private const string ScenePath = "res://scripts/ui/viewportWorkspace/ViewportPane.tscn";

    private ViewportWorkspace _workspace;
    private PaneDropOverlay _dropOverlay;
    private ViewportPaneDragHandle _dragHandle;
    private SubViewportContainer _viewportContainer;
    private Label _titleLabel;
    private MenuButton _splitButton;
    private Button _closeButton;
    private bool _uiWired;

    public string PaneId { get; private set; } = "";
    public string Title { get; private set; } = "";
    public Color PaneColor { get; private set; }

    public static ViewportPane Create()
    {
        PackedScene scene = ResourceLoader.Load<PackedScene>(ScenePath);
        if (scene == null)
        {
            throw new InvalidOperationException($"Unable to load viewport pane scene: {ScenePath}");
        }

        return scene.Instantiate<ViewportPane>();
    }

    public void Initialize(ViewportWorkspace workspace, string paneId, string title, Color color)
    {
        _workspace = workspace;
        PaneId = paneId;
        Title = title;
        PaneColor = color;
        Name = $"ViewportPane_{paneId.Replace("-", "_")}";

        CacheSceneNodes();
        ApplyPaneState();
        WireSceneNodes();
        HideDropOverlay();
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (
            _workspace == null
            || !_workspace.CanDropPaneData(data, PaneId, out _)
            || _dropOverlay == null
        )
        {
            HideDropOverlay();
            return false;
        }

        _dropOverlay.ShowZone(GetDropZone(atPosition));
        return true;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (
            _workspace != null
            && _workspace.CanDropPaneData(data, PaneId, out string draggedPaneId)
        )
        {
            _workspace.DropPane(draggedPaneId, PaneId, GetDropZone(atPosition));
        }

        HideDropOverlay();
    }

    internal void HideDropOverlay()
    {
        _dropOverlay?.HideZone();
    }

    internal void SetViewportInputEnabled(bool enabled)
    {
        if (_viewportContainer != null)
        {
            // Pass lets SubViewportContainer forward events to the camera. During pane drags,
            // Ignore lets the parent ViewportPane remain the drop target over the viewport body.
            _viewportContainer.MouseFilter = enabled
                ? MouseFilterEnum.Pass
                : MouseFilterEnum.Ignore;
        }
    }

    internal void BeginDrag()
    {
        _workspace?.BeginPaneDrag(PaneId);
    }

    internal Control CreateDragPreview()
    {
        PanelContainer preview = new() { CustomMinimumSize = new Vector2(160.0f, 32.0f) };
        StyleBoxFlat style = new()
        {
            BgColor = new Color(0.10f, 0.12f, 0.14f, 0.92f),
            BorderColor = new Color(0.44f, 0.62f, 0.82f, 1.0f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 10.0f,
            ContentMarginRight = 10.0f,
            ContentMarginTop = 6.0f,
            ContentMarginBottom = 6.0f,
        };
        preview.AddThemeStyleboxOverride("panel", style);
        preview.AddChild(new Label { Text = Title });
        return preview;
    }

    private void CacheSceneNodes()
    {
        _dropOverlay = GetNode<PaneDropOverlay>("DropOverlay");
        _dragHandle = GetNode<ViewportPaneDragHandle>("Frame/Column/Header/Row/DragHandle");
        _viewportContainer = GetNode<SubViewportContainer>("Frame/Column/ViewportContainer");
        _titleLabel = GetNode<Label>("Frame/Column/Header/Row/TitleLabel");
        _splitButton = GetNode<MenuButton>("Frame/Column/Header/Row/SplitButton");
        _closeButton = GetNode<Button>("Frame/Column/Header/Row/CloseButton");
    }

    private void ApplyPaneState()
    {
        _titleLabel.Text = Title;
    }

    private void WireSceneNodes()
    {
        if (_uiWired)
        {
            return;
        }

        MouseExited += HideDropOverlay;
        _dragHandle.Initialize(this);
        _closeButton.Pressed += OnClosePressed;

        PopupMenu popup = _splitButton.GetPopup();
        foreach (ViewportSplitOption option in ViewportWorkspace.SplitOptions)
        {
            popup.AddItem(option.Label, (int)option.Zone);
        }

        popup.IdPressed += OnSplitMenuIdPressed;

        _uiWired = true;
    }

    private void OnClosePressed()
    {
        _workspace?.ClosePane(this);
    }

    private void OnSplitMenuIdPressed(long id)
    {
        _workspace?.SplitPane(this, (ViewportDropZone)(int)id);
    }

    private ViewportDropZone GetDropZone(Vector2 position)
    {
        if (Size.X <= 0.0f || Size.Y <= 0.0f)
        {
            return ViewportDropZone.Right;
        }

        float left = position.X;
        float right = Size.X - position.X;
        float top = position.Y;
        float bottom = Size.Y - position.Y;
        float nearest = Mathf.Min(Mathf.Min(left, right), Mathf.Min(top, bottom));

        if (nearest == left)
        {
            return ViewportDropZone.Left;
        }

        if (nearest == right)
        {
            return ViewportDropZone.Right;
        }

        return nearest == top ? ViewportDropZone.Top : ViewportDropZone.Bottom;
    }
}

using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

public enum ViewportDropZone
{
    Left,
    Right,
    Top,
    Bottom,
}

public partial class ViewportWorkspace : TilingContainer
{
    private const string LayoutPath = "user://viewport_layout.json";
    private const int LayoutVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    private static readonly Color[] PanePalette =
    [
        new Color(0.12f, 0.15f, 0.17f),
        new Color(0.16f, 0.13f, 0.18f),
        new Color(0.13f, 0.17f, 0.14f),
        new Color(0.19f, 0.16f, 0.12f),
        new Color(0.11f, 0.15f, 0.21f),
        new Color(0.18f, 0.12f, 0.14f),
    ];

    private readonly Dictionary<string, ViewportPane> _panesById = [];
    private string _activeDragPaneId = "";
    private int _nextPaneNumber = 1;
    private bool _suppressPersistence;
    private PopupMenu _viewMenu;

    private enum WorkspaceLayoutPreset
    {
        Single = 1,
        TwoColumns = 2,
        TwoRows = 3,
        Quad = 4,
        ResetLayout = 5,
    }

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        BorderThickness = 1.0f;
        GrabThickness = 12.0f;
        BorderColor = new Color(0.30f, 0.33f, 0.36f);
        MinimumPaneSize = 150.0f;

        Connect(SignalName.LayoutChanged, Callable.From(OnTilingLayoutChanged));

        _suppressPersistence = true;
        bool restored = RestoreSavedLayout();
        if (!restored)
        {
            ApplyPreset(WorkspaceLayoutPreset.Single, false);
        }

        _suppressPersistence = false;
        SaveLayout();
        SetupLayoutMenu();
    }

    internal void BeginPaneDrag(string paneId)
    {
        _activeDragPaneId = paneId;
    }

    internal void EndPaneDrag()
    {
        _activeDragPaneId = "";
        HideDropOverlays();
    }

    internal bool CanDropPaneData(Variant data, string targetPaneId, out string draggedPaneId)
    {
        draggedPaneId = "";

        if (data.VariantType != Variant.Type.String)
        {
            return false;
        }

        draggedPaneId = data.AsString();
        return draggedPaneId == _activeDragPaneId
            && draggedPaneId != targetPaneId
            && _panesById.ContainsKey(draggedPaneId)
            && _panesById.ContainsKey(targetPaneId);
    }

    internal void DropPane(string draggedPaneId, string targetPaneId, ViewportDropZone zone)
    {
        if (
            !_panesById.TryGetValue(draggedPaneId, out ViewportPane draggedPane)
            || !_panesById.TryGetValue(targetPaneId, out ViewportPane targetPane)
            || draggedPane == targetPane
        )
        {
            EndPaneDrag();
            return;
        }

        bool changed = zone switch
        {
            ViewportDropZone.Left
                => MovePane(
                    draggedPane,
                    targetPane,
                    SplitAxis.Horizontal,
                    InsertPlacement.Before
                ),
            ViewportDropZone.Right
                => MovePane(draggedPane, targetPane, SplitAxis.Horizontal, InsertPlacement.After),
            ViewportDropZone.Top
                => MovePane(draggedPane, targetPane, SplitAxis.Vertical, InsertPlacement.Before),
            ViewportDropZone.Bottom
                => MovePane(draggedPane, targetPane, SplitAxis.Vertical, InsertPlacement.After),
            _ => false,
        };

        EndPaneDrag();
        if (changed)
        {
            SaveLayout();
        }
    }

    internal void SplitPane(ViewportPane targetPane, ViewportDropZone zone)
    {
        if (targetPane == null || !_panesById.ContainsKey(targetPane.PaneId))
        {
            return;
        }

        ViewportPane newPane = CreateNewPane();
        bool inserted = zone switch
        {
            ViewportDropZone.Left
                => InsertSplit(
                    targetPane,
                    newPane,
                    SplitAxis.Horizontal,
                    InsertPlacement.Before
                ),
            ViewportDropZone.Right
                => InsertSplit(targetPane, newPane, SplitAxis.Horizontal, InsertPlacement.After),
            ViewportDropZone.Top
                => InsertSplit(targetPane, newPane, SplitAxis.Vertical, InsertPlacement.Before),
            ViewportDropZone.Bottom
                => InsertSplit(targetPane, newPane, SplitAxis.Vertical, InsertPlacement.After),
            _ => false,
        };

        if (!inserted)
        {
            _panesById.Remove(newPane.PaneId);
            newPane.QueueFree();
            return;
        }

        SaveLayout();
    }

    internal void ClosePane(ViewportPane pane)
    {
        if (pane == null || _panesById.Count <= 1)
        {
            return;
        }

        _panesById.Remove(pane.PaneId);
        RemovePane(pane, true);
        SaveLayout();
    }

    private void OnTilingLayoutChanged()
    {
        if (!_suppressPersistence)
        {
            SaveLayout();
        }
    }

    private void ApplyPreset(WorkspaceLayoutPreset preset, bool save = true)
    {
        bool wasSuppressingPersistence = _suppressPersistence;
        _suppressPersistence = true;

        List<ViewportPane> oldPanes = new(_panesById.Values);
        _panesById.Clear();

        ViewportPane first = CreateNewPane();
        SetRoot(first);

        switch (preset)
        {
            case WorkspaceLayoutPreset.TwoColumns:
                InsertSplit(first, CreateNewPane(), SplitAxis.Horizontal);
                break;

            case WorkspaceLayoutPreset.TwoRows:
                InsertSplit(first, CreateNewPane(), SplitAxis.Vertical);
                break;

            case WorkspaceLayoutPreset.Quad:
                ViewportPane second = CreateNewPane();
                ViewportPane third = CreateNewPane();
                ViewportPane fourth = CreateNewPane();

                InsertSplit(first, second, SplitAxis.Horizontal);
                InsertSplitRoot(third, SplitAxis.Vertical);
                InsertSplit(third, fourth, SplitAxis.Horizontal);
                break;
        }

        foreach (ViewportPane oldPane in oldPanes)
        {
            if (GodotObject.IsInstanceValid(oldPane))
            {
                oldPane.QueueFree();
            }
        }

        _suppressPersistence = wasSuppressingPersistence;
        if (save && !_suppressPersistence)
        {
            SaveLayout();
        }
    }

    private ViewportPane CreateNewPane()
    {
        int paneNumber = _nextPaneNumber++;
        return CreatePane(
            $"viewport-{paneNumber}",
            $"Viewport {paneNumber}",
            PanePalette[(paneNumber - 1) % PanePalette.Length],
            true
        );
    }

    private ViewportPane CreatePane(string paneId, string title, Color color, bool register)
    {
        ViewportPane pane = new();
        pane.Initialize(this, paneId, title, color);

        if (register)
        {
            _panesById[paneId] = pane;
        }

        return pane;
    }

    private void SaveLayout()
    {
        LayoutSnapshotNode snapshot = CaptureLayout(control =>
            control is ViewportPane pane ? pane.PaneId : ""
        );
        if (snapshot == null)
        {
            return;
        }

        PersistedLayoutNode root = ToPersistedNode(snapshot);
        if (root == null)
        {
            return;
        }

        PersistedLayout layout =
            new()
            {
                Version = LayoutVersion,
                NextPaneNumber = _nextPaneNumber,
                Root = root,
                Panes = CapturePersistedPanes(),
            };

        try
        {
            using FileAccess file = FileAccess.Open(LayoutPath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PushWarning($"Unable to save viewport layout: {FileAccess.GetOpenError()}");
                return;
            }

            file.StoreString(JsonSerializer.Serialize(layout, JsonOptions));
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Unable to save viewport layout: {exception.Message}");
        }
    }

    private bool RestoreSavedLayout()
    {
        if (!FileAccess.FileExists(LayoutPath))
        {
            return false;
        }

        PersistedLayout layout;
        try
        {
            using FileAccess file = FileAccess.Open(LayoutPath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PushWarning($"Unable to read viewport layout: {FileAccess.GetOpenError()}");
                return false;
            }

            layout = JsonSerializer.Deserialize<PersistedLayout>(file.GetAsText(), JsonOptions);
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Unable to read viewport layout: {exception.Message}");
            return false;
        }

        if (layout?.Root == null || layout.Panes == null || layout.Panes.Count == 0)
        {
            return false;
        }

        Dictionary<string, PersistedPane> persistedPanes = [];
        foreach (PersistedPane pane in layout.Panes)
        {
            if (!string.IsNullOrEmpty(pane.Id))
            {
                persistedPanes[pane.Id] = pane;
            }
        }

        LayoutSnapshotNode snapshot = ToLayoutSnapshot(layout.Root);
        if (snapshot == null)
        {
            return false;
        }

        Dictionary<string, ViewportPane> restoredPanes = [];
        bool restored = RestoreLayout(
            snapshot,
            paneId =>
            {
                if (
                    restoredPanes.ContainsKey(paneId)
                    || !persistedPanes.TryGetValue(paneId, out PersistedPane persistedPane)
                )
                {
                    return null;
                }

                ViewportPane pane = CreatePane(
                    persistedPane.Id,
                    persistedPane.Title,
                    persistedPane.ToColor(),
                    false
                );
                restoredPanes[paneId] = pane;
                return pane;
            },
            true
        );

        if (!restored)
        {
            foreach (ViewportPane pane in restoredPanes.Values)
            {
                if (GodotObject.IsInstanceValid(pane))
                {
                    pane.QueueFree();
                }
            }

            return false;
        }

        _panesById.Clear();
        foreach ((string id, ViewportPane pane) in restoredPanes)
        {
            _panesById[id] = pane;
        }

        _nextPaneNumber = Math.Max(layout.NextPaneNumber, GetMinimumNextPaneNumber());
        return true;
    }

    private List<PersistedPane> CapturePersistedPanes()
    {
        List<PersistedPane> panes = [];
        foreach (Control control in GetPanes())
        {
            if (control is not ViewportPane pane)
            {
                continue;
            }

            panes.Add(
                new PersistedPane
                {
                    Id = pane.PaneId,
                    Title = pane.Title,
                    Red = pane.PaneColor.R,
                    Green = pane.PaneColor.G,
                    Blue = pane.PaneColor.B,
                    Alpha = pane.PaneColor.A,
                }
            );
        }

        return panes;
    }

    private static PersistedLayoutNode ToPersistedNode(LayoutSnapshotNode snapshot)
    {
        switch (snapshot)
        {
            case PaneLayoutSnapshotNode pane:
                return new PersistedLayoutNode { Type = "pane", PaneId = pane.PaneId };

            case SplitLayoutSnapshotNode split:
                PersistedLayoutNode first = ToPersistedNode(split.First);
                PersistedLayoutNode second = ToPersistedNode(split.Second);
                if (first == null || second == null)
                {
                    return null;
                }

                return new PersistedLayoutNode
                {
                    Type = "split",
                    Axis = split.Axis.ToString(),
                    Ratio = split.Ratio,
                    First = first,
                    Second = second,
                };
        }

        return null;
    }

    private static LayoutSnapshotNode ToLayoutSnapshot(PersistedLayoutNode node)
    {
        if (node == null)
        {
            return null;
        }

        if (node.Type == "pane")
        {
            return string.IsNullOrEmpty(node.PaneId) ? null : new PaneLayoutSnapshotNode(node.PaneId);
        }

        if (
            node.Type != "split"
            || !Enum.TryParse(node.Axis, out SplitAxis axis)
            || node.First == null
            || node.Second == null
        )
        {
            return null;
        }

        LayoutSnapshotNode first = ToLayoutSnapshot(node.First);
        LayoutSnapshotNode second = ToLayoutSnapshot(node.Second);
        return first == null || second == null
            ? null
            : new SplitLayoutSnapshotNode(axis, node.Ratio, first, second);
    }

    private int GetMinimumNextPaneNumber()
    {
        int minimum = 1;
        foreach (string paneId in _panesById.Keys)
        {
            if (
                paneId.StartsWith("viewport-", StringComparison.Ordinal)
                && int.TryParse(paneId["viewport-".Length..], out int paneNumber)
            )
            {
                minimum = Math.Max(minimum, paneNumber + 1);
            }
        }

        return minimum;
    }

    private void SetupLayoutMenu()
    {
        MenuBar menuBar = FindAncestorMenuBar();
        if (menuBar == null)
        {
            return;
        }

        _viewMenu = menuBar.GetNodeOrNull<PopupMenu>("View");
        if (_viewMenu == null)
        {
            _viewMenu = new PopupMenu { Name = "View" };
            menuBar.AddChild(_viewMenu);
        }
        else if (_viewMenu.ItemCount > 0)
        {
            _viewMenu.AddSeparator();
        }

        _viewMenu.AddItem("Single", (int)WorkspaceLayoutPreset.Single);
        _viewMenu.AddItem("Two Columns", (int)WorkspaceLayoutPreset.TwoColumns);
        _viewMenu.AddItem("Two Rows", (int)WorkspaceLayoutPreset.TwoRows);
        _viewMenu.AddItem("Quad", (int)WorkspaceLayoutPreset.Quad);
        _viewMenu.AddSeparator();
        _viewMenu.AddItem("Reset Layout", (int)WorkspaceLayoutPreset.ResetLayout);
        _viewMenu.IdPressed += OnLayoutMenuIdPressed;
    }

    private MenuBar FindAncestorMenuBar()
    {
        Node current = this;
        while (current.GetParent() != null)
        {
            current = current.GetParent();
            foreach (Node child in current.GetChildren())
            {
                if (child is MenuBar menuBar)
                {
                    return menuBar;
                }
            }
        }

        return null;
    }

    private void OnLayoutMenuIdPressed(long id)
    {
        ApplyPreset((WorkspaceLayoutPreset)(int)id);
    }

    private void HideDropOverlays()
    {
        foreach (ViewportPane pane in _panesById.Values)
        {
            if (GodotObject.IsInstanceValid(pane))
            {
                pane.HideDropOverlay();
            }
        }
    }

    private sealed class PersistedLayout
    {
        public int Version { get; set; } = LayoutVersion;
        public int NextPaneNumber { get; set; } = 1;
        public PersistedLayoutNode Root { get; set; }
        public List<PersistedPane> Panes { get; set; } = [];
    }

    private sealed class PersistedLayoutNode
    {
        public string Type { get; set; } = "";
        public string PaneId { get; set; } = "";
        public string Axis { get; set; } = "";
        public float Ratio { get; set; } = 0.5f;
        public PersistedLayoutNode First { get; set; }
        public PersistedLayoutNode Second { get; set; }
    }

    private sealed class PersistedPane
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public float Red { get; set; } = 0.12f;
        public float Green { get; set; } = 0.14f;
        public float Blue { get; set; } = 0.17f;
        public float Alpha { get; set; } = 1.0f;

        public Color ToColor()
        {
            return new Color(Red, Green, Blue, Alpha);
        }
    }
}

public partial class ViewportPane : Control
{
    private ViewportWorkspace _workspace;
    private PaneDropOverlay _dropOverlay;

    public string PaneId { get; private set; } = "";
    public string Title { get; private set; } = "";
    public Color PaneColor { get; private set; }

    public void Initialize(ViewportWorkspace workspace, string paneId, string title, Color color)
    {
        _workspace = workspace;
        PaneId = paneId;
        Title = title;
        PaneColor = color;
        Name = $"ViewportPane_{paneId.Replace("-", "_")}";

        BuildUi();
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
        if (_workspace != null && _workspace.CanDropPaneData(data, PaneId, out string draggedPaneId))
        {
            _workspace.DropPane(draggedPaneId, PaneId, GetDropZone(atPosition));
        }

        HideDropOverlay();
    }

    internal void HideDropOverlay()
    {
        _dropOverlay?.HideZone();
    }

    internal void BeginDrag()
    {
        _workspace?.BeginPaneDrag(PaneId);
    }

    internal Control CreateDragPreview()
    {
        PanelContainer preview = new() { CustomMinimumSize = new Vector2(160.0f, 32.0f) };
        StyleBoxFlat style =
            new()
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

    private void BuildUi()
    {
        CustomMinimumSize = new Vector2(180.0f, 140.0f);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        MouseExited += HideDropOverlay;

        PanelContainer frame = new() { Name = "Frame", MouseFilter = MouseFilterEnum.Pass };
        FillParent(frame);
        frame.AddThemeStyleboxOverride("panel", CreateFrameStyle());
        AddChild(frame);

        VBoxContainer column = new()
        {
            Name = "Column",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Pass,
        };
        frame.AddChild(column);

        column.AddChild(CreateHeader());
        column.AddChild(CreateViewportContent());

        _dropOverlay = new PaneDropOverlay { Name = "DropOverlay" };
        FillParent(_dropOverlay);
        AddChild(_dropOverlay);
        _dropOverlay.HideZone();
    }

    private Control CreateHeader()
    {
        PanelContainer header = new()
        {
            Name = "Header",
            CustomMinimumSize = new Vector2(0.0f, 28.0f),
            MouseFilter = MouseFilterEnum.Pass,
        };

        StyleBoxFlat headerStyle =
            new()
            {
                BgColor = new Color(0.08f, 0.09f, 0.10f, 1.0f),
                ContentMarginLeft = 6.0f,
                ContentMarginTop = 3.0f,
                ContentMarginRight = 4.0f,
                ContentMarginBottom = 3.0f,
            };
        header.AddThemeStyleboxOverride("panel", headerStyle);

        HBoxContainer row = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Pass,
        };
        header.AddChild(row);

        ViewportPaneDragHandle dragHandle = new();
        dragHandle.Initialize(this);
        row.AddChild(dragHandle);

        Label titleLabel =
            new()
            {
                Text = Title,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
            };
        row.AddChild(titleLabel);

        row.AddChild(CreateSplitMenu());

        Button closeButton =
            new()
            {
                Text = "x",
                TooltipText = "Close viewport",
                CustomMinimumSize = new Vector2(26.0f, 22.0f),
                FocusMode = FocusModeEnum.None,
                Flat = true,
            };
        closeButton.Pressed += () => _workspace?.ClosePane(this);
        row.AddChild(closeButton);

        return header;
    }

    private Control CreateSplitMenu()
    {
        MenuButton splitButton =
            new()
            {
                Text = "+",
                TooltipText = "Split viewport",
                CustomMinimumSize = new Vector2(26.0f, 22.0f),
                FocusMode = FocusModeEnum.None,
                Flat = true,
            };

        PopupMenu popup = splitButton.GetPopup();
        popup.AddItem("Split Left", (int)ViewportDropZone.Left);
        popup.AddItem("Split Right", (int)ViewportDropZone.Right);
        popup.AddItem("Split Top", (int)ViewportDropZone.Top);
        popup.AddItem("Split Bottom", (int)ViewportDropZone.Bottom);
        popup.IdPressed += id => _workspace?.SplitPane(this, (ViewportDropZone)(int)id);

        return splitButton;
    }

    private Control CreateViewportContent()
    {
        SubViewportContainer container =
            new()
            {
                Name = "ViewportContainer",
                Stretch = true,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore,
            };

        SubViewport viewport =
            new()
            {
                Name = "Viewport",
                Size = new Vector2I(640, 480),
                Disable3D = true,
                TransparentBg = false,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            };

        Control root = new() { Name = "ViewportRoot", MouseFilter = MouseFilterEnum.Ignore };
        FillParent(root);

        ColorRect background =
            new()
            {
                Color = PaneColor,
                MouseFilter = MouseFilterEnum.Ignore,
            };
        FillParent(background);
        root.AddChild(background);

        Label label =
            new()
            {
                Text = Title,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
            };
        FillParent(label);
        root.AddChild(label);

        viewport.AddChild(root);
        container.AddChild(viewport);
        return container;
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

    private StyleBoxFlat CreateFrameStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.08f, 0.09f, 1.0f),
            BorderColor = new Color(0.18f, 0.20f, 0.23f, 1.0f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
        };
    }

    private static void FillParent(Control control)
    {
        control.AnchorRight = 1.0f;
        control.AnchorBottom = 1.0f;
        control.OffsetLeft = 0.0f;
        control.OffsetTop = 0.0f;
        control.OffsetRight = 0.0f;
        control.OffsetBottom = 0.0f;
    }
}

public partial class ViewportPaneDragHandle : Button
{
    private ViewportPane _pane;

    public void Initialize(ViewportPane pane)
    {
        _pane = pane;
        Text = "::";
        TooltipText = "Drag viewport";
        CustomMinimumSize = new Vector2(26.0f, 22.0f);
        FocusMode = FocusModeEnum.None;
        Flat = true;
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (_pane == null)
        {
            return default;
        }

        _pane.BeginDrag();
        SetDragPreview(_pane.CreateDragPreview());
        return _pane.PaneId;
    }
}

public partial class PaneDropOverlay : Control
{
    private ViewportDropZone _zone = ViewportDropZone.Right;

    public void ShowZone(ViewportDropZone zone)
    {
        _zone = zone;
        Visible = true;
        QueueRedraw();
    }

    public void HideZone()
    {
        Visible = false;
        QueueRedraw();
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        if (!Visible)
        {
            return;
        }

        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.03f, 0.04f, 0.05f, 0.36f), true);

        Rect2 zoneRect = GetZoneRect();
        DrawRect(zoneRect, new Color(0.20f, 0.48f, 0.76f, 0.42f), true);
        DrawRect(zoneRect, new Color(0.54f, 0.75f, 0.95f, 0.95f), false, 2.0f);
    }

    private Rect2 GetZoneRect()
    {
        Vector2 size = Size;
        float horizontalSpan = size.X * 0.42f;
        float verticalSpan = size.Y * 0.42f;

        return _zone switch
        {
            ViewportDropZone.Left => new Rect2(0.0f, 0.0f, horizontalSpan, size.Y),
            ViewportDropZone.Right
                => new Rect2(size.X - horizontalSpan, 0.0f, horizontalSpan, size.Y),
            ViewportDropZone.Top => new Rect2(0.0f, 0.0f, size.X, verticalSpan),
            ViewportDropZone.Bottom
                => new Rect2(0.0f, size.Y - verticalSpan, size.X, verticalSpan),
            _ => new Rect2(size.X - horizontalSpan, 0.0f, horizontalSpan, size.Y),
        };
    }
}

using System;
using System.Collections.Generic;
using Godot;

public enum ViewportDropZone
{
    Left,
    Right,
    Top,
    Bottom,
}

internal readonly record struct ViewportSplitOption(
    string Label,
    ViewportDropZone Zone,
    TilingContainer.SplitAxis Axis,
    TilingContainer.InsertPlacement Placement
);

public partial class ViewportWorkspace : TilingContainer
{
    internal static readonly ViewportSplitOption[] SplitOptions =
    [
        new("Split Left", ViewportDropZone.Left, SplitAxis.Horizontal, InsertPlacement.Before),
        new("Split Right", ViewportDropZone.Right, SplitAxis.Horizontal, InsertPlacement.After),
        new("Split Top", ViewportDropZone.Top, SplitAxis.Vertical, InsertPlacement.Before),
        new("Split Bottom", ViewportDropZone.Bottom, SplitAxis.Vertical, InsertPlacement.After),
    ];

    private static readonly Color[] PanePalette =
    [
        new Color(0.12f, 0.15f, 0.17f),
        new Color(0.16f, 0.13f, 0.18f),
        new Color(0.13f, 0.17f, 0.14f),
        new Color(0.19f, 0.16f, 0.12f),
        new Color(0.11f, 0.15f, 0.21f),
        new Color(0.18f, 0.12f, 0.14f),
    ];

    private string _activeDragPaneId = "";
    private int _nextPaneNumber = 1;
    private bool _suppressPersistence;
    private bool _isShuttingDown;

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

        RunWithoutPersistence(() =>
        {
            if (!RestoreSavedLayout())
            {
                ApplyPreset(WorkspaceLayoutPreset.Single, false);
            }
        });

        SaveLayout();
        SetupLayoutMenu();
        GetWindow().CloseRequested += OnWindowCloseRequested;
    }

    public override void _ExitTree()
    {
        _isShuttingDown = true;
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
            && FindPane(draggedPaneId) != null
            && FindPane(targetPaneId) != null;
    }

    internal void DropPane(string draggedPaneId, string targetPaneId, ViewportDropZone zone)
    {
        ViewportPane draggedPane = FindPane(draggedPaneId);
        ViewportPane targetPane = FindPane(targetPaneId);

        if (
            draggedPane == null
            || targetPane == null
            || draggedPane == targetPane
            || !TryGetSplitRequest(zone, out SplitAxis axis, out InsertPlacement placement)
        )
        {
            EndPaneDrag();
            return;
        }

        MovePane(draggedPane, targetPane, axis, placement);
        EndPaneDrag();
    }

    internal void SplitPane(ViewportPane targetPane, ViewportDropZone zone)
    {
        if (
            targetPane == null
            || !ContainsPane(targetPane)
            || !TryGetSplitRequest(zone, out SplitAxis axis, out InsertPlacement placement)
        )
        {
            return;
        }

        ViewportPane newPane = CreateNewPane();
        bool inserted = InsertSplit(targetPane, newPane, axis, placement);

        if (!inserted)
        {
            newPane.QueueFree();
            return;
        }
    }

    internal void ClosePane(ViewportPane pane)
    {
        if (pane == null || !ContainsPane(pane) || GetViewportPanes().Count <= 1)
        {
            return;
        }

        RemovePane(pane, true);
    }

    private void OnTilingLayoutChanged()
    {
        if (CanPersistLayout())
        {
            SaveLayout();
        }
    }

    private void ApplyPreset(WorkspaceLayoutPreset preset, bool save = true)
    {
        RunWithoutPersistence(() =>
        {
            List<ViewportPane> oldPanes = GetViewportPanes();

            ViewportPane first = CreateNewPane();
            SetRoot(first);

            switch (preset)
            {
                case WorkspaceLayoutPreset.Single:
                case WorkspaceLayoutPreset.ResetLayout:
                    break;

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
        });

        if (save && CanPersistLayout())
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
            PanePalette[(paneNumber - 1) % PanePalette.Length]
        );
    }

    private ViewportPane CreatePane(string paneId, string title, Color color)
    {
        ViewportPane pane = ViewportPane.Create();
        pane.Initialize(this, paneId, title, color);
        return pane;
    }

    private int GetMinimumNextPaneNumber()
    {
        int minimum = 1;
        foreach (ViewportPane pane in GetViewportPanes())
        {
            string paneId = pane.PaneId;
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

        PopupMenu viewMenu = menuBar.GetNodeOrNull<PopupMenu>("View");
        if (viewMenu == null)
        {
            viewMenu = new PopupMenu { Name = "View" };
            menuBar.AddChild(viewMenu);
        }
        else if (viewMenu.ItemCount > 0)
        {
            viewMenu.AddSeparator();
        }

        viewMenu.AddItem("Single", (int)WorkspaceLayoutPreset.Single);
        viewMenu.AddItem("Two Columns", (int)WorkspaceLayoutPreset.TwoColumns);
        viewMenu.AddItem("Two Rows", (int)WorkspaceLayoutPreset.TwoRows);
        viewMenu.AddItem("Quad", (int)WorkspaceLayoutPreset.Quad);
        viewMenu.AddSeparator();
        viewMenu.AddItem("Reset Layout", (int)WorkspaceLayoutPreset.ResetLayout);
        viewMenu.IdPressed += OnLayoutMenuIdPressed;
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

    private void OnWindowCloseRequested()
    {
        _isShuttingDown = true;
    }

    private void HideDropOverlays()
    {
        foreach (ViewportPane pane in GetViewportPanes())
        {
            if (GodotObject.IsInstanceValid(pane))
            {
                pane.HideDropOverlay();
            }
        }
    }

    private ViewportPane FindPane(string paneId)
    {
        if (string.IsNullOrEmpty(paneId))
        {
            return null;
        }

        foreach (ViewportPane pane in GetViewportPanes())
        {
            if (pane.PaneId == paneId)
            {
                return pane;
            }
        }

        return null;
    }

    private List<ViewportPane> GetViewportPanes()
    {
        List<ViewportPane> panes = [];
        foreach (Control control in GetPanes())
        {
            if (control is ViewportPane pane)
            {
                panes.Add(pane);
            }
        }

        return panes;
    }

    private static bool TryGetSplitRequest(
        ViewportDropZone zone,
        out SplitAxis axis,
        out InsertPlacement placement
    )
    {
        ViewportSplitOption? option = zone switch
        {
            ViewportDropZone.Left
            or ViewportDropZone.Right
            or ViewportDropZone.Top
            or ViewportDropZone.Bottom => SplitOptions[(int)zone],
            _ => null,
        };

        axis = option?.Axis ?? default;
        placement = option?.Placement ?? default;
        return option.HasValue;
    }

    private void RunWithoutPersistence(Action action)
    {
        bool wasSuppressingPersistence = _suppressPersistence;
        _suppressPersistence = true;

        try
        {
            action();
        }
        finally
        {
            _suppressPersistence = wasSuppressingPersistence;
        }
    }

    private bool CanPersistLayout()
    {
        return !_suppressPersistence && !_isShuttingDown && IsInsideTree() && !IsQueuedForDeletion();
    }
}

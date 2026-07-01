using System.Collections.Generic;
using System.Diagnostics;
using Godot;

[GlobalClass]
public partial class TilingContainer : Container
{
    public enum SplitAxis
    {
        Horizontal,
        Vertical,
    }

    public enum InsertPlacement
    {
        Before,
        After,
    }

    public sealed class LayoutSnapshotNode
    {
        public string Type { get; set; } = "";
        public string PaneId { get; set; } = "";
        public string Axis { get; set; } = "";
        public float Ratio { get; set; } = DefaultSplitRatio;
        public LayoutSnapshotNode First { get; set; }
        public LayoutSnapshotNode Second { get; set; }
    }

    [Signal]
    public delegate void LayoutChangedEventHandler();

    [Signal]
    public delegate void SplitDragStartedEventHandler();

    [Signal]
    public delegate void SplitDragEndedEventHandler();

    private const float DefaultSplitRatio = 0.5f;

    // Reusable scratch space for various operations.
    private readonly List<SplitNode> _scratchSplits = [];
    private readonly List<LeafNode> _scratchLeaves = [];

    private LayoutNode _root;
    private SplitNode _draggingSplit;
    private float _dragPointerOffset;
    private bool _suppressChildOrderPrune;

    private float _borderThickness = 1.0f;
    private float _grabThickness = 8.0f;
    private float _minimumPaneSize = 32.0f;
    private Color _borderColor = new(0.18f, 0.18f, 0.18f, 1.0f);

    [Export]
    public float BorderThickness
    {
        get => _borderThickness;
        set
        {
            _borderThickness = Mathf.Max(0.0f, value);
            InvalidateLayout();
        }
    }

    [Export]
    public float GrabThickness
    {
        get => _grabThickness;
        set
        {
            _grabThickness = Mathf.Max(0.0f, value);
            InvalidateLayout();
        }
    }

    [Export]
    public float MinimumPaneSize
    {
        get => _minimumPaneSize;
        set
        {
            _minimumPaneSize = Mathf.Max(0.0f, value);
            InvalidateLayout();
        }
    }

    [Export]
    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            QueueRedraw();
        }
    }

    // Sets the new root pane. Detaches all other panes.
    public bool SetRoot(Control child)
    {
        if (!CanManageChild(child))
        {
            return false;
        }

        DetachAllManagedPanes(false);
        DestroySplitHandles(_root);
        Debug.Assert(TryAttachChild(child)); // Shouldn't be possible to fail here.
        _root = new LeafNode(child);

        InvalidateLayout();
        return true;
    }

    // Insert a new pane as a child of the target pane. Fails if the new child is already
    // a child of this container.
    public bool InsertSplit(
        Control target,
        Control newChild,
        SplitAxis axis,
        InsertPlacement placement = InsertPlacement.After
    )
    {
        if (!CanManageChild(target) || !CanManageChild(newChild) || _root == null)
        {
            return false;
        }

        LeafNode targetLeaf = _root.FindLeaf(target);
        if (targetLeaf == null || ContainsPane(newChild))
        {
            return false;
        }

        if (!TryAttachChild(newChild))
        {
            return false;
        }

        SplitNode targetParent = targetLeaf.Parent;
        LeafNode newLeaf = new(newChild);
        SplitNode split =
            placement == InsertPlacement.After
                ? new SplitNode(axis, targetLeaf, newLeaf)
                : new SplitNode(axis, newLeaf, targetLeaf);

        ReplaceNode(targetLeaf, targetParent, split);

        InvalidateLayout();
        return true;
    }

    public bool InsertSplitRoot(
        Control newChild,
        SplitAxis axis,
        InsertPlacement placement = InsertPlacement.After
    )
    {
        if (!CanManageChild(newChild) || _root == null || ContainsPane(newChild))
        {
            return false;
        }

        if (!TryAttachChild(newChild))
        {
            return false;
        }

        LeafNode newLeaf = new(newChild);
        SplitNode split =
            placement == InsertPlacement.After
                ? new SplitNode(axis, _root, newLeaf)
                : new SplitNode(axis, newLeaf, _root);

        _root = split;
        _root.Parent = null;

        InvalidateLayout();
        return true;
    }

    // Removes a pane, re-adjusting the layout as necessary. Optionally queue free the child.
    public bool RemovePane(Control child, bool queueFreeChild = false)
    {
        if (!CanManageChild(child) || _root == null)
        {
            return false;
        }

        LeafNode leaf = _root.FindLeaf(child);
        if (leaf == null)
        {
            return false;
        }

        CollapseLeaf(leaf);
        DetachManagedChild(child, queueFreeChild);

        InvalidateLayout();
        return true;
    }

    // Removes a pane from its current slot and reinserts it as a sibling of the target under a new split.
    public bool MovePane(
        Control movingPane,
        Control targetPane,
        SplitAxis axis,
        InsertPlacement placement = InsertPlacement.After
    )
    {
        if (
            !CanManageChild(movingPane)
            || !CanManageChild(targetPane)
            || movingPane == targetPane
            || _root == null
        )
        {
            return false;
        }

        LeafNode movingLeaf = _root.FindLeaf(movingPane);
        LeafNode targetLeaf = _root.FindLeaf(targetPane);
        if (movingLeaf == null || targetLeaf == null)
        {
            return false;
        }

        CollapseLeaf(movingLeaf);
        return InsertSplit(targetPane, movingPane, axis, placement);
    }

    public bool ContainsPane(Control child)
    {
        return child != null && _root != null && _root.FindLeaf(child) != null;
    }

    public Godot.Collections.Array<Control> GetPanes()
    {
        Godot.Collections.Array<Control> panes = [];
        _root?.CollectPanes(panes);
        return panes;
    }

    public override Vector2 _GetMinimumSize()
    {
        return _root?.GetMinimumSize(this) ?? new Vector2(MinimumPaneSize, MinimumPaneSize);
    }

    // Overridden to draw the custom border lines for the split nodes.
    public override void _Draw()
    {
        if (_root == null || BorderThickness <= 0.0f || BorderColor.A <= 0.0f)
        {
            return;
        }

        _scratchSplits.Clear();
        _root.CollectSplits(_scratchSplits);

        foreach (SplitNode split in _scratchSplits)
        {
            if (split.BorderRect.Size.X > 0.0f && split.BorderRect.Size.Y > 0.0f)
            {
                DrawRect(split.BorderRect, BorderColor, true);
            }
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationReady)
        {
            AdoptSingleExistingChildIfNeeded();
        }
        else if (what == NotificationChildOrderChanged)
        {
            if (!_suppressChildOrderPrune && PruneDetachedPanes())
            {
                InvalidateLayout();
            }
        }
        else if (what == NotificationSortChildren)
        {
            PruneDetachedPanes();
            SortTilingChildren();
        }
    }

    private void SortTilingChildren()
    {
        if (_root == null)
        {
            HideAllHandles();
            return;
        }

        Rect2 availableRect = new(Vector2.Zero, Size);
        _root.Layout(this, availableRect);

        _scratchSplits.Clear();
        _root.CollectSplits(_scratchSplits);

        foreach (SplitNode split in _scratchSplits)
        {
            EnsureHandle(split);
            FitChildInRect(split.Handle, split.HandleRect);
            split.Handle.Visible = true;
        }

        QueueRedraw();
    }

    private void InvalidateLayout()
    {
        QueueSort();
        QueueRedraw();
        UpdateMinimumSize();
        EmitSignal(SignalName.LayoutChanged);
    }

    private float GetVisibleBorderThickness()
    {
        return Mathf.Max(0.0f, BorderThickness);
    }

    private float GetHandleThickness()
    {
        return Mathf.Max(GetVisibleBorderThickness(), GrabThickness);
    }

    private Vector2 GetPaneMinimumSize(Control child)
    {
        Vector2 childMinimum = child.GetCombinedMinimumSize();
        return new Vector2(
            Mathf.Max(MinimumPaneSize, childMinimum.X),
            Mathf.Max(MinimumPaneSize, childMinimum.Y)
        );
    }

    private float GetPrimaryMinimum(LayoutNode node, SplitAxis axis)
    {
        Vector2 minimumSize = node.GetMinimumSize(this);
        return axis == SplitAxis.Horizontal ? minimumSize.X : minimumSize.Y;
    }

    private float ClampFirstSpan(SplitNode split, float contentSpan, float desiredFirstSpan)
    {
        float firstMinimum = GetPrimaryMinimum(split.First, split.Axis);
        float secondMinimum = GetPrimaryMinimum(split.Second, split.Axis);
        float totalMinimum = firstMinimum + secondMinimum;

        if (contentSpan <= 0.0f)
        {
            return 0.0f;
        }

        if (totalMinimum > contentSpan)
        {
            if (totalMinimum <= 0.0f)
            {
                return contentSpan * DefaultSplitRatio;
            }

            return contentSpan * (firstMinimum / totalMinimum);
        }

        return Mathf.Clamp(desiredFirstSpan, firstMinimum, contentSpan - secondMinimum);
    }

    private Rect2 GetHandleRect(SplitNode split)
    {
        float handleThickness = GetHandleThickness();
        if (handleThickness <= 0.0f)
        {
            return split.BorderRect;
        }

        if (split.Axis == SplitAxis.Horizontal)
        {
            float centerX = split.BorderRect.Position.X + split.BorderRect.Size.X * 0.5f;
            return new Rect2(
                centerX - handleThickness * 0.5f,
                split.Rect.Position.Y,
                handleThickness,
                split.Rect.Size.Y
            );
        }

        float centerY = split.BorderRect.Position.Y + split.BorderRect.Size.Y * 0.5f;
        return new Rect2(
            split.Rect.Position.X,
            centerY - handleThickness * 0.5f,
            split.Rect.Size.X,
            handleThickness
        );
    }
}

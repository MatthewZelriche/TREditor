using System.Collections.Generic;
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

    [Signal]
    public delegate void LayoutChangedEventHandler();

    [Signal]
    public delegate void SplitDragStartedEventHandler();

    [Signal]
    public delegate void SplitDragEndedEventHandler();

    private const float DefaultSplitRatio = 0.5f;

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
            InvalidateLayout(true);
        }
    }

    [Export]
    public float GrabThickness
    {
        get => _grabThickness;
        set
        {
            _grabThickness = Mathf.Max(0.0f, value);
            InvalidateLayout(true);
        }
    }

    [Export]
    public float MinimumPaneSize
    {
        get => _minimumPaneSize;
        set
        {
            _minimumPaneSize = Mathf.Max(0.0f, value);
            InvalidateLayout(true);
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

    public bool SetRoot(Control child)
    {
        if (!CanManageChild(child))
        {
            return false;
        }

        if (!EnsureChildAttached(child))
        {
            return false;
        }

        DetachOldPanesExcept(child);
        DestroySplitHandles(_root);
        _root = new LeafNode(child);

        InvalidateLayout(true);
        return true;
    }

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

        if (!EnsureChildAttached(newChild))
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
        EnsureHandle(split);

        InvalidateLayout(true);
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

        if (!EnsureChildAttached(newChild))
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
        EnsureHandle(split);

        InvalidateLayout(true);
        return true;
    }

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

        InvalidateLayout(true);
        return true;
    }

    public bool ContainsPane(Control child)
    {
        return child != null && _root != null && _root.FindLeaf(child) != null;
    }

    public Rect2 GetPaneRect(Control child)
    {
        LeafNode leaf = child != null && _root != null ? _root.FindLeaf(child) : null;
        return leaf?.Rect ?? new Rect2();
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
                InvalidateLayout(true);
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

    private bool CanManageChild(Control child)
    {
        return child != null
            && child != this
            && GodotObject.IsInstanceValid(child)
            && !IsHandle(child);
    }

    private bool EnsureChildAttached(Control child)
    {
        Node parent = child.GetParent();
        if (parent == this)
        {
            return true;
        }

        if (parent != null)
        {
            return false;
        }

        _suppressChildOrderPrune = true;
        AddChild(child);
        _suppressChildOrderPrune = false;
        return true;
    }

    private void DetachManagedChild(Control child, bool queueFreeChild)
    {
        if (queueFreeChild)
        {
            child.QueueFree();
            return;
        }

        if (child.GetParent() == this)
        {
            _suppressChildOrderPrune = true;
            RemoveChild(child);
            _suppressChildOrderPrune = false;
        }
    }

    private void DetachOldPanesExcept(Control childToKeep)
    {
        if (_root == null)
        {
            return;
        }

        _scratchLeaves.Clear();
        _root.CollectLeaves(_scratchLeaves);

        _suppressChildOrderPrune = true;

        foreach (LeafNode leaf in _scratchLeaves)
        {
            Control child = leaf.Child;
            if (
                child == childToKeep
                || !GodotObject.IsInstanceValid(child)
                || child.GetParent() != this
            )
            {
                continue;
            }

            RemoveChild(child);
        }

        _suppressChildOrderPrune = false;
    }

    private void AdoptSingleExistingChildIfNeeded()
    {
        if (_root != null)
        {
            return;
        }

        Control firstPane = null;
        int paneCount = 0;

        foreach (Node child in GetChildren())
        {
            if (child is not Control control || IsHandle(control))
            {
                continue;
            }

            firstPane = control;
            paneCount++;
            if (paneCount > 1)
            {
                return;
            }
        }

        if (firstPane != null)
        {
            SetRoot(firstPane);
        }
    }

    private void ReplaceNode(LayoutNode oldNode, SplitNode oldParent, LayoutNode newNode)
    {
        newNode.Parent = oldParent;

        if (oldParent == null)
        {
            _root = newNode;
            return;
        }

        if (oldParent.First == oldNode)
        {
            oldParent.First = newNode;
        }
        else
        {
            oldParent.Second = newNode;
        }
    }

    private void CollapseLeaf(LeafNode leaf)
    {
        SplitNode parent = leaf.Parent;
        if (parent == null)
        {
            _root = null;
            return;
        }

        LayoutNode sibling = parent.First == leaf ? parent.Second : parent.First;
        SplitNode grandparent = parent.Parent;

        DestroyHandle(parent);
        sibling.Parent = grandparent;

        if (grandparent == null)
        {
            _root = sibling;
        }
        else if (grandparent.First == parent)
        {
            grandparent.First = sibling;
        }
        else
        {
            grandparent.Second = sibling;
        }
    }

    private bool PruneDetachedPanes()
    {
        if (_root == null)
        {
            return false;
        }

        _scratchLeaves.Clear();
        _root.CollectLeaves(_scratchLeaves);

        bool changed = false;
        foreach (LeafNode leaf in _scratchLeaves)
        {
            Control child = leaf.Child;
            if (!GodotObject.IsInstanceValid(child) || child.GetParent() != this)
            {
                CollapseLeaf(leaf);
                changed = true;
            }
        }

        return changed;
    }

    private void EnsureHandle(SplitNode split)
    {
        if (split.Handle != null && GodotObject.IsInstanceValid(split.Handle))
        {
            return;
        }

        Control handle = new()
        {
            Name = "_TilingSplitHandle",
            MouseFilter = MouseFilterEnum.Stop,
            FocusMode = FocusModeEnum.None,
            ClipContents = false,
        };

        handle.SetMeta("tiling_container_handle", true);
        handle.GuiInput += @event => OnHandleGuiInput(split, @event);

        _suppressChildOrderPrune = true;
        AddChild(handle, false, InternalMode.Back);
        _suppressChildOrderPrune = false;

        split.Handle = handle;
    }

    private void DestroySplitHandles(LayoutNode node)
    {
        if (node == null)
        {
            return;
        }

        if (node is not SplitNode split)
        {
            return;
        }

        DestroySplitHandles(split.First);
        DestroySplitHandles(split.Second);
        DestroyHandle(split);
    }

    private void DestroyHandle(SplitNode split)
    {
        if (split.Handle == null || !GodotObject.IsInstanceValid(split.Handle))
        {
            split.Handle = null;
            return;
        }

        split.Handle.QueueFree();
        split.Handle = null;
    }

    private void HideAllHandles()
    {
        foreach (Node child in GetChildren(true))
        {
            if (child is Control control && IsHandle(control))
            {
                control.Visible = false;
            }
        }
    }

    private bool IsHandle(Control control)
    {
        return control.HasMeta("tiling_container_handle");
    }

    private void OnHandleGuiInput(SplitNode split, InputEvent @event)
    {
        if (
            @event is InputEventMouseButton mouseButton
            && mouseButton.ButtonIndex == MouseButton.Left
        )
        {
            if (mouseButton.Pressed)
            {
                StartDragging(split);
            }
            else if (_draggingSplit == split)
            {
                StopDragging();
            }

            split.Handle?.AcceptEvent();
        }
        else if (@event is InputEventMouseMotion && _draggingSplit == split)
        {
            UpdateDraggedSplit(split);
            split.Handle?.AcceptEvent();
        }
    }

    private void StartDragging(SplitNode split)
    {
        _draggingSplit = split;
        Vector2 mousePosition = GetLocalMousePosition();

        _dragPointerOffset =
            split.Axis == SplitAxis.Horizontal
                ? mousePosition.X - split.BorderRect.Position.X
                : mousePosition.Y - split.BorderRect.Position.Y;

        EmitSignal(SignalName.SplitDragStarted);
    }

    private void StopDragging()
    {
        _draggingSplit = null;
        EmitSignal(SignalName.SplitDragEnded);
    }

    private void UpdateDraggedSplit(SplitNode split)
    {
        if (split.Rect.Size.X <= 0.0f || split.Rect.Size.Y <= 0.0f)
        {
            return;
        }

        Vector2 mousePosition = GetLocalMousePosition();
        float borderThickness = GetVisibleBorderThickness();
        float contentSpan;
        float desiredFirstSpan;

        if (split.Axis == SplitAxis.Horizontal)
        {
            contentSpan = Mathf.Max(0.0f, split.Rect.Size.X - borderThickness);
            desiredFirstSpan = mousePosition.X - split.Rect.Position.X - _dragPointerOffset;
        }
        else
        {
            contentSpan = Mathf.Max(0.0f, split.Rect.Size.Y - borderThickness);
            desiredFirstSpan = mousePosition.Y - split.Rect.Position.Y - _dragPointerOffset;
        }

        if (contentSpan <= 0.0f)
        {
            return;
        }

        float clampedFirstSpan = ClampFirstSpan(split, contentSpan, desiredFirstSpan);
        split.Ratio = Mathf.Clamp(clampedFirstSpan / contentSpan, 0.0f, 1.0f);

        InvalidateLayout(true);
    }

    private void InvalidateLayout(bool emitLayoutChanged)
    {
        QueueSort();
        QueueRedraw();
        UpdateMinimumSize();

        if (emitLayoutChanged)
        {
            EmitSignal(SignalName.LayoutChanged);
        }
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

    private abstract class LayoutNode
    {
        public SplitNode Parent;
        public Rect2 Rect;

        public abstract Vector2 GetMinimumSize(TilingContainer owner);
        public abstract LeafNode FindLeaf(Control child);
        public abstract void Layout(TilingContainer owner, Rect2 rect);
        public abstract void CollectPanes(Godot.Collections.Array<Control> panes);
        public abstract void CollectLeaves(List<LeafNode> leaves);
        public abstract void CollectSplits(List<SplitNode> splits);
    }

    private sealed class LeafNode(Control child) : LayoutNode
    {
        public readonly Control Child = child;

        public override Vector2 GetMinimumSize(TilingContainer owner)
        {
            if (!GodotObject.IsInstanceValid(Child))
            {
                return new Vector2(owner.MinimumPaneSize, owner.MinimumPaneSize);
            }

            return owner.GetPaneMinimumSize(Child);
        }

        public override LeafNode FindLeaf(Control child)
        {
            return Child == child ? this : null;
        }

        public override void Layout(TilingContainer owner, Rect2 rect)
        {
            Rect = rect;

            if (GodotObject.IsInstanceValid(Child) && Child.GetParent() == owner)
            {
                owner.FitChildInRect(Child, rect);
            }
        }

        public override void CollectPanes(Godot.Collections.Array<Control> panes)
        {
            if (GodotObject.IsInstanceValid(Child))
            {
                panes.Add(Child);
            }
        }

        public override void CollectLeaves(List<LeafNode> leaves)
        {
            leaves.Add(this);
        }

        public override void CollectSplits(List<SplitNode> splits) { }
    }

    private sealed class SplitNode : LayoutNode
    {
        public readonly SplitAxis Axis;
        public float Ratio = DefaultSplitRatio;
        public LayoutNode First;
        public LayoutNode Second;
        public Control Handle;
        public Rect2 BorderRect;
        public Rect2 HandleRect;

        public SplitNode(SplitAxis axis, LayoutNode first, LayoutNode second)
        {
            Axis = axis;
            First = first;
            Second = second;
            First.Parent = this;
            Second.Parent = this;
        }

        public override Vector2 GetMinimumSize(TilingContainer owner)
        {
            Vector2 firstMinimum = First.GetMinimumSize(owner);
            Vector2 secondMinimum = Second.GetMinimumSize(owner);
            float borderThickness = owner.GetVisibleBorderThickness();

            if (Axis == SplitAxis.Horizontal)
            {
                return new Vector2(
                    firstMinimum.X + secondMinimum.X + borderThickness,
                    Mathf.Max(firstMinimum.Y, secondMinimum.Y)
                );
            }

            return new Vector2(
                Mathf.Max(firstMinimum.X, secondMinimum.X),
                firstMinimum.Y + secondMinimum.Y + borderThickness
            );
        }

        public override LeafNode FindLeaf(Control child)
        {
            return First.FindLeaf(child) ?? Second.FindLeaf(child);
        }

        public override void Layout(TilingContainer owner, Rect2 rect)
        {
            Rect = rect;

            float borderThickness = owner.GetVisibleBorderThickness();

            if (Axis == SplitAxis.Horizontal)
            {
                float contentSpan = Mathf.Max(0.0f, rect.Size.X - borderThickness);
                float firstSpan = owner.ClampFirstSpan(this, contentSpan, contentSpan * Ratio);
                float secondSpan = Mathf.Max(0.0f, contentSpan - firstSpan);

                Rect2 firstRect = new(rect.Position, new Vector2(firstSpan, rect.Size.Y));
                BorderRect = new Rect2(
                    rect.Position.X + firstSpan,
                    rect.Position.Y,
                    borderThickness,
                    rect.Size.Y
                );
                Rect2 secondRect = new(
                    rect.Position.X + firstSpan + borderThickness,
                    rect.Position.Y,
                    secondSpan,
                    rect.Size.Y
                );

                First.Layout(owner, firstRect);
                Second.Layout(owner, secondRect);
            }
            else
            {
                float contentSpan = Mathf.Max(0.0f, rect.Size.Y - borderThickness);
                float firstSpan = owner.ClampFirstSpan(this, contentSpan, contentSpan * Ratio);
                float secondSpan = Mathf.Max(0.0f, contentSpan - firstSpan);

                Rect2 firstRect = new(rect.Position, new Vector2(rect.Size.X, firstSpan));
                BorderRect = new Rect2(
                    rect.Position.X,
                    rect.Position.Y + firstSpan,
                    rect.Size.X,
                    borderThickness
                );
                Rect2 secondRect = new(
                    rect.Position.X,
                    rect.Position.Y + firstSpan + borderThickness,
                    rect.Size.X,
                    secondSpan
                );

                First.Layout(owner, firstRect);
                Second.Layout(owner, secondRect);
            }

            HandleRect = owner.GetHandleRect(this);
        }

        public override void CollectPanes(Godot.Collections.Array<Control> panes)
        {
            First.CollectPanes(panes);
            Second.CollectPanes(panes);
        }

        public override void CollectLeaves(List<LeafNode> leaves)
        {
            First.CollectLeaves(leaves);
            Second.CollectLeaves(leaves);
        }

        public override void CollectSplits(List<SplitNode> splits)
        {
            splits.Add(this);
            First.CollectSplits(splits);
            Second.CollectSplits(splits);
        }
    }
}

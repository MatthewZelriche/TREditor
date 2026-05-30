using Godot;

public partial class TilingContainer
{
    // Makes sure a split node has a valid corresponding handle control for mouse interaction.
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

        RunWithChildOrderPruneSuppressed(() => AddChild(handle, false, InternalMode.Back));
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

        InvalidateLayout();
    }
}

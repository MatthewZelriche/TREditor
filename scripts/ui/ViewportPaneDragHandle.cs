using Godot;

public partial class ViewportPaneDragHandle : Button
{
    private ViewportPane _pane;

    public void Initialize(ViewportPane pane)
    {
        _pane = pane;
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

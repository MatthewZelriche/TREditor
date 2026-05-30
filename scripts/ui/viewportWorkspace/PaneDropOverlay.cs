using Godot;

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
            ViewportDropZone.Right => new Rect2(
                size.X - horizontalSpan,
                0.0f,
                horizontalSpan,
                size.Y
            ),
            ViewportDropZone.Top => new Rect2(0.0f, 0.0f, size.X, verticalSpan),
            ViewportDropZone.Bottom => new Rect2(0.0f, size.Y - verticalSpan, size.X, verticalSpan),
            _ => new Rect2(size.X - horizontalSpan, 0.0f, horizontalSpan, size.Y),
        };
    }
}

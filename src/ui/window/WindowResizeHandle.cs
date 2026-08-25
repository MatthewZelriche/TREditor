#nullable enable

using Godot;

public partial class WindowResizeHandle : Control
{
    [Export]
    public DisplayServer.WindowResizeEdge ResizeEdge { get; set; }

    public override void _Ready()
    {
        MouseFilter = DisplayServer.HasFeature(DisplayServer.Feature.WindowDrag)
            && !GetWindow().Unresizable
            ? MouseFilterEnum.Stop
            : MouseFilterEnum.Ignore;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (
            @event is not InputEventMouseButton mouseButton
            || mouseButton.ButtonIndex != MouseButton.Left
            || !mouseButton.Pressed
            || !DisplayServer.HasFeature(DisplayServer.Feature.WindowDrag)
            || GetWindow().Unresizable
        )
        {
            return;
        }

        Window window = GetWindow();
        DisplayServer.WindowStartResize(ResizeEdge, window.GetWindowId());
        AcceptEvent();
    }
}

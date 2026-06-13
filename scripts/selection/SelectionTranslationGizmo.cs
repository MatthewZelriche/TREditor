using System;
using Gizmo3DPlugin;
using Godot;

public partial class SelectionTranslationGizmo : Gizmo3D
{
    public Func<float> GetGridSnapSize { get; set; } = () => GridSnap.Off;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Keycode: Key.Ctrl or Key.Shift })
        {
            return;
        }

        base._UnhandledInput(@event);
    }

    public bool ShouldCapturePointerInput(InputEvent @event)
    {
        if (!Visible)
        {
            return false;
        }

        if (Editing)
        {
            return IsGizmoPointerEvent(@event);
        }

        // GUI input is published to editor tools before Gizmo3D receives its unhandled-input
        // callback. Refresh hover on every left press so clicks use the current handle position,
        // even when the gizmo or cursor moved without a preceding motion event.
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press)
        {
            base._UnhandledInput(new InputEventMouseMotion { Position = press.Position });
            return Hovering;
        }

        return Hovering && IsGizmoPointerEvent(@event);
    }

    protected override Vector3 EditTranslate(Vector3 translation)
    {
        return GridSnap.Snap(translation, GetGridSnapSize());
    }

    private static bool IsGizmoPointerEvent(InputEvent @event) =>
        @event is InputEventMouseMotion
        || @event is InputEventMouseButton { ButtonIndex: MouseButton.Left };
}

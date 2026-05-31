using System;
using Godot;

public readonly record struct ViewportInputModifiers(
    bool ShiftPressed,
    bool CtrlPressed,
    bool AltPressed,
    bool MetaPressed
);

public readonly record struct ViewportMouseButtonEvent(
    string ViewportPaneId,
    Vector2 ViewportPosition,
    Vector2 ViewportSize,
    MouseButton Button,
    bool Pressed,
    bool DoubleClick,
    ViewportInputModifiers Modifiers,
    Vector3 RayOrigin,
    Vector3 RayDirection
);

public readonly record struct ViewportMouseMotionEvent(
    string ViewportPaneId,
    Vector2 ViewportPosition,
    Vector2 Relative,
    Vector2 ViewportSize,
    ViewportInputModifiers Modifiers,
    Vector3 RayOrigin,
    Vector3 RayDirection
);

// Autoload singleton EventBus for viewport input events.
public partial class ViewportInputEvents : Node
{
    public static ViewportInputEvents Instance { get; private set; }

    public event Action<ViewportMouseButtonEvent> ViewportMouseButton;
    public event Action<ViewportMouseMotionEvent> ViewportMouseMotion;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    internal void Publish(ViewportMouseButtonEvent input)
    {
        ViewportMouseButton?.Invoke(input);
    }

    internal void Publish(ViewportMouseMotionEvent input)
    {
        ViewportMouseMotion?.Invoke(input);
    }
}

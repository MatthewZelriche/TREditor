using Godot;

public partial class ViewCamera : Camera3D
{
    [Export]
    private float horzSens = 0.3f;

    [Export]
    private float vertSens = 0.3f;

    [Export]
    private float moveSpeed = 8.0f;

    private float pitch = 0.0f;
    private float yaw = 0.0f;
    private bool isGrabbed;

    public float HorizontalSensitivity
    {
        get => horzSens;
        set => horzSens = value;
    }

    public float VerticalSensitivity
    {
        get => vertSens;
        set => vertSens = value;
    }

    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = value;
    }

    public bool IsControllingInput => isGrabbed;

    public override void _Ready()
    {
        var euler = Rotation;
        pitch = Mathf.RadToDeg(euler.X);
        yaw = Mathf.RadToDeg(euler.Y);
    }

    // TODO: Possibly backwards, need to check.
    public Vector3 GetForwardVector()
    {
        return GlobalTransform.Basis.Z;
    }

    public Vector3 GetRightVector()
    {
        return GlobalTransform.Basis.X;
    }

    public override void _Process(double delta)
    {
        if (!isGrabbed)
        {
            return;
        }

        Vector3 move = Vector3.Zero;

        if (Input.IsActionPressed(KeybindingActions.CameraForward, exactMatch: true))
        {
            move -= GetForwardVector();
        }

        if (Input.IsActionPressed(KeybindingActions.CameraBack, exactMatch: true))
        {
            move += GetForwardVector();
        }

        if (Input.IsActionPressed(KeybindingActions.CameraLeft, exactMatch: true))
        {
            move -= GetRightVector();
        }

        if (Input.IsActionPressed(KeybindingActions.CameraRight, exactMatch: true))
        {
            move += GetRightVector();
        }

        if (!move.IsZeroApprox())
        {
            GlobalPosition += move.Normalized() * moveSpeed * (float)delta;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motionEvent)
        {
            if (!isGrabbed)
            {
                return;
            }

            pitch -= motionEvent.Relative.Y * vertSens;
            yaw -= motionEvent.Relative.X * horzSens;

            // Clamp camera to avoid flipping
            pitch = Mathf.Clamp(pitch, -89.0f, 89.0f);

            // Intentionally zero out roll.
            Rotation = new Vector3(Mathf.DegToRad(pitch), Mathf.DegToRad(yaw), 0);
        }
        else if (KeybindingService.IsActionPressed(@event, KeybindingActions.CameraLook))
        {
            isGrabbed = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
            GetViewport().SetInputAsHandled();
        }
        else if (
            isGrabbed && KeybindingService.IsActionReleased(@event, KeybindingActions.CameraLook)
        )
        {
            isGrabbed = false;
            Input.MouseMode = Input.MouseModeEnum.Visible;
            GetViewport().SetInputAsHandled();
        }
    }
}

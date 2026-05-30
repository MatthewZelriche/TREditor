using Godot;

public partial class ViewCamera : Camera3D
{
    [Export]
    private float horzSens = 0.4f;

    [Export]
    private float vertSens = 0.4f;

    private float pitch = 0.0f;
    private float yaw = 0.0f;

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

    public Vector3 GetHorzForwardVector()
    {
        return new Vector3(Mathf.Sin(Mathf.DegToRad(yaw)), 0.0f, Mathf.Cos(Mathf.DegToRad(yaw)));
    }

    public Vector3 GetRightVector()
    {
        return GlobalTransform.Basis.X;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motionEvent)
        {
            pitch -= motionEvent.Relative.Y * vertSens;
            yaw -= motionEvent.Relative.X * horzSens;

            // Clamp camera to avoid flipping
            pitch = Mathf.Clamp(pitch, -89.0f, 89.0f);

            // Intentionally zero out roll.
            Rotation = new Vector3(Mathf.DegToRad(pitch), Mathf.DegToRad(yaw), 0);
        }
        // TODO: Decouple so that the button or key press can be selected in keybinds.
        else if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
            else if (mouseButton.ButtonIndex == MouseButton.Right && !mouseButton.Pressed)
            {
                Input.MouseMode = Input.MouseModeEnum.Visible;
            }
        }
    }
}

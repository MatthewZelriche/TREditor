#nullable enable

using System;
using Godot;

public partial class MainViewport : SubViewportContainer
{
    private const float MaximumPitch = 89.0f * Mathf.Pi / 180.0f;

    private static readonly Vector3 InitialCameraPosition = new(10.0f, 10.0f, 10.0f);
    private static readonly Vector3 InitialCameraTarget = Vector3.Zero;

    private readonly Camera3D _camera;
    private float _pitch;
    private float _yaw;
    private bool _isNavigating;
    private Input.MouseModeEnum _previousMouseMode;

    public MainViewport()
    {
        Name = "Perspective";
        Stretch = true;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        SubViewport renderViewport = new()
        {
            Name = "RenderViewport",
            RenderTargetUpdateMode = SubViewport.UpdateMode.WhenParentVisible,
        };
        AddChild(renderViewport);

        _camera = new Camera3D
        {
            Name = "Camera",
            Projection = Camera3D.ProjectionType.Perspective,
            Current = true,
        };
        _camera.LookAtFromPosition(InitialCameraPosition, InitialCameraTarget, Vector3.Up);
        _pitch = _camera.Rotation.X;
        _yaw = _camera.Rotation.Y;
        renderViewport.AddChild(_camera);
    }

    public event Action<MainViewport>? ActivationRequested;

    public float LookSensitivity { get; set; } = 0.0025f;

    public float MovementSpeed { get; set; } = 10.0f;

    internal Camera3D Camera => _camera;

    internal bool IsNavigating => _isNavigating;

    public override void _GuiInput(InputEvent @event) => HandleGuiInput(@event);

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (
                mouseButton.ButtonIndex == MouseButton.Right
                && !mouseButton.Pressed
                && _isNavigating
            )
            {
                EndNavigation();
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (
            _isNavigating
            && @event is InputEventMouseMotion mouseMotion
        )
        {
            ApplyLook(-mouseMotion.Relative * LookSensitivity);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (!_isNavigating)
            return;

        Move(
            new Vector2(
                GetAxis(Key.A, Key.D),
                GetAxis(Key.S, Key.W)
            ),
            delta
        );
    }

    public override void _ExitTree() => EndNavigation();

    public override void _Notification(int what)
    {
        if (what == NotificationWMWindowFocusOut)
            EndNavigation();
    }

    internal void HandleGuiInput(InputEvent @event)
    {
        if (
            @event is not InputEventMouseButton mouseButton
            || !mouseButton.Pressed
            || (
                mouseButton.ButtonIndex != MouseButton.Left
                && mouseButton.ButtonIndex != MouseButton.Right
            )
        )
        {
            return;
        }

        ActivationRequested?.Invoke(this);
        if (mouseButton.ButtonIndex == MouseButton.Left)
            return;

        BeginNavigation();
        AcceptEvent();
    }

    internal void Move(Vector2 movementInput, double delta)
    {
        if (!_isNavigating || movementInput.IsZeroApprox())
            return;

        var localDirection = new Vector3(
            movementInput.X,
            0.0f,
            -movementInput.Y
        ).Normalized();
        Vector3 worldDirection = _camera.GlobalBasis * localDirection;
        _camera.GlobalPosition += worldDirection * MovementSpeed * (float)delta;
    }

    internal void StopNavigation() => EndNavigation();

    private static float GetAxis(Key negative, Key positive)
    {
        float value = 0.0f;
        if (Input.IsPhysicalKeyPressed(negative))
            value -= 1.0f;
        if (Input.IsPhysicalKeyPressed(positive))
            value += 1.0f;
        return value;
    }

    private void BeginNavigation()
    {
        if (_isNavigating)
            return;

        _isNavigating = true;
        _previousMouseMode = Input.MouseMode;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void EndNavigation()
    {
        if (!_isNavigating)
            return;

        _isNavigating = false;
        Input.MouseMode = _previousMouseMode;
    }

    private void ApplyLook(Vector2 lookDelta)
    {
        _yaw += lookDelta.X;
        _pitch = Mathf.Clamp(
            _pitch + lookDelta.Y,
            -MaximumPitch,
            MaximumPitch
        );
        _camera.Rotation = new Vector3(_pitch, _yaw, 0.0f);
    }
}

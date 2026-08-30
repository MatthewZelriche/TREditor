#nullable enable

using System;
using Godot;

public partial class MainViewport : SubViewportContainer
{
    private const float MaximumPitch = 89.0f * Mathf.Pi / 180.0f;

    private static readonly Vector3 InitialCameraPosition = new(10.0f, 10.0f, 10.0f);
    private static readonly Vector3 InitialCameraTarget = Vector3.Zero;

    private readonly Camera3D _camera;
    private readonly Node3D _toolOverlayRoot;
    private float _pitch;
    private float _yaw;
    private bool _isNavigating;
    private bool _isToolDragging;
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

        // A generic home for 3D overlays that belong to this pane. The viewport neither creates nor
        // knows about the specific overlays a tool registers here.
        _toolOverlayRoot = new Node3D { Name = "ToolOverlay" };
        renderViewport.AddChild(_toolOverlayRoot);
    }

    public event Action<MainViewport>? ActivationRequested;

    // The left-button interaction phases a tool consumes. They are raised as plain viewport events;
    // the viewport does not know which tool, if any, is listening.
    public event Action<EditorMouseInput>? ToolMouseDown;

    public event Action<EditorMouseInput>? ToolMouseDrag;

    public event Action<EditorMouseInput>? ToolMouseUp;

    // Raised when a drag is taken away from the tool rather than completed, so its owner can cancel
    // the interaction. Losing window focus, camera navigation, and pane removal all end this way.
    public event Action? ToolInteractionCancelled;

    public float LookSensitivity { get; set; } = 0.0025f;

    public float MovementSpeed { get; set; } = 10.0f;

    internal Camera3D Camera => _camera;

    internal Node3D ToolOverlayRoot => _toolOverlayRoot;

    internal bool IsNavigating => _isNavigating;

    internal bool IsToolDragging => _isToolDragging;

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
            else if (
                mouseButton.ButtonIndex == MouseButton.Left
                && !mouseButton.Pressed
                && _isToolDragging
            )
            {
                // Release is handled here rather than in GUI input so a drag that ends outside the
                // viewport still completes the interaction.
                EndToolDrag(mouseButton);
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (@event is not InputEventMouseMotion mouseMotion)
            return;

        if (_isNavigating)
        {
            ApplyLook(-mouseMotion.Relative * LookSensitivity);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_isToolDragging)
        {
            ToolMouseDrag?.Invoke(CreateToolInput(mouseMotion));
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (!_isNavigating)
            return;

        Move(
            new Vector2(
                Input.GetAxis(
                    EditorInputActions.CameraMoveLeft,
                    EditorInputActions.CameraMoveRight
                ),
                Input.GetAxis(
                    EditorInputActions.CameraMoveBackward,
                    EditorInputActions.CameraMoveForward
                )
            ),
            delta
        );
    }

    public override void _ExitTree()
    {
        EndNavigation();
        StopToolDrag();
    }

    public override void _Notification(int what)
    {
        if (what != NotificationWMWindowFocusOut)
            return;

        EndNavigation();
        StopToolDrag();
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
        {
            BeginToolDrag(mouseButton);
            return;
        }

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

    // Drops an in-progress drag without raising mouse up. The active tool is cancelled by whoever
    // owns it; the viewport only forgets that it was dragging.
    internal void StopToolDrag()
    {
        if (!_isToolDragging)
            return;

        _isToolDragging = false;
        ToolInteractionCancelled?.Invoke();
    }

    private void BeginToolDrag(InputEventMouseButton mouseButton)
    {
        if (_isNavigating)
            return;

        _isToolDragging = true;
        ToolMouseDown?.Invoke(CreateToolInput(mouseButton, mouseButton.Position));
    }

    private void EndToolDrag(InputEventMouseButton mouseButton)
    {
        _isToolDragging = false;
        ToolMouseUp?.Invoke(CreateToolInput(mouseButton));
    }

    // Events delivered through _Input carry window coordinates, while a tool projects through this
    // viewport's camera and needs positions local to the control.
    private EditorMouseInput CreateToolInput(InputEventMouse mouseEvent) =>
        CreateToolInput(mouseEvent, mouseEvent.Position - GlobalPosition);

    private EditorMouseInput CreateToolInput(
        InputEventWithModifiers modifiers,
        Vector2 localPosition
    ) =>
        new(
            _camera,
            localPosition,
            modifiers.ShiftPressed,
            modifiers.CtrlPressed,
            modifiers.AltPressed
        );

    private void BeginNavigation()
    {
        if (_isNavigating)
            return;

        // Camera navigation takes the mouse over, so an unfinished drag can never be completed and
        // the viewport stops tracking it. Cancelling the tool itself belongs to the tool's owner.
        StopToolDrag();
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

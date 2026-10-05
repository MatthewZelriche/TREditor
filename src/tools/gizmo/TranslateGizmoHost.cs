using System;
using Gizmo3DPlugin;
using Godot;

// One viewport's transform gizmo. The host adapts the pinned Gizmo3D node to editor ownership: the tool
// registry decides when input reaches a gizmo, so automatic unhandled-input processing is off and the
// coordinator drives the three interaction phases explicitly. Everything specific to this editor lives
// here rather than in the vendored node.
public partial class TranslateGizmoHost : Gizmo3D
{
    private readonly SnapSettings _snap;
    private uint _layerMask = 1;
    private Vector2 _lastPosition;

    public TranslateGizmoHost(SnapSettings snap)
    {
        ArgumentNullException.ThrowIfNull(snap);
        _snap = snap;
        Name = "TransformGizmo";
        SetMode(TransformOperation.Translate);
        // Selection feedback and infinite axis lines belong to the editor's own selection rendering,
        // which this slice does not have yet.
        ShowSelectionBox = false;
        ShowAxes = false;
        ShowTransformPlane = true;
    }

    // Whether the user is mid-drag on this host's gizmo.
    internal bool IsInteracting => Editing;

    internal void SetMode(TransformOperation operation)
    {
        Mode = operation switch
        {
            TransformOperation.Translate => ToolMode.Move,
            TransformOperation.Rotate => ToolMode.Rotate,
            TransformOperation.Scale => ToolMode.Scale,
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
    }

    // Assigned before the host enters the tree. The base setter only reaches the render instances once
    // the node is ready, so the value is applied again from _Ready.
    internal void SetRenderLayer(uint layerMask)
    {
        _layerMask = layerMask;
        Layers = layerMask;
    }

    public override void _Ready()
    {
        base._Ready();
        Layers = _layerMask;
        SetProcessUnhandledInput(false);
    }

    // Shows the gizmo for the shared proxy. Gizmo3D derives its own visibility from what it has
    // selected, so selecting and clearing is also how the host is shown and hidden.
    internal void Attach(Node3D proxy)
    {
        if (!IsSelected(proxy))
            Select(proxy);
    }

    internal void Detach()
    {
        ClearSelection();
        Visible = false;
    }

    // Returns true when the press landed on a gizmo handle and this host now owns the drag.
    internal bool BeginInteraction(Vector2 screenPosition)
    {
        _lastPosition = screenPosition;
        using var press = new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true,
            Position = screenPosition,
        };
        _UnhandledInput(press);
        return Editing;
    }

    internal void UpdateInteraction(Vector2 screenPosition)
    {
        if (!Editing)
            return;

        _lastPosition = screenPosition;
        using var motion = new InputEventMouseMotion
        {
            Position = screenPosition,
            ButtonMask = MouseButtonMask.Left,
        };
        _UnhandledInput(motion);
    }

    internal void EndInteraction()
    {
        if (!Editing)
            return;

        using var release = new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = false,
            Position = _lastPosition,
        };
        _UnhandledInput(release);
    }

    // Gizmo3D's built-in Ctrl snapping never reaches us because the coordinator feeds mouse events
    // only. Honor the shared bottom-bar grid increment on translation and scale deltas instead.
    protected override Vector3 EditTranslate(Vector3 translation) => SnapGrid(translation);

    protected override Vector3 EditScale(Vector3 scale) => SnapGrid(scale);

    private Vector3 SnapGrid(Vector3 value) =>
        _snap.GridSnap is float step && step > 0.0f ? value.Snapped(step) : value;

    // Angle snap is an editor setting, not Gizmo3D's transient Ctrl modifier. Quantize the rotation
    // vector's magnitude while preserving its signed axis direction.
    protected override Vector3 EditRotate(Vector3 rotation)
    {
        if (
            _snap.AngleSnap is not float step
            || step <= 0.0f
            || rotation.IsZeroApprox()
        )
        {
            return rotation;
        }

        float snappedRadians = Mathf.DegToRad(
            Mathf.Snapped(Mathf.RadToDeg(rotation.Length()), step)
        );
        return rotation.Normalized() * snappedRadians;
    }
}

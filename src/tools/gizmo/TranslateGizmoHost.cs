using Gizmo3DPlugin;
using Godot;

// One viewport's move gizmo. The host adapts the pinned Gizmo3D node to editor ownership: the tool
// registry decides when input reaches a gizmo, so automatic unhandled-input processing is off and the
// coordinator drives the three interaction phases explicitly. Everything specific to this editor lives
// here rather than in the vendored node.
public partial class TranslateGizmoHost : Gizmo3D
{
    private uint _layerMask = 1;
    private Vector2 _lastPosition;

    public TranslateGizmoHost()
    {
        Name = "TranslateGizmo";
        Mode = ToolMode.Move;
        // Selection feedback and infinite axis lines belong to the editor's own selection rendering,
        // which this slice does not have yet.
        ShowSelectionBox = false;
        ShowAxes = false;
        ShowTransformPlane = true;
    }

    // Whether the user is mid-drag on this host's gizmo.
    internal bool IsInteracting => Editing;

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
}

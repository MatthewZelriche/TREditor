using Godot;

// One viewport mouse sample handed to the active tool. The camera travels with the sample because a
// tool has to project the cursor through the viewport the event came from, and screen position is
// local to that viewport's control.
public readonly record struct EditorMouseInput(
    Camera3D Camera,
    Vector2 ScreenPosition,
    bool IsShiftPressed = false,
    bool IsCtrlPressed = false,
    bool IsAltPressed = false,
    bool IsDoubleClick = false
);

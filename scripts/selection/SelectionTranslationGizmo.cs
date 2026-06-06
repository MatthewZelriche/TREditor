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

    protected override Vector3 EditTranslate(Vector3 translation)
    {
        return GridSnap.Snap(translation, GetGridSnapSize());
    }
}

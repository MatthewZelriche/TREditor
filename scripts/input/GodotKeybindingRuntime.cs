#nullable enable

using Godot;

public sealed class GodotKeybindingRuntime : IKeybindingRuntime
{
    public void Apply(string actionId, InputBinding? binding)
    {
        StringName action = actionId;
        if (!InputMap.HasAction(action))
            InputMap.AddAction(action);

        InputMap.ActionEraseEvents(action);
        if (binding != null)
        {
            foreach (InputEvent input in binding.ToInputEvents())
                InputMap.ActionAddEvent(action, input);
        }
    }
}

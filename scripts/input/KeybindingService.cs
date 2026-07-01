#nullable enable

using System;
using Godot;

public partial class KeybindingService : Node
{
    private KeybindingManager? _manager;

    public static KeybindingService? Instance { get; private set; }

    public event Action<string>? BindingChanged;

    public override void _EnterTree()
    {
        Instance = this;
        _manager = new KeybindingManager(
            new GodotKeybindingStore(),
            new GodotKeybindingRuntime(),
            GD.PushWarning
        );
        _manager.BindingChanged += OnBindingChanged;
    }

    public override void _ExitTree()
    {
        if (_manager != null)
            _manager.BindingChanged -= OnBindingChanged;
        _manager = null;
        if (Instance == this)
            Instance = null;
    }

    public InputBinding? GetBinding(string actionId) => GetManager().GetBinding(actionId);

    public string GetBindingDisplayText(string actionId) =>
        GetBinding(actionId)?.GetDisplayText() ?? "Unbound";

    public string? FindConflict(string actionId, InputBinding? binding) =>
        GetManager().FindConflict(actionId, binding);

    public KeybindingChangeResult SetBinding(
        string actionId,
        InputBinding? binding,
        bool replaceConflict = false
    ) => GetManager().SetBinding(actionId, binding, replaceConflict);

    public KeybindingChangeResult ResetBinding(string actionId, bool replaceConflict = false) =>
        GetManager().ResetBinding(actionId, replaceConflict);

    public KeybindingChangeResult ResetAll() => GetManager().ResetAll();

    public static bool IsActionPressed(InputEvent input, string actionId) =>
        input.IsPressed()
        && !input.IsEcho()
        && InputMap.EventIsAction(input, actionId, exactMatch: true);

    public static bool IsActionReleased(InputEvent input, string actionId) =>
        input.IsReleased() && InputMap.EventIsAction(input, actionId, exactMatch: true);

    private KeybindingManager GetManager() =>
        _manager ?? throw new InvalidOperationException("KeybindingService is not initialized.");

    private void OnBindingChanged(string actionId) => BindingChanged?.Invoke(actionId);
}

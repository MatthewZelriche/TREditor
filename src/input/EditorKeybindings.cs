#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Godot;

public sealed record KeybindingDefinition(
    string DisplayName,
    Key DefaultPhysicalKey
);

public readonly record struct KeybindingAssignmentResult(
    bool Applied,
    string? ConflictingActionName
);

public static class EditorInputActions
{
    public const string CameraMoveForward = "camera_move_forward";
    public const string CameraMoveBackward = "camera_move_backward";
    public const string CameraMoveLeft = "camera_move_left";
    public const string CameraMoveRight = "camera_move_right";
    public const string ToolTranslate = "tool_translate";
    public const string ToolRotate = "tool_rotate";
    public const string ToolScale = "tool_scale";
    public const string EditDelete = "edit_delete";

    public static readonly IReadOnlyDictionary<string, KeybindingDefinition> Definitions =
        new Dictionary<string, KeybindingDefinition>(StringComparer.Ordinal)
        {
            [CameraMoveForward] = new("Move Forward", Key.W),
            [CameraMoveBackward] = new("Move Backward", Key.S),
            [CameraMoveLeft] = new("Move Left", Key.A),
            [CameraMoveRight] = new("Move Right", Key.D),
            [ToolTranslate] = new("Translate Tool", Key.T),
            [ToolRotate] = new("Rotate Tool", Key.R),
            [ToolScale] = new("Scale Tool", Key.Y),
            [EditDelete] = new("Delete", Key.Delete),
        };

}

public sealed class KeybindingManager : IDisposable
{
    public const string DefaultSettingsPath = "user://keybindings.cfg";

    private const string ConfigSection = "keybindings";

    private readonly Dictionary<string, InputEventKey?> _bindings = new(
        StringComparer.Ordinal
    );
    private readonly Dictionary<Key, string> _bindingOwners = [];
    private readonly string _settingsPath;
    private bool _isDisposed;

    public KeybindingManager(string settingsPath = DefaultSettingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsPath = settingsPath;
        SetDefaultBindings();
    }

    public InputEventKey? GetBinding(string inputActionName) =>
        _bindings[inputActionName];

    public string? FindConflict(
        string inputActionName,
        InputEventKey binding
    )
    {
        ArgumentNullException.ThrowIfNull(binding);

        return _bindingOwners.TryGetValue(GetBindingKey(binding), out string? owner)
            && owner != inputActionName
            ? owner
            : null;
    }

    public void Load()
    {
        ThrowIfDisposed();
        SetDefaultBindings();

        using var config = new ConfigFile();
        if (Godot.FileAccess.FileExists(_settingsPath))
        {
            Error error = config.Load(_settingsPath);
            if (error == Error.Ok)
                LoadBindings(config);
            else
                GD.PushWarning($"Could not load keybindings from {_settingsPath}: {error}");
        }

        RebuildBindingIndex();
        ApplyAll();
    }

    public KeybindingAssignmentResult Assign(
        string inputActionName,
        InputEventKey binding,
        bool replaceConflict = false
    )
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(binding);

        _ = EditorInputActions.Definitions[inputActionName];
        string? conflict = FindConflict(inputActionName, binding);
        if (conflict is not null && !replaceConflict)
            return new KeybindingAssignmentResult(false, conflict);

        if (conflict is string conflictingActionName)
            ReplaceBinding(conflictingActionName, null);

        ReplaceBinding(inputActionName, DuplicateBinding(binding));
        Save();
        return new KeybindingAssignmentResult(true, conflict);
    }

    public void ResetDefaults()
    {
        ThrowIfDisposed();
        SetDefaultBindings();
        ApplyAll();
        Save();
    }

    public string GetDisplayText(string inputActionName) =>
        GetBinding(inputActionName)?.AsTextPhysicalKeycode() ?? "Unassigned";

    public void Dispose()
    {
        if (_isDisposed)
            return;

        DisposeBindings();
        _isDisposed = true;
    }

    private void LoadBindings(ConfigFile config)
    {
        foreach (
            (string actionName, KeybindingDefinition _) in EditorInputActions.Definitions
        )
        {
            if (!config.HasSectionKey(ConfigSection, actionName))
                continue;

            Variant stored = config.GetValue(ConfigSection, actionName);
            if (stored.VariantType == Variant.Type.Bool && !(bool)stored)
            {
                ReplaceBinding(
                    actionName,
                    null,
                    apply: false,
                    updateBindingIndex: false
                );
                continue;
            }

            if (stored.AsGodotObject() is InputEventKey binding)
            {
                ReplaceBinding(
                    actionName,
                    binding,
                    apply: false,
                    updateBindingIndex: false
                );
            }
            else
            {
                GD.PushWarning(
                    $"Ignoring invalid binding for {actionName}"
                );
            }
        }
    }

    private void RebuildBindingIndex()
    {
        _bindingOwners.Clear();
        foreach (
            (string actionName, KeybindingDefinition _) in EditorInputActions.Definitions
        )
        {
            InputEventKey? binding = _bindings[actionName];
            if (binding is null)
                continue;

            if (_bindingOwners.TryAdd(GetBindingKey(binding), actionName))
                continue;

            binding.Dispose();
            _bindings[actionName] = null;
            GD.PushWarning(
                $"Ignoring duplicate binding for {actionName}"
            );
        }
    }

    private void SetDefaultBindings()
    {
        DisposeBindings();
        foreach (
            (string actionName, KeybindingDefinition definition) in EditorInputActions.Definitions
        )
        {
            InputEventKey binding = new()
            {
                PhysicalKeycode = definition.DefaultPhysicalKey,
            };
            _bindings[actionName] = binding;
            _bindingOwners.Add(GetBindingKey(binding), actionName);
        }
    }

    private void ApplyAll()
    {
        foreach (string actionName in EditorInputActions.Definitions.Keys)
            Apply(actionName);
    }

    private void Apply(string inputActionName)
    {
        _ = EditorInputActions.Definitions[inputActionName];
        if (!InputMap.HasAction(inputActionName))
            InputMap.AddAction(inputActionName);

        InputMap.ActionEraseEvents(inputActionName);
        if (_bindings[inputActionName] is InputEventKey binding)
            InputMap.ActionAddEvent(inputActionName, binding);
    }

    private void Save()
    {
        using var config = new ConfigFile();
        foreach (string actionName in EditorInputActions.Definitions.Keys)
        {
            InputEventKey? binding = _bindings[actionName];
            if (binding is null)
                config.SetValue(ConfigSection, actionName, false);
            else
                config.SetValue(ConfigSection, actionName, binding);
        }

        Error error = config.Save(_settingsPath);
        if (error != Error.Ok)
            throw new IOException($"Could not save keybindings to {_settingsPath}: {error}");
    }

    private void ReplaceBinding(
        string inputActionName,
        InputEventKey? binding,
        bool apply = true,
        bool updateBindingIndex = true
    )
    {
        InputEventKey? outgoing = _bindings[inputActionName];
        if (outgoing is not null)
        {
            if (updateBindingIndex)
                _bindingOwners.Remove(GetBindingKey(outgoing));
            outgoing.Dispose();
        }

        _bindings[inputActionName] = binding;
        if (updateBindingIndex && binding is not null)
            _bindingOwners.Add(GetBindingKey(binding), inputActionName);

        if (apply)
            Apply(inputActionName);
    }

    private void DisposeBindings()
    {
        foreach (InputEventKey? binding in _bindings.Values)
            binding?.Dispose();

        _bindings.Clear();
        _bindingOwners.Clear();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, this);

    private static InputEventKey DuplicateBinding(InputEventKey binding) =>
        (InputEventKey)binding.Duplicate();

    private static Key GetBindingKey(InputEventKey binding) =>
        binding.PhysicalKeycode != Key.None
            ? binding.GetPhysicalKeycodeWithModifiers()
            : binding.GetKeycodeWithModifiers();
}

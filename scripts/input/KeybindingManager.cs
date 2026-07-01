#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public interface IKeybindingStore
{
    IReadOnlyDictionary<string, string> Load(out string? warning);

    bool TrySave(IReadOnlyDictionary<string, string> overrides, out string? error);
}

public interface IKeybindingRuntime
{
    void Apply(string actionId, InputBinding? binding);
}

public enum KeybindingChangeStatus
{
    Applied,
    Conflict,
    PersistenceFailed,
}

public sealed record KeybindingChangeResult(
    KeybindingChangeStatus Status,
    string? ConflictingActionId = null,
    string? Error = null
)
{
    public bool WasApplied => Status == KeybindingChangeStatus.Applied;
}

public sealed class KeybindingManager
{
    private readonly IKeybindingStore _store;
    private readonly IKeybindingRuntime _runtime;
    private readonly Action<string> _reportWarning;
    private readonly Dictionary<string, InputBinding?> _bindings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _overrides = new(StringComparer.Ordinal);

    public KeybindingManager(
        IKeybindingStore store,
        IKeybindingRuntime runtime,
        Action<string>? reportWarning = null
    )
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(runtime);

        _store = store;
        _runtime = runtime;
        _reportWarning = reportWarning ?? (_ => { });
        Load();
    }

    public event Action<string>? BindingChanged;

    public InputBinding? GetBinding(string actionId)
    {
        KeybindingCatalog.Get(actionId);
        return _bindings[actionId];
    }

    public string? FindConflict(string actionId, InputBinding? binding)
    {
        KeybindingCatalog.Get(actionId);
        binding = InputBinding.Normalize(binding);
        if (binding == null)
            return null;

        foreach ((string candidateId, InputBinding? candidateBinding) in _bindings)
        {
            if (candidateId != actionId && Equals(candidateBinding, binding))
                return candidateId;
        }

        return null;
    }

    public KeybindingChangeResult SetBinding(
        string actionId,
        InputBinding? binding,
        bool replaceConflict = false
    ) => ChangeBinding(actionId, binding, removeOverride: false, replaceConflict);

    public KeybindingChangeResult ResetBinding(string actionId, bool replaceConflict = false)
    {
        KeybindingDefinition definition = KeybindingCatalog.Get(actionId);
        return ChangeBinding(
            actionId,
            definition.DefaultBinding,
            removeOverride: true,
            replaceConflict
        );
    }

    public KeybindingChangeResult ResetAll()
    {
        Dictionary<string, InputBinding?> nextBindings = KeybindingCatalog.All.ToDictionary(
            definition => definition.ActionId,
            definition => definition.DefaultBinding,
            StringComparer.Ordinal
        );
        if (!_store.TrySave(new Dictionary<string, string>(), out string? error))
            return new KeybindingChangeResult(
                KeybindingChangeStatus.PersistenceFailed,
                Error: error
            );

        _overrides.Clear();
        foreach (KeybindingDefinition definition in KeybindingCatalog.All)
        {
            string actionId = definition.ActionId;
            if (Equals(_bindings[actionId], nextBindings[actionId]))
                continue;

            _bindings[actionId] = nextBindings[actionId];
            _runtime.Apply(actionId, nextBindings[actionId]);
            BindingChanged?.Invoke(actionId);
        }

        return new KeybindingChangeResult(KeybindingChangeStatus.Applied);
    }

    private void Load()
    {
        IReadOnlyDictionary<string, string> persisted = _store.Load(out string? warning);
        if (!string.IsNullOrWhiteSpace(warning))
            _reportWarning(warning);

        foreach (KeybindingDefinition definition in KeybindingCatalog.All)
        {
            InputBinding? binding = definition.DefaultBinding;
            string? validOverride = null;
            if (persisted.TryGetValue(definition.ActionId, out string? encoded))
            {
                if (KeybindingCodec.TryDecode(encoded, out InputBinding? decoded))
                {
                    binding = decoded;
                    validOverride = encoded;
                }
                else
                {
                    _reportWarning(
                        $"Ignoring invalid keybinding for '{definition.ActionId}': '{encoded}'."
                    );
                }
            }

            if (
                binding != null
                && _bindings.FirstOrDefault(pair => Equals(pair.Value, binding))
                    is { Key: not null } conflict
            )
            {
                _reportWarning(
                    $"Ignoring duplicate keybinding for '{definition.ActionId}'; "
                        + $"it is already assigned to '{conflict.Key}'."
                );
                binding = null;
                validOverride = KeybindingCodec.Unbound;
            }

            if (validOverride != null)
                _overrides[definition.ActionId] = validOverride;

            _bindings.Add(definition.ActionId, binding);
            _runtime.Apply(definition.ActionId, binding);
        }
    }

    private KeybindingChangeResult ChangeBinding(
        string actionId,
        InputBinding? binding,
        bool removeOverride,
        bool replaceConflict
    )
    {
        KeybindingCatalog.Get(actionId);
        binding = InputBinding.Normalize(binding);
        string? conflict = FindConflict(actionId, binding);
        if (conflict != null && !replaceConflict)
            return new KeybindingChangeResult(
                KeybindingChangeStatus.Conflict,
                ConflictingActionId: conflict
            );

        bool bindingChanged = !Equals(_bindings[actionId], binding);
        bool conflictChanged = conflict != null && _bindings[conflict] != null;
        bool overrideChanged = removeOverride
            ? _overrides.ContainsKey(actionId)
            : !_overrides.TryGetValue(actionId, out string? currentEncoded)
                || currentEncoded != KeybindingCodec.Encode(binding);

        if (!bindingChanged && !conflictChanged && !overrideChanged)
            return new KeybindingChangeResult(KeybindingChangeStatus.Applied);

        Dictionary<string, string> nextOverrides = new(_overrides, StringComparer.Ordinal);
        if (removeOverride)
            nextOverrides.Remove(actionId);
        else
            nextOverrides[actionId] = KeybindingCodec.Encode(binding);

        if (conflict != null)
            nextOverrides[conflict] = KeybindingCodec.Unbound;

        if (!_store.TrySave(nextOverrides, out string? error))
            return new KeybindingChangeResult(
                KeybindingChangeStatus.PersistenceFailed,
                Error: error
            );

        _overrides.Clear();
        foreach ((string key, string value) in nextOverrides)
            _overrides.Add(key, value);

        if (conflictChanged)
        {
            _bindings[conflict!] = null;
            _runtime.Apply(conflict!, null);
            BindingChanged?.Invoke(conflict!);
        }

        if (bindingChanged)
        {
            _bindings[actionId] = binding;
            _runtime.Apply(actionId, binding);
            BindingChanged?.Invoke(actionId);
        }

        return new KeybindingChangeResult(KeybindingChangeStatus.Applied);
    }
}

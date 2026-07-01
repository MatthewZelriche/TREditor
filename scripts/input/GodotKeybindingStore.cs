#nullable enable

using System;
using System.Collections.Generic;
using Godot;

public sealed class GodotKeybindingStore : IKeybindingStore
{
    public const string ConfigPath = "user://editor.cfg";

    private const string ConfigSection = "keybindings";

    public IReadOnlyDictionary<string, string> Load(out string? warning)
    {
        warning = null;
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        try
        {
            ConfigFile config = new();
            Error error = config.Load(ConfigPath);
            if (error == Error.FileNotFound)
                return values;
            if (error != Error.Ok)
            {
                warning = $"Unable to load keybindings from '{ConfigPath}': {error}.";
                return values;
            }

            if (!config.HasSection(ConfigSection))
                return values;

            foreach (string actionId in config.GetSectionKeys(ConfigSection))
                values[actionId] = config.GetValue(ConfigSection, actionId).AsString();
        }
        catch (Exception exception)
        {
            warning = $"Unable to load keybindings from '{ConfigPath}': {exception.Message}";
        }

        return values;
    }

    public bool TrySave(IReadOnlyDictionary<string, string> overrides, out string? error)
    {
        error = null;
        try
        {
            ConfigFile config = new();
            Error loadError = config.Load(ConfigPath);
            if (loadError != Error.Ok && loadError != Error.FileNotFound)
            {
                error = $"Unable to load '{ConfigPath}' before saving: {loadError}.";
                return false;
            }

            if (config.HasSection(ConfigSection))
                config.EraseSection(ConfigSection);

            foreach ((string actionId, string encoded) in overrides)
                config.SetValue(ConfigSection, actionId, encoded);

            Error saveError = config.Save(ConfigPath);
            if (saveError != Error.Ok)
            {
                error = $"Unable to save keybindings to '{ConfigPath}': {saveError}.";
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            error = $"Unable to save keybindings to '{ConfigPath}': {exception.Message}";
            return false;
        }
    }
}

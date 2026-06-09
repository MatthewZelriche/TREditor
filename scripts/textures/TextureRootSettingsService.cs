#nullable enable

using System;
using System.IO;
using Godot;

/// <summary>
/// Owns the editor's single optional texture search root and persists it in editor.cfg.
/// </summary>
public sealed class TextureRootSettingsService
{
    public const string ConfigPath = "user://editor.cfg";

    private const string ConfigSection = "textures";
    private const string RootPathKey = "root_path";

    private readonly Action<string?> _persistConfiguredRoot;
    private readonly Func<string, bool> _directoryExists;

    public string? RootPath { get; private set; }

    public TextureRootSettingsService()
        : this(ReadConfiguredRoot, PersistConfiguredRoot, Directory.Exists) { }

    public TextureRootSettingsService(
        Func<string?> readConfiguredRoot,
        Action<string?> persistConfiguredRoot,
        Func<string, bool> directoryExists
    )
    {
        ArgumentNullException.ThrowIfNull(readConfiguredRoot);
        ArgumentNullException.ThrowIfNull(persistConfiguredRoot);
        ArgumentNullException.ThrowIfNull(directoryExists);

        _persistConfiguredRoot = persistConfiguredRoot;
        _directoryExists = directoryExists;
        RootPath = NormalizeAndValidate(readConfiguredRoot(), _directoryExists);
    }

    /// <summary>
    /// Validates and persists a new root. Invalid paths leave the current setting unchanged.
    /// </summary>
    public bool TrySetRootPath(string rootPath)
    {
        string? normalized = NormalizeAndValidate(rootPath, _directoryExists);
        if (normalized == null)
            return false;

        _persistConfiguredRoot(normalized);
        RootPath = normalized;
        return true;
    }

    public void ClearRootPath()
    {
        _persistConfiguredRoot(null);
        RootPath = null;
    }

    private static string? NormalizeAndValidate(
        string? configuredRoot,
        Func<string, bool> directoryExists
    )
    {
        // An absent setting deliberately means "no texture root". In particular, do not probe
        // conventional folders here; choosing a root remains an explicit editor/user decision.
        if (string.IsNullOrWhiteSpace(configuredRoot))
            return null;

        try
        {
            string normalized = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(configuredRoot.Trim())
            );
            return directoryExists(normalized) ? normalized : null;
        }
        catch (Exception exception)
            when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static string? ReadConfiguredRoot()
    {
        try
        {
            var config = new ConfigFile();
            Error error = config.Load(ConfigPath);
            if (error == Error.FileNotFound)
                return null;
            if (error != Error.Ok)
            {
                GD.PushWarning($"Unable to load editor settings '{ConfigPath}': {error}.");
                return null;
            }

            return config.HasSectionKey(ConfigSection, RootPathKey)
                ? config.GetValue(ConfigSection, RootPathKey).AsString()
                : null;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Unable to load editor settings '{ConfigPath}': {exception.Message}");
            return null;
        }
    }

    private static void PersistConfiguredRoot(string? rootPath)
    {
        try
        {
            var config = new ConfigFile();
            Error loadError = config.Load(ConfigPath);
            if (loadError != Error.Ok && loadError != Error.FileNotFound)
            {
                GD.PushWarning($"Unable to load editor settings '{ConfigPath}': {loadError}.");
                return;
            }

            if (rootPath == null)
                config.EraseSectionKey(ConfigSection, RootPathKey);
            else
                config.SetValue(ConfigSection, RootPathKey, rootPath);

            Error saveError = config.Save(ConfigPath);
            if (saveError != Error.Ok)
                GD.PushWarning($"Unable to save editor settings '{ConfigPath}': {saveError}.");
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Unable to save editor settings '{ConfigPath}': {exception.Message}");
        }
    }
}

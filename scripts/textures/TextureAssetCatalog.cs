#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using Godot;

public readonly record struct TextureAsset(string AssetId, string FilePath);

public readonly record struct TextureAssetDiscoveryError(string Path, string Message);

public readonly record struct TextureAssetDiscoveryResult(
    IReadOnlyList<TextureAsset> Assets,
    IReadOnlyList<TextureAssetDiscoveryError> Errors
);

/// <summary>
/// Discovers texture assets beneath one root, owns the active texture identity, and incrementally
/// loads cached browser previews.
/// </summary>
public sealed class TextureAssetCatalog
{
    public const int DefaultPreviewsPerFrame = 2;

    private static readonly HashSet<string> SupportedExtensions = new(
        [".png", ".jpg", ".jpeg", ".webp"],
        StringComparer.OrdinalIgnoreCase
    );

    private readonly Func<string, TextureAssetDiscoveryResult> _discover;
    private readonly QueuedResourceCache<string, Texture2D> _previews;
    private readonly Dictionary<string, string> _filePathsByAssetId = new(StringComparer.Ordinal);

    public IReadOnlyList<TextureAsset> Assets { get; private set; } = [];

    public IReadOnlyList<TextureAssetDiscoveryError> Errors { get; private set; } = [];

    public string? ActiveAssetId { get; private set; }

    public TextureAssetCatalog()
        : this(Discover, TexturePreviewLoader.Load, TexturePreviewLoader.CreateFallback) { }

    public TextureAssetCatalog(
        Func<string, TextureAssetDiscoveryResult> discover,
        Func<string, Texture2D?>? loadPreview = null,
        Func<Texture2D>? createFallbackPreview = null
    )
    {
        ArgumentNullException.ThrowIfNull(discover);
        _discover = discover;
        _previews = new QueuedResourceCache<string, Texture2D>(
            loadPreview ?? TexturePreviewLoader.Load,
            createFallbackPreview ?? TexturePreviewLoader.CreateFallback
        );
    }

    /// <summary>
    /// Replaces the catalog with a deterministic snapshot of <paramref name="rootPath"/>.
    /// A missing root produces an empty catalog without attempting filesystem discovery.
    /// </summary>
    public void Rescan(string? rootPath)
    {
        if (rootPath == null)
        {
            Assets = [];
            Errors = [];
            ActiveAssetId = null;
            _filePathsByAssetId.Clear();
            _previews.Synchronize([]);
            return;
        }

        TextureAssetDiscoveryResult result = _discover(rootPath);
        Assets = result.Assets.OrderBy(asset => asset.AssetId, StringComparer.Ordinal).ToArray();
        Errors = result.Errors.ToArray();
        _filePathsByAssetId.Clear();
        foreach (TextureAsset asset in Assets)
            _filePathsByAssetId[asset.AssetId] = asset.FilePath;
        _previews.Synchronize(Assets.Select(asset => asset.FilePath));

        // A partial scan cannot prove that the active texture disappeared. Preserve its identity
        // until a complete scan succeeds, so a temporary unreadable folder does not change the
        // texture the user intends to paint with.
        if (
            Errors.Count == 0
            && ActiveAssetId != null
            && !_filePathsByAssetId.ContainsKey(ActiveAssetId)
        )
        {
            ActiveAssetId = null;
        }
    }

    public bool TrySetActiveAsset(string assetId)
    {
        string normalized = TextureMaterialLibrary.NormalizeAssetId(assetId);
        if (!_filePathsByAssetId.ContainsKey(normalized))
            return false;

        ActiveAssetId = normalized;
        return true;
    }

    public int ProcessPreviewQueue(int maximumCount = DefaultPreviewsPerFrame) =>
        _previews.Process(maximumCount);

    public bool TryGetPreview(string assetId, out QueuedResource<Texture2D> preview)
    {
        string normalized = TextureMaterialLibrary.NormalizeAssetId(assetId);
        if (_filePathsByAssetId.TryGetValue(normalized, out string? filePath))
            return _previews.TryGet(filePath, out preview);

        preview = default;
        return false;
    }

    private static TextureAssetDiscoveryResult Discover(string rootPath)
    {
        List<TextureAsset> assets = [];
        List<TextureAssetDiscoveryError> errors = [];
        Stack<string> pendingDirectories = new();
        pendingDirectories.Push(rootPath);

        while (pendingDirectories.TryPop(out string? directory))
        {
            foreach (string entryPath in EnumerateEntries(directory, errors))
            {
                if (!TryGetAttributes(entryPath, errors, out FileAttributes attributes))
                    continue;

                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    // Do not recurse through directory links. Their targets may sit outside the
                    // selected root or form cycles, neither of which belongs in this catalog.
                    if (!attributes.HasFlag(FileAttributes.ReparsePoint))
                        pendingDirectories.Push(entryPath);
                    continue;
                }

                if (!SupportedExtensions.Contains(Path.GetExtension(entryPath)))
                    continue;

                try
                {
                    string assetId = TextureMaterialLibrary.NormalizeAssetId(
                        Path.GetRelativePath(rootPath, entryPath)
                    );
                    assets.Add(new TextureAsset(assetId, entryPath));
                }
                catch (Exception exception)
                    when (exception is ArgumentException or NotSupportedException)
                {
                    errors.Add(new TextureAssetDiscoveryError(entryPath, exception.Message));
                }
            }
        }

        return new TextureAssetDiscoveryResult(assets, errors);
    }

    private static IReadOnlyList<string> EnumerateEntries(
        string directory,
        List<TextureAssetDiscoveryError> errors
    )
    {
        try
        {
            return Directory.EnumerateFileSystemEntries(directory).ToArray();
        }
        catch (Exception exception) when (IsFilesystemException(exception))
        {
            errors.Add(new TextureAssetDiscoveryError(directory, exception.Message));
            return [];
        }
    }

    private static bool TryGetAttributes(
        string path,
        List<TextureAssetDiscoveryError> errors,
        out FileAttributes attributes
    )
    {
        try
        {
            attributes = File.GetAttributes(path);
            return true;
        }
        catch (Exception exception) when (IsFilesystemException(exception))
        {
            errors.Add(new TextureAssetDiscoveryError(path, exception.Message));
            attributes = default;
            return false;
        }
    }

    private static bool IsFilesystemException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException;
}

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public sealed class TextureBrowserState
{
    public string SearchText { get; private set; } = "";

    public void SetSearchText(string? searchText)
    {
        SearchText = searchText?.Trim() ?? "";
    }

    public IReadOnlyList<TextureAsset> Filter(IReadOnlyList<TextureAsset> assets)
    {
        if (SearchText.Length == 0)
            return assets;

        return assets
            .Where(asset => asset.AssetId.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public string GetStatus(
        string? rootPath,
        int assetCount,
        int visibleCount,
        int errorCount
    )
    {
        if (rootPath == null)
            return "Choose a texture folder to begin.";
        if (assetCount == 0)
            return errorCount > 0
                ? $"No textures found. Scan completed with {errorCount} warning(s)."
                : "No supported textures found.";
        if (visibleCount == 0)
            return "No textures match the current search.";
        if (errorCount > 0)
            return $"{visibleCount} texture(s). Scan completed with {errorCount} warning(s).";
        return $"{visibleCount} texture(s).";
    }
}

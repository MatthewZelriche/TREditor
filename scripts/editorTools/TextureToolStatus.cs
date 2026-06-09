#nullable enable

public static class TextureToolStatus
{
    public static string GetActiveTextureMessage(
        string? activeAssetId,
        QueuedResourceState? previewState
    )
    {
        if (activeAssetId == null)
            return "Texture tool: select an active texture.";
        if (previewState == QueuedResourceState.Failed)
            return $"Texture tool: '{activeAssetId}' failed to load; using fallback.";
        return "";
    }
}

#nullable enable

using Godot;

public readonly record struct TextureFaceApplyIntent(string AssetId, FaceHandle Face);

public static class TextureToolInput
{
    public static bool TryResolveApplyIntent(
        ViewportMouseButtonEvent input,
        string? activeAssetId,
        ScenePickHit hit,
        out TextureFaceApplyIntent intent
    )
    {
        intent = default;
        if (
            input.Button != MouseButton.Left
            || !input.Pressed
            || activeAssetId == null
            || hit.Kind != ScenePickElementKind.Face
        )
        {
            return false;
        }

        intent = new TextureFaceApplyIntent(activeAssetId, hit.Face);
        return true;
    }
}

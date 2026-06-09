#nullable enable

using System.IO;
using Godot;

/// <summary>
/// Loads full-resolution textures identified by paths relative to one validated texture root.
/// </summary>
public static class TextureFileLoader
{
    public static Texture2D? Load(string rootPath, string assetId)
    {
        string normalizedAssetId = TextureMaterialLibrary.NormalizeAssetId(assetId);
        string filePath = Path.Combine(
            rootPath,
            normalizedAssetId.Replace('/', Path.DirectorySeparatorChar)
        );

        if (!File.Exists(filePath))
            return null;

        Image image = Image.LoadFromFile(filePath);
        if (image == null || image.IsEmpty())
            return null;

        if (!image.HasMipmaps())
            image.GenerateMipmaps();

        return ImageTexture.CreateFromImage(image);
    }
}

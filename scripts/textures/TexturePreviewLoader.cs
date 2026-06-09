#nullable enable

using Godot;

public static class TexturePreviewLoader
{
    private const int PreviewSize = 128;
    private const int CheckerSize = 8;

    public static Texture2D? Load(string filePath)
    {
        Image image = Image.LoadFromFile(filePath);
        if (image == null || image.IsEmpty())
            return null;

        float scale = Mathf.Min(
            (float)PreviewSize / image.GetWidth(),
            (float)PreviewSize / image.GetHeight()
        );
        if (scale < 1.0f)
        {
            image.Resize(
                Mathf.Max(1, Mathf.RoundToInt(image.GetWidth() * scale)),
                Mathf.Max(1, Mathf.RoundToInt(image.GetHeight() * scale)),
                Image.Interpolation.Lanczos
            );
        }

        return ImageTexture.CreateFromImage(image);
    }

    public static Texture2D CreateFallback()
    {
        var image = Image.CreateEmpty(PreviewSize, PreviewSize, false, Image.Format.Rgba8);
        for (int y = 0; y < PreviewSize; y++)
        {
            for (int x = 0; x < PreviewSize; x++)
            {
                bool magenta = (x / CheckerSize + y / CheckerSize) % 2 == 0;
                image.SetPixel(x, y, magenta ? Colors.Magenta : Colors.Black);
            }
        }

        return ImageTexture.CreateFromImage(image);
    }
}

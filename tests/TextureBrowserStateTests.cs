namespace TREditor2026.Tests;

public sealed class TextureBrowserStateTests
{
    private static readonly TextureAsset[] Assets =
    [
        new("floors/metal.png", @"C:\textures\floors\metal.png"),
        new("walls/brick.png", @"C:\textures\walls\brick.png"),
    ];

    [Fact]
    public void Filter_MatchesAssetIdsCaseInsensitively()
    {
        var state = new TextureBrowserState();
        state.SetSearchText("BRICK");

        Assert.Equal(["walls/brick.png"], state.Filter(Assets).Select(asset => asset.AssetId));
    }

    [Fact]
    public void Filter_EmptySearchReturnsCompleteCatalog()
    {
        var state = new TextureBrowserState();
        state.SetSearchText("   ");

        Assert.Same(Assets, state.Filter(Assets));
    }

    [Fact]
    public void GetStatus_DescribesImportantEmptyAndWarningStates()
    {
        var state = new TextureBrowserState();

        Assert.Equal("Choose a texture folder to begin.", state.GetStatus(null, 0, 0, 0));
        Assert.Equal("No supported textures found.", state.GetStatus(@"C:\textures", 0, 0, 0));
        Assert.Equal(
            "No textures match the current search.",
            state.GetStatus(@"C:\textures", 2, 0, 0)
        );
        Assert.Contains("1 warning(s)", state.GetStatus(@"C:\textures", 2, 2, 1));
    }
}

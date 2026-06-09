namespace TREditor2026.Tests;

public sealed class TextureToolStatusTests
{
    [Fact]
    public void GetActiveTextureMessage_NoActiveTexturePromptsForSelection()
    {
        Assert.Equal(
            "Texture tool: select an active texture.",
            TextureToolStatus.GetActiveTextureMessage(null, null)
        );
    }

    [Fact]
    public void GetActiveTextureMessage_FailedPreviewReportsFallback()
    {
        string message = TextureToolStatus.GetActiveTextureMessage(
            "walls/missing.png",
            QueuedResourceState.Failed
        );

        Assert.Contains("walls/missing.png", message);
        Assert.Contains("fallback", message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(QueuedResourceState.Pending)]
    [InlineData(QueuedResourceState.Loaded)]
    public void GetActiveTextureMessage_UsableOrPendingTextureClearsStatus(
        QueuedResourceState? state
    )
    {
        Assert.Equal("", TextureToolStatus.GetActiveTextureMessage("walls/brick.png", state));
    }
}

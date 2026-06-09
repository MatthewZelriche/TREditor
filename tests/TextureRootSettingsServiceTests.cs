namespace TREditor2026.Tests;

public sealed class TextureRootSettingsServiceTests
{
    [Fact]
    public void Constructor_MissingSettingLeavesRootUnsetWithoutProbingFilesystem()
    {
        int probes = 0;

        var settings = new TextureRootSettingsService(
            () => null,
            _ => { },
            _ =>
            {
                probes++;
                return true;
            }
        );

        Assert.Null(settings.RootPath);
        Assert.Equal(0, probes);
    }

    [Fact]
    public void Constructor_NormalizesValidConfiguredRoot()
    {
        string configured = Path.Combine(Path.GetTempPath(), "textures", "..", "textures");
        string expected = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured));

        var settings = new TextureRootSettingsService(
            () => configured,
            _ => { },
            path => path == expected
        );

        Assert.Equal(expected, settings.RootPath);
    }

    [Fact]
    public void Constructor_InvalidConfiguredRootLeavesRootUnset()
    {
        var settings = new TextureRootSettingsService(
            () => Path.Combine(Path.GetTempPath(), "missing-texture-root"),
            _ => { },
            _ => false
        );

        Assert.Null(settings.RootPath);
    }

    [Fact]
    public void TrySetRootPath_PersistsNormalizedValidRoot()
    {
        string? persisted = null;
        string configured = Path.Combine(Path.GetTempPath(), "textures", ".");
        string expected = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured));
        var settings = new TextureRootSettingsService(
            () => null,
            value => persisted = value,
            _ => true
        );

        bool changed = settings.TrySetRootPath(configured);

        Assert.True(changed);
        Assert.Equal(expected, settings.RootPath);
        Assert.Equal(expected, persisted);
    }

    [Fact]
    public void PersistedRootCanBeLoadedByANewService()
    {
        string? persisted = null;
        string configured = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "textures"));
        var first = new TextureRootSettingsService(
            () => persisted,
            value => persisted = value,
            _ => true
        );

        Assert.True(first.TrySetRootPath(configured));

        var second = new TextureRootSettingsService(() => persisted, _ => { }, _ => true);
        Assert.Equal(configured, second.RootPath);
    }

    [Fact]
    public void TrySetRootPath_InvalidRootDoesNotReplaceOrPersistCurrentRoot()
    {
        string initial = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "textures"));
        int writes = 0;
        var settings = new TextureRootSettingsService(
            () => initial,
            _ => writes++,
            path => path == initial
        );

        bool changed = settings.TrySetRootPath(Path.Combine(Path.GetTempPath(), "missing"));

        Assert.False(changed);
        Assert.Equal(initial, settings.RootPath);
        Assert.Equal(0, writes);
    }

    [Fact]
    public void ClearRootPath_PersistsMissingSetting()
    {
        string initial = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "textures"));
        string? persisted = initial;
        var settings = new TextureRootSettingsService(
            () => initial,
            value => persisted = value,
            _ => true
        );

        settings.ClearRootPath();

        Assert.Null(settings.RootPath);
        Assert.Null(persisted);
    }
}

namespace TREditor2026.Tests;

public sealed class TextureAssetCatalogTests
{
    [Fact]
    public void Rescan_RecursivelyDiscoversSupportedImagesWithSortedNormalizedIds()
    {
        using var fixture = new TemporaryDirectory();
        fixture.CreateFile("z-last.WEBP");
        fixture.CreateFile("walls/brick.PNG");
        fixture.CreateFile("walls/metal.jpeg");
        fixture.CreateFile("floors/tile.jpg");
        fixture.CreateFile("notes.txt");

        var catalog = new TextureAssetCatalog();
        catalog.Rescan(fixture.Path);

        Assert.Equal(
            ["floors/tile.jpg", "walls/brick.PNG", "walls/metal.jpeg", "z-last.WEBP"],
            catalog.Assets.Select(asset => asset.AssetId)
        );
        Assert.All(catalog.Assets, asset => Assert.True(Path.IsPathFullyQualified(asset.FilePath)));
        Assert.Empty(catalog.Errors);
    }

    [Fact]
    public void Rescan_MissingRootDoesNotAttemptDiscovery()
    {
        int scans = 0;
        var catalog = new TextureAssetCatalog(_ =>
        {
            scans++;
            return new TextureAssetDiscoveryResult([], []);
        });

        catalog.Rescan(null);

        Assert.Equal(0, scans);
        Assert.Empty(catalog.Assets);
        Assert.Empty(catalog.Errors);
    }

    [Fact]
    public void Rescan_MissingConfiguredDirectoryReportsErrorWithoutThrowing()
    {
        string missingRoot = Path.Combine(Path.GetTempPath(), $"TREditor2026-{Guid.NewGuid():N}");
        var catalog = new TextureAssetCatalog();

        catalog.Rescan(missingRoot);

        Assert.Empty(catalog.Assets);
        Assert.Equal(missingRoot, Assert.Single(catalog.Errors).Path);
    }

    [Fact]
    public void Rescan_PreservesActiveAssetThatStillExists()
    {
        using var fixture = new TemporaryDirectory();
        fixture.CreateFile("walls/brick.png");
        var catalog = new TextureAssetCatalog();
        catalog.Rescan(fixture.Path);
        Assert.True(catalog.TrySetActiveAsset(@"walls\brick.png"));

        fixture.CreateFile("walls/metal.png");
        catalog.Rescan(fixture.Path);

        Assert.Equal("walls/brick.png", catalog.ActiveAssetId);
    }

    [Fact]
    public void Rescan_CompleteScanClearsActiveAssetThatNoLongerExists()
    {
        using var fixture = new TemporaryDirectory();
        string texturePath = fixture.CreateFile("walls/brick.png");
        var catalog = new TextureAssetCatalog();
        catalog.Rescan(fixture.Path);
        Assert.True(catalog.TrySetActiveAsset("walls/brick.png"));

        File.Delete(texturePath);
        catalog.Rescan(fixture.Path);

        Assert.Null(catalog.ActiveAssetId);
    }

    [Fact]
    public void Rescan_PartialScanReportsErrorAndPreservesActiveAsset()
    {
        int scan = 0;
        var asset = new TextureAsset("walls/brick.png", @"C:\textures\walls\brick.png");
        var catalog = new TextureAssetCatalog(_ =>
        {
            scan++;
            return scan == 1
                ? new TextureAssetDiscoveryResult([asset], [])
                : new TextureAssetDiscoveryResult(
                    [],
                    [new TextureAssetDiscoveryError(@"C:\textures\walls", "Access denied.")]
                );
        });
        catalog.Rescan(@"C:\textures");
        Assert.True(catalog.TrySetActiveAsset(asset.AssetId));

        catalog.Rescan(@"C:\textures");

        Assert.Equal(asset.AssetId, catalog.ActiveAssetId);
        Assert.Equal("Access denied.", Assert.Single(catalog.Errors).Message);
    }

    [Fact]
    public void TrySetActiveAsset_RejectsUnknownAssetWithoutChangingCurrentAsset()
    {
        var asset = new TextureAsset("walls/brick.png", @"C:\textures\walls\brick.png");
        var catalog = new TextureAssetCatalog(_ => new TextureAssetDiscoveryResult([asset], []));
        catalog.Rescan(@"C:\textures");
        Assert.True(catalog.TrySetActiveAsset(asset.AssetId));

        bool changed = catalog.TrySetActiveAsset("walls/missing.png");

        Assert.False(changed);
        Assert.Equal(asset.AssetId, catalog.ActiveAssetId);
    }

    [Fact]
    public void Rescan_QueuesDiscoveredAssetPreviewsAsPending()
    {
        var asset = new TextureAsset("walls/brick.png", @"C:\textures\walls\brick.png");
        var catalog = new TextureAssetCatalog(_ => new TextureAssetDiscoveryResult([asset], []));

        catalog.Rescan(@"C:\textures");

        Assert.True(catalog.TryGetPreview(asset.AssetId, out var preview));
        Assert.Equal(QueuedResourceState.Pending, preview.State);
        Assert.Null(preview.Resource);
        Assert.False(catalog.TryGetPreview("walls/missing.png", out _));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"TREditor2026-{Guid.NewGuid():N}"
            );

        public TemporaryDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public string CreateFile(string relativePath)
        {
            string filePath = System.IO.Path.Combine(
                Path,
                relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar)
            );
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "");
            return filePath;
        }

        public void Dispose()
        {
            Directory.Delete(Path, true);
        }
    }
}

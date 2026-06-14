namespace TREditor2026.Tests;

public sealed class QueuedResourceCacheTests
{
    [Fact]
    public void Process_ResolvesOnlyBoundedNumberInQueueOrder()
    {
        List<string> loads = [];
        var cache = CreateCache(key =>
        {
            loads.Add(key);
            return new object();
        });
        cache.Synchronize(["first", "second", "third"]);

        int processed = cache.Process(2);

        Assert.Equal(2, processed);
        Assert.Equal(["first", "second"], loads);
        Assert.Equal(QueuedResourceState.Loaded, Get(cache, "first").State);
        Assert.Equal(QueuedResourceState.Loaded, Get(cache, "second").State);
        Assert.Equal(QueuedResourceState.Pending, Get(cache, "third").State);
    }

    [Fact]
    public void Synchronize_RetainsLoadedResourcesWithoutQueueingThemAgain()
    {
        int loads = 0;
        var cache = CreateCache(_ =>
        {
            loads++;
            return new object();
        });
        cache.Synchronize(["brick"]);
        cache.Process(1);
        object? first = Get(cache, "brick").Resource;

        cache.Synchronize(["brick"]);
        int processed = cache.Process(1);

        Assert.Equal(0, processed);
        Assert.Equal(1, loads);
        Assert.Same(first, Get(cache, "brick").Resource);
    }

    [Fact]
    public void Process_FailedLoadsShareFallbackAndDoNotInterruptQueue()
    {
        var fallback = new object();
        int fallbackCreations = 0;
        var cache = new QueuedResourceCache<string, object>(
            key => key == "throws" ? throw new InvalidDataException() : null,
            () =>
            {
                fallbackCreations++;
                return fallback;
            }
        );
        cache.Synchronize(["throws", "missing"]);

        int processed = cache.Process(2);

        Assert.Equal(2, processed);
        Assert.Equal(QueuedResourceState.Failed, Get(cache, "throws").State);
        Assert.Equal(QueuedResourceState.Failed, Get(cache, "missing").State);
        Assert.Same(fallback, Get(cache, "throws").Resource);
        Assert.Same(fallback, Get(cache, "missing").Resource);
        Assert.Equal(1, fallbackCreations);
    }

    [Fact]
    public void Synchronize_RemovesStaleEntriesAndQueuesNewEntriesInSnapshotOrder()
    {
        List<string> loads = [];
        var cache = CreateCache(key =>
        {
            loads.Add(key);
            return new object();
        });
        cache.Synchronize(["removed"]);

        cache.Synchronize(["second", "first"]);
        cache.Process(2);

        Assert.False(cache.TryGet("removed", out _));
        Assert.Equal(["second", "first"], loads);
    }

    private static QueuedResourceCache<string, object> CreateCache(Func<string, object?> load) =>
        new(load, () => new object());

    private static QueuedResource<object> Get(QueuedResourceCache<string, object> cache, string key)
    {
        Assert.True(cache.TryGet(key, out QueuedResource<object> resource));
        return resource;
    }
}

namespace TREditor2026.Tests;

public sealed class LazyResourceCacheTests
{
    [Fact]
    public void Resolve_LoadsEachKeyOnceAndReusesResource()
    {
        int loads = 0;
        var expected = new object();
        var cache = new LazyResourceCache<string, object>(
            _ =>
            {
                loads++;
                return expected;
            },
            () => new object()
        );

        object first = cache.Resolve("brick");
        object second = cache.Resolve("brick");

        Assert.Same(expected, first);
        Assert.Same(first, second);
        Assert.Equal(1, loads);
    }

    [Fact]
    public void Resolve_MissingKeysReuseOneFallback()
    {
        int fallbackCreations = 0;
        var cache = new LazyResourceCache<string, object>(
            _ => null,
            () =>
            {
                fallbackCreations++;
                return new object();
            }
        );

        object first = cache.Resolve("missing-a");
        object second = cache.Resolve("missing-b");

        Assert.Same(first, second);
        Assert.Equal(1, fallbackCreations);
    }
}

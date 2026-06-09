#nullable enable

using System;
using System.Collections.Generic;

/// <summary>
/// Lazily resolves keyed resources once and substitutes one shared fallback when resolution fails.
/// </summary>
public sealed class LazyResourceCache<TKey, TResource>
    where TKey : notnull
    where TResource : class
{
    private readonly Func<TKey, TResource?> _load;
    private readonly Func<TResource> _createFallback;
    private readonly Dictionary<TKey, TResource> _resources = [];
    private TResource? _fallback;

    public LazyResourceCache(Func<TKey, TResource?> load, Func<TResource> createFallback)
    {
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(createFallback);

        _load = load;
        _createFallback = createFallback;
    }

    public TResource Resolve(TKey key)
    {
        if (_resources.TryGetValue(key, out TResource? cached))
            return cached;

        TResource resource = _load(key) ?? GetFallback();
        _resources.Add(key, resource);
        return resource;
    }

    public void Clear()
    {
        _resources.Clear();
        _fallback = null;
    }

    private TResource GetFallback()
    {
        return _fallback ??=
            _createFallback()
            ?? throw new InvalidOperationException("Fallback resource factory returned null.");
    }
}

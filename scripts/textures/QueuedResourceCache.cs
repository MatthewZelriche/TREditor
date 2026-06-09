#nullable enable

using System;
using System.Collections.Generic;

public enum QueuedResourceState
{
    Pending,
    Loaded,
    Failed,
}

public readonly record struct QueuedResource<TResource>(
    QueuedResourceState State,
    TResource? Resource
)
    where TResource : class;

/// <summary>
/// Retains resolved resources across snapshots and resolves newly added keys in snapshot order.
/// </summary>
public sealed class QueuedResourceCache<TKey, TResource>
    where TKey : notnull
    where TResource : class
{
    private readonly Func<TKey, TResource?> _load;
    private readonly Func<TResource> _createFallback;
    private readonly Dictionary<TKey, QueuedResource<TResource>> _resources = [];
    private Queue<TKey> _pending = [];
    private TResource? _fallback;

    public QueuedResourceCache(Func<TKey, TResource?> load, Func<TResource> createFallback)
    {
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(createFallback);
        _load = load;
        _createFallback = createFallback;
    }

    public void Synchronize(IEnumerable<TKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        Dictionary<TKey, QueuedResource<TResource>> synchronized = [];
        Queue<TKey> pending = [];
        foreach (TKey key in keys)
        {
            if (synchronized.ContainsKey(key))
                continue;

            if (_resources.TryGetValue(key, out QueuedResource<TResource> existing))
            {
                synchronized.Add(key, existing);
                if (existing.State == QueuedResourceState.Pending)
                    pending.Enqueue(key);
            }
            else
            {
                synchronized.Add(
                    key,
                    new QueuedResource<TResource>(QueuedResourceState.Pending, null)
                );
                pending.Enqueue(key);
            }
        }

        _resources.Clear();
        foreach ((TKey key, QueuedResource<TResource> resource) in synchronized)
            _resources.Add(key, resource);
        _pending = pending;
    }

    public int Process(int maximumCount)
    {
        if (maximumCount < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumCount));

        int processed = 0;
        while (processed < maximumCount && _pending.TryDequeue(out TKey? key))
        {
            if (
                !_resources.TryGetValue(key, out QueuedResource<TResource> current)
                || current.State != QueuedResourceState.Pending
            )
            {
                continue;
            }

            TResource? resource = null;
            try
            {
                resource = _load(key);
            }
            catch
            {
                // A failed loader is represented as a failed preview. The catalog/browser can
                // report that state without allowing one corrupt file to interrupt later work.
            }

            _resources[key] =
                resource != null
                    ? new QueuedResource<TResource>(QueuedResourceState.Loaded, resource)
                    : new QueuedResource<TResource>(QueuedResourceState.Failed, GetFallback());
            processed++;
        }

        return processed;
    }

    public bool TryGet(TKey key, out QueuedResource<TResource> resource) =>
        _resources.TryGetValue(key, out resource);

    private TResource GetFallback()
    {
        return _fallback ??=
            _createFallback()
            ?? throw new InvalidOperationException("Fallback resource factory returned null.");
    }
}

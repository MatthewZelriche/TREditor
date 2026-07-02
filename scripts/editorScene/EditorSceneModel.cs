using System;
using System.Collections.Generic;

/// <summary>
/// Authoritative registry of live editor objects. Owns inserted <see cref="EditorObjectModel"/>
/// instances until they are removed or the model is cleared/disposed.
/// </summary>
public sealed class EditorSceneModel : IDisposable
{
    private readonly Dictionary<EditorObjectId, EditorObjectModel> _objects = [];
    private bool _disposed;

    public bool Add(EditorObjectModel obj)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(obj);

        if (_objects.ContainsKey(obj.Id))
            return false;

        _objects.Add(obj.Id, obj);
        return true;
    }

    public EditorObjectModel Remove(EditorObjectId id)
    {
        ThrowIfDisposed();

        if (!_objects.Remove(id, out EditorObjectModel obj))
            return null;

        return obj;
    }

    public bool TryGet(EditorObjectId id, out EditorObjectModel obj)
    {
        ThrowIfDisposed();
        return _objects.TryGetValue(id, out obj);
    }

    public bool Contains(EditorObjectId id)
    {
        ThrowIfDisposed();
        return _objects.ContainsKey(id);
    }

    public IReadOnlyCollection<EditorObjectModel> Objects
    {
        get
        {
            ThrowIfDisposed();
            return _objects.Values;
        }
    }

    public int Count
    {
        get
        {
            ThrowIfDisposed();
            return _objects.Count;
        }
    }

    public void Clear()
    {
        ThrowIfDisposed();

        foreach (EditorObjectModel obj in _objects.Values)
            obj.Dispose();

        _objects.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (EditorObjectModel obj in _objects.Values)
            obj.Dispose();

        _objects.Clear();
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

using System.Collections.Generic;

// Stencil values are shared by the surface renderer and component overlays. Zero is reserved for
// meshes beyond the 8-bit stencil buffer's capacity; those meshes still write zero so they occlude
// other objects' overlays, while their own overlays gracefully fall back to normal depth testing.
internal sealed class MeshStencilReferences
{
    private const int MaximumReference = 255;

    private readonly Dictionary<MeshObjectId, int> _references = [];
    private readonly SortedSet<int> _available = CreateAvailableReferences();

    public int Acquire(MeshObjectId id)
    {
        if (_references.TryGetValue(id, out int existing))
            return existing;

        int reference = _available.Count == 0 ? 0 : _available.Min;
        if (reference != 0)
            _available.Remove(reference);
        _references.Add(id, reference);
        return reference;
    }

    public int Get(MeshObjectId id) => _references.GetValueOrDefault(id);

    public void Release(MeshObjectId id)
    {
        if (_references.Remove(id, out int reference) && reference != 0)
            _available.Add(reference);
    }

    public void Clear()
    {
        _references.Clear();
        _available.Clear();
        for (int reference = 1; reference <= MaximumReference; reference++)
            _available.Add(reference);
    }

    private static SortedSet<int> CreateAvailableReferences()
    {
        var references = new SortedSet<int>();
        for (int reference = 1; reference <= MaximumReference; reference++)
            references.Add(reference);
        return references;
    }
}

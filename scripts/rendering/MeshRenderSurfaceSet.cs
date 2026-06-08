using System;
using System.Collections.Generic;

/// <summary>
/// Reusable render-surface buckets keyed by material slot.
/// </summary>
public sealed class MeshRenderSurfaceSet
{
    private readonly Dictionary<int, MeshRenderSurfaceData> _surfacesBySlot = [];
    private readonly List<MeshRenderSurfaceData> _activeSurfaces = [];

    public IReadOnlyList<MeshRenderSurfaceData> ActiveSurfaces => _activeSurfaces;

    /// <summary>
    /// Returns the active surface for <paramref name="materialSlot"/>, retaining list capacities
    /// from previous rebuilds when that slot is reused.
    /// </summary>
    public MeshRenderData GetOrCreateSurface(int materialSlot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(materialSlot);

        if (!_surfacesBySlot.TryGetValue(materialSlot, out MeshRenderSurfaceData surface))
        {
            surface = new MeshRenderSurfaceData(materialSlot);
            _surfacesBySlot.Add(materialSlot, surface);
        }

        if (!surface.IsActive)
        {
            surface.IsActive = true;
            _activeSurfaces.Add(surface);
        }

        return surface.Data;
    }

    public void Clear()
    {
        foreach (MeshRenderSurfaceData surface in _activeSurfaces)
        {
            surface.Data.Clear();
            surface.IsActive = false;
        }

        _activeSurfaces.Clear();
    }
}

public sealed class MeshRenderSurfaceData
{
    internal MeshRenderSurfaceData(int materialSlot)
    {
        MaterialSlot = materialSlot;
    }

    public int MaterialSlot { get; }

    public MeshRenderData Data { get; } = new();

    internal bool IsActive { get; set; }
}

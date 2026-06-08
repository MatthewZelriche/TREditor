using System.Collections.Generic;
using Godot;

/// <summary>
/// Caller-owned scratch data for one render surface.
/// </summary>
public sealed class MeshRenderData
{
    public List<Vector3> Vertices { get; } = [];

    public List<Vector3> Normals { get; } = [];

    public List<Vector2> Uvs { get; } = [];

    public List<int> Indices { get; } = [];

    public void Clear()
    {
        Vertices.Clear();
        Normals.Clear();
        Uvs.Clear();
        Indices.Clear();
    }
}

using System.Collections.Generic;
using System.Numerics;

public sealed class MeshSurface
{
    public List<Vector3> Vertices { get; } = [];
    public List<Vector3> Normals { get; } = [];
    public List<Vector2> Uvs { get; } = [];
    public List<int> ElementIndices { get; } = [];
    public List<int> FaceSlots { get; } = [];
    public int SkippedFaceCount { get; internal set; }
}

using System.Collections.Generic;
using System.Numerics;
using TRMesh.Mesh;

internal sealed class GDMeshSurfaceBuilder
{
    readonly List<int> _triangulatedHalfEdges = [];
    readonly Dictionary<int, int> _vertexIndices = [];

    public List<Vector3> Vertices { get; } = [];
    public List<Vector3> Normals { get; } = [];
    public List<Vector2> Uvs { get; } = [];
    public List<int> Indices { get; } = [];
    public int SkippedFaceCount { get; private set; }

    public void Build(SpatialMesh mesh)
    {
        Clear();

        foreach (var face in mesh.Faces)
        {
            _triangulatedHalfEdges.Clear();
            if (!mesh.TriangulateFace(face.Slot, _triangulatedHalfEdges))
            {
                SkippedFaceCount++;
                continue;
            }

            Vector3 normal = face.normal;
            for (int i = 0; i < _triangulatedHalfEdges.Count; i += 3)
            {
                // TRMesh emits CCW triangles. Godot considers clockwise triangles front-facing.
                AppendIndex(mesh, _triangulatedHalfEdges[i], normal);
                AppendIndex(mesh, _triangulatedHalfEdges[i + 2], normal);
                AppendIndex(mesh, _triangulatedHalfEdges[i + 1], normal);
            }
        }
    }

    void AppendIndex(SpatialMesh mesh, int halfEdge, Vector3 faceNormal)
    {
        if (!_vertexIndices.TryGetValue(halfEdge, out int vertexIndex))
        {
            vertexIndex = Vertices.Count;
            _vertexIndices.Add(halfEdge, vertexIndex);
            var corner = mesh.GetHalfEdgeData(halfEdge);
            int vertex = corner.topology.SourceVertex;
            Vertices.Add(mesh.GetVertexData(vertex).position);
            Normals.Add(faceNormal);
            Uvs.Add(corner.UV);
        }

        Indices.Add(vertexIndex);
    }

    void Clear()
    {
        _triangulatedHalfEdges.Clear();
        _vertexIndices.Clear();
        Vertices.Clear();
        Normals.Clear();
        Uvs.Clear();
        Indices.Clear();
        SkippedFaceCount = 0;
    }
}

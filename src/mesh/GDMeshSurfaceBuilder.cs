using System.Collections.Generic;
using System.Numerics;
using TRMesh.Mesh;

internal sealed class GDMeshSurfaceBuilder
{
    readonly List<int> _triangulatedHalfEdges = [];
    readonly Dictionary<int, int> _vertexIndices = [];

    public MeshSurface Build(SpatialMesh mesh)
    {
        Clear();
        var surface = new MeshSurface();

        foreach (var face in mesh.Faces)
        {
            _triangulatedHalfEdges.Clear();
            if (!mesh.TriangulateFace(face.Slot, _triangulatedHalfEdges))
            {
                surface.SkippedFaceCount++;
                continue;
            }

            Vector3 normal = face.normal;
            for (int i = 0; i < _triangulatedHalfEdges.Count; i += 3)
            {
                // TRMesh emits CCW triangles. Godot considers clockwise triangles front-facing.
                AppendIndex(mesh, surface, _triangulatedHalfEdges[i], normal);
                AppendIndex(mesh, surface, _triangulatedHalfEdges[i + 2], normal);
                AppendIndex(mesh, surface, _triangulatedHalfEdges[i + 1], normal);
                surface.FaceSlots.Add(face.Slot);
            }
        }

        return surface;
    }

    void AppendIndex(SpatialMesh mesh, MeshSurface surface, int halfEdge, Vector3 faceNormal)
    {
        if (!_vertexIndices.TryGetValue(halfEdge, out int vertexIndex))
        {
            vertexIndex = surface.Vertices.Count;
            _vertexIndices.Add(halfEdge, vertexIndex);
            var corner = mesh.GetHalfEdgeData(halfEdge);
            int vertex = corner.topology.SourceVertex;
            surface.Vertices.Add(mesh.GetVertexData(vertex).position);
            surface.Normals.Add(faceNormal);
            surface.Uvs.Add(corner.UV);
        }

        surface.ElementIndices.Add(vertexIndex);
    }

    void Clear()
    {
        _triangulatedHalfEdges.Clear();
        _vertexIndices.Clear();
    }
}

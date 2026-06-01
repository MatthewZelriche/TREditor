using System;
using System.Collections.Generic;
using Godot;
using TREditorSharp;
using GodotArray = Godot.Collections.Array;
using NumericsVector3 = System.Numerics.Vector3;

public partial class MeshRenderable : MeshInstance3D
{
    public SpatialMesh SourceMesh { get; private set; } = new();

    // Can't have parameter constructors for Godot, so this is like a re-usable constructor.
    public void TakeMesh(SpatialMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (!ReferenceEquals(SourceMesh, mesh))
        {
            // MeshRenderable owns its SpatialMesh so callers can hand off generated meshes cleanly.
            SourceMesh.Dispose();
            SourceMesh = mesh;
        }

        RebuildRenderableMesh();
    }

    public void RebuildRenderableMesh()
    {
        var indices = new List<int>();
        var faceIndices = new List<int>();
        int highestDenseIndex = -1;

        foreach (var face in SourceMesh.EnumerateLiveFaces())
        {
            faceIndices.Clear();

            if (!SourceMesh.TriangulateFace(face, faceIndices))
            {
                GD.PushWarning($"MeshRenderable skipped face {face}: triangulation failed.");
                continue;
            }

            for (int i = 0; i < faceIndices.Count; i += 3)
            {
                int a = faceIndices[i];
                int b = faceIndices[i + 1];
                int c = faceIndices[i + 2];

                // TRMesh stores outward faces CCW; Godot expects the opposite winding here.
                indices.Add(a);
                indices.Add(c);
                indices.Add(b);
                highestDenseIndex = Math.Max(highestDenseIndex, Math.Max(a, Math.Max(b, c)));
            }
        }

        if (indices.Count == 0)
        {
            Mesh = null;
            return;
        }

        // TriangulateFace returns dense vertex indices, so build the Godot arrays in dense order.
        // TODO: Consider copying over the position data directly to avoid intermediate allocations.
        // TODO: Consider hoisting normal computation into TRMesh library.
        int vertexCount = highestDenseIndex + 1;
        var vertices = new Vector3[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            vertices[i] = ToGodotVector3(SourceMesh.GetVertexPositionByDenseIndex(i));
        }

        // TODO: This ToArray is costly.
        int[] indexArray = [.. indices];
        Vector3[] normals = CalculateVertexNormals(vertices, indexArray);

        var meshArrays = new GodotArray();
        meshArrays.Resize((int)Godot.Mesh.ArrayType.Max);
        meshArrays[(int)Godot.Mesh.ArrayType.Vertex] = vertices;
        meshArrays[(int)Godot.Mesh.ArrayType.Normal] = normals;
        meshArrays[(int)Godot.Mesh.ArrayType.Index] = indexArray;

        var renderMesh = new ArrayMesh();
        renderMesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, meshArrays);
        Mesh = renderMesh;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            SourceMesh.Dispose();
        }
    }

    private static Vector3 ToGodotVector3(NumericsVector3 vector) =>
        new(vector.X, vector.Y, vector.Z);

    private static Vector3[] CalculateVertexNormals(Vector3[] vertices, int[] indices)
    {
        var normals = new Vector3[vertices.Length];

        for (int i = 0; i < indices.Length; i += 3)
        {
            int aIndex = indices[i];
            int bIndex = indices[i + 1];
            int cIndex = indices[i + 2];

            Vector3 a = vertices[aIndex];
            Vector3 b = vertices[bIndex];
            Vector3 c = vertices[cIndex];
            Vector3 triangleNormal = (b - a).Cross(c - a);

            normals[aIndex] += triangleNormal;
            normals[bIndex] += triangleNormal;
            normals[cIndex] += triangleNormal;
        }

        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = normals[i].LengthSquared() > 0.0f ? normals[i].Normalized() : Vector3.Up;
        }

        return normals;
    }
}

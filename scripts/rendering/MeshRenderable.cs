using System;
using System.Collections.Generic;
using Godot;
using TREditorSharp;
using GodotArray = Godot.Collections.Array;
using NumericsVector3 = System.Numerics.Vector3;

public partial class MeshRenderable : MeshInstance3D
{
    private const string DefaultMaterialPath = "res://resource/matcap_material.tres";
    private static Material _defaultMaterial;

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
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var indices = new List<int>();
        var faceIndices = new List<int>();

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
                AddRenderTriangle(vertices, normals, indices, a, c, b);
            }
        }

        if (indices.Count == 0)
        {
            Mesh = null;
            return;
        }

        var meshArrays = new GodotArray();
        meshArrays.Resize((int)Godot.Mesh.ArrayType.Max);
        meshArrays[(int)Godot.Mesh.ArrayType.Vertex] = vertices.ToArray();
        meshArrays[(int)Godot.Mesh.ArrayType.Normal] = normals.ToArray();
        meshArrays[(int)Godot.Mesh.ArrayType.Index] = indices.ToArray();

        var renderMesh = new ArrayMesh();
        renderMesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, meshArrays);
        Mesh = renderMesh;
        ApplyDefaultMaterialIfNeeded();
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

    private void AddRenderTriangle(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<int> indices,
        int aIndex,
        int bIndex,
        int cIndex
    )
    {
        Vector3 a = ToGodotVector3(SourceMesh.GetVertexPositionByDenseIndex(aIndex));
        Vector3 b = ToGodotVector3(SourceMesh.GetVertexPositionByDenseIndex(bIndex));
        Vector3 c = ToGodotVector3(SourceMesh.GetVertexPositionByDenseIndex(cIndex));
        Vector3 normal = CalculateTriangleNormal(a, b, c);
        int firstRenderIndex = vertices.Count;

        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);
        indices.Add(firstRenderIndex);
        indices.Add(firstRenderIndex + 1);
        indices.Add(firstRenderIndex + 2);
    }

    private static Vector3 CalculateTriangleNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 normal = (b - a).Cross(c - a);
        return normal.LengthSquared() > 0.0f ? normal.Normalized() : Vector3.Up;
    }

    private void ApplyDefaultMaterialIfNeeded()
    {
        if (MaterialOverride != null)
        {
            return;
        }

        _defaultMaterial ??= ResourceLoader.Load<Material>(DefaultMaterialPath);
        if (_defaultMaterial == null)
        {
            GD.PushWarning(
                $"MeshRenderable could not load default material: {DefaultMaterialPath}"
            );
            return;
        }

        MaterialOverride = _defaultMaterial;
    }

}

using System;
using System.Collections.Generic;
using Godot;
using TREditorSharp;
using GodotArray = Godot.Collections.Array;
using NumericsVector3 = System.Numerics.Vector3;

public partial class MeshRenderable : MeshInstance3D
{
    private const string DefaultMaterialPath = "res://resource/matcap_material.tres";
    private const string SelectionMaterialPath =
        "res://resource/matcap_stencil_write_material.tres";
    private static Material _defaultMaterial;
    private static Material _selectionMaterial;

    private bool _isSelected;

    /// <summary>
    /// Appends one render triangle into caller-owned rebuild scratch lists
    /// (e.g. <see cref="TRMeshGD"/> scratch buffers). Does not update this node.
    /// </summary>
    public static void AppendRebuildTriangle(
        SpatialMesh sourceMesh,
        List<Vector3> vertices,
        List<Vector3> normals,
        List<int> indices,
        int aIndex,
        int bIndex,
        int cIndex
    )
    {
        ArgumentNullException.ThrowIfNull(sourceMesh);

        Vector3 a = ToGodotVector3(sourceMesh.GetVertexPositionByDenseIndex(aIndex));
        Vector3 b = ToGodotVector3(sourceMesh.GetVertexPositionByDenseIndex(bIndex));
        Vector3 c = ToGodotVector3(sourceMesh.GetVertexPositionByDenseIndex(cIndex));
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

    /// <summary>
    /// Commits filled rebuild scratch lists to this node's <see cref="MeshInstance3D.Mesh"/>.
    /// </summary>
    public void Rebuild(List<Vector3> vertices, List<Vector3> normals, List<int> indices)
    {
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
        ApplyMaterial();
    }

    public void SetSelected(bool selected)
    {
        if (_isSelected == selected)
        {
            return;
        }

        _isSelected = selected;
        ApplyMaterial();
    }

    private static Vector3 ToGodotVector3(NumericsVector3 vector) =>
        new(vector.X, vector.Y, vector.Z);

    private static Vector3 CalculateTriangleNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 normal = (b - a).Cross(c - a);
        return normal.LengthSquared() > 0.0f ? normal.Normalized() : Vector3.Up;
    }

    private void ApplyMaterial()
    {
        if (_isSelected)
        {
            _selectionMaterial ??= ResourceLoader.Load<Material>(SelectionMaterialPath);
            if (_selectionMaterial == null)
            {
                GD.PushWarning(
                    $"MeshRenderable could not load selection material: {SelectionMaterialPath}"
                );
                return;
            }

            MaterialOverride = _selectionMaterial;
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

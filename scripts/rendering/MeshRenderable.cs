using System;
using Godot;
using GodotArray = Godot.Collections.Array;

public partial class MeshRenderable : MeshInstance3D
{
    private const string DefaultMaterialPath = "res://resource/matcap_material.tres";
    private const string SelectionMaterialPath =
        "res://resource/matcap_stencil_write_material.tres";
    private static Material _defaultMaterial;
    private static Material _selectionMaterial;

    private bool _isSelected;

    /// <summary>
    /// Commits filled render data to this node's <see cref="MeshInstance3D.Mesh"/>.
    /// </summary>
    public void Rebuild(MeshRenderData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Indices.Count == 0)
        {
            Mesh = null;
            return;
        }

        var meshArrays = new GodotArray();
        meshArrays.Resize((int)Godot.Mesh.ArrayType.Max);
        meshArrays[(int)Godot.Mesh.ArrayType.Vertex] = data.Vertices.ToArray();
        meshArrays[(int)Godot.Mesh.ArrayType.Normal] = data.Normals.ToArray();
        meshArrays[(int)Godot.Mesh.ArrayType.TexUV] = data.Uvs.ToArray();
        meshArrays[(int)Godot.Mesh.ArrayType.Index] = data.Indices.ToArray();

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

        if (HasCustomMaterialOverride())
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

    private bool HasCustomMaterialOverride() =>
        MaterialOverride != null
        && !ReferenceEquals(MaterialOverride, _defaultMaterial)
        && !ReferenceEquals(MaterialOverride, _selectionMaterial);
}

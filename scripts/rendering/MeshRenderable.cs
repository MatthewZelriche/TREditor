using System;
using System.Collections.Generic;
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
    private bool _usesSurfaceMaterials;

    /// <summary>
    /// Commits filled render data to this node's <see cref="MeshInstance3D.Mesh"/>.
    /// </summary>
    public void Rebuild(MeshRenderData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _usesSurfaceMaterials = false;

        if (data.Indices.Count == 0)
        {
            Mesh = null;
            return;
        }

        var renderMesh = new ArrayMesh();
        renderMesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, BuildMeshArrays(data));
        Mesh = renderMesh;
        ApplyMaterial();
    }

    /// <summary>
    /// Commits one Godot surface per active material slot.
    /// </summary>
    public void Rebuild(MeshRenderSurfaceSet surfaces, TextureMaterialLibrary textureMaterials)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        ArgumentNullException.ThrowIfNull(textureMaterials);
        _usesSurfaceMaterials = true;

        if (surfaces.ActiveSurfaces.Count == 0)
        {
            Mesh = null;
            ApplyMaterial();
            return;
        }

        var renderMesh = new ArrayMesh();
        int surfaceIndex = 0;
        foreach (MeshRenderSurfaceData surface in surfaces.ActiveSurfaces)
        {
            renderMesh.AddSurfaceFromArrays(
                Godot.Mesh.PrimitiveType.Triangles,
                BuildMeshArrays(surface.Data)
            );
            renderMesh.SurfaceSetMaterial(
                surfaceIndex,
                ResolveSurfaceMaterial(surface.MaterialSlot, textureMaterials)
            );
            surfaceIndex++;
        }

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

        if (_usesSurfaceMaterials)
        {
            MaterialOverride = null;
            return;
        }

        if (HasCustomMaterialOverride())
        {
            return;
        }

        MaterialOverride = GetDefaultMaterial();
    }

    private static GodotArray BuildMeshArrays(MeshRenderData data)
    {
        var meshArrays = new GodotArray();
        meshArrays.Resize((int)Godot.Mesh.ArrayType.Max);
        meshArrays[(int)Godot.Mesh.ArrayType.Vertex] = data.Vertices.ToArray();
        meshArrays[(int)Godot.Mesh.ArrayType.Normal] = data.Normals.ToArray();
        meshArrays[(int)Godot.Mesh.ArrayType.TexUV] = data.Uvs.ToArray();
        meshArrays[(int)Godot.Mesh.ArrayType.Index] = data.Indices.ToArray();
        return meshArrays;
    }

    private static Material ResolveSurfaceMaterial(
        int materialSlot,
        TextureMaterialLibrary textureMaterials
    )
    {
        if (materialSlot == 0)
            return GetDefaultMaterial();

        try
        {
            return textureMaterials.ResolveMaterial(materialSlot);
        }
        catch (KeyNotFoundException exception)
        {
            GD.PushWarning(exception.Message);
            return GetDefaultMaterial();
        }
    }

    private static Material GetDefaultMaterial()
    {
        _defaultMaterial ??= ResourceLoader.Load<Material>(DefaultMaterialPath);
        if (_defaultMaterial == null)
            GD.PushWarning(
                $"MeshRenderable could not load default material: {DefaultMaterialPath}"
            );
        return _defaultMaterial;
    }

    private bool HasCustomMaterialOverride() =>
        MaterialOverride != null
        && !ReferenceEquals(MaterialOverride, _defaultMaterial)
        && !ReferenceEquals(MaterialOverride, _selectionMaterial);
}

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

    private readonly List<Material> _surfaceMaterials = [];
    private readonly Dictionary<Material, Material> _selectedSurfaceMaterials = [];
    private bool _isSelected;
    private bool _usesSurfaceMaterials;

    /// <summary>
    /// Commits filled render data to this node's <see cref="MeshInstance3D.Mesh"/>.
    /// </summary>
    public void Rebuild(MeshRenderData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _usesSurfaceMaterials = false;
        _surfaceMaterials.Clear();

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
        _surfaceMaterials.Clear();

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
            Material material = ResolveSurfaceMaterial(surface.MaterialSlot, textureMaterials);
            _surfaceMaterials.Add(material);
            renderMesh.SurfaceSetMaterial(surfaceIndex, material);
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
        if (_usesSurfaceMaterials)
        {
            // A MaterialOverride would replace every face material and hide textures. Swap each
            // surface to a stencil-writing visual equivalent instead, preserving its appearance.
            MaterialOverride = null;
            ApplySurfaceSelectionMaterials();
            return;
        }

        if (_isSelected)
        {
            MaterialOverride = GetSelectionMaterial();
            return;
        }

        if (HasCustomMaterialOverride())
        {
            return;
        }

        MaterialOverride = GetDefaultMaterial();
    }

    private void ApplySurfaceSelectionMaterials()
    {
        if (Mesh is not ArrayMesh renderMesh)
            return;

        for (int surfaceIndex = 0; surfaceIndex < _surfaceMaterials.Count; surfaceIndex++)
        {
            Material material = _surfaceMaterials[surfaceIndex];
            renderMesh.SurfaceSetMaterial(
                surfaceIndex,
                _isSelected ? GetSelectedSurfaceMaterial(material) : material
            );
        }
    }

    private Material GetSelectedSurfaceMaterial(Material material)
    {
        if (_selectedSurfaceMaterials.TryGetValue(material, out Material selected))
            return selected;

        if (material is BaseMaterial3D baseMaterial)
        {
            var selectedBaseMaterial = (BaseMaterial3D)baseMaterial.Duplicate();
            selectedBaseMaterial.StencilMode = BaseMaterial3D.StencilModeEnum.Custom;
            selectedBaseMaterial.StencilFlags = (int)BaseMaterial3D.StencilFlagsEnum.Write;
            selectedBaseMaterial.StencilCompare = BaseMaterial3D.StencilCompareEnum.Always;
            selectedBaseMaterial.StencilReference = 1;
            selected = selectedBaseMaterial;
        }
        else
        {
            // The current non-BaseMaterial3D surface is the default matcap ShaderMaterial.
            selected = GetSelectionMaterial();
        }

        _selectedSurfaceMaterials.Add(material, selected);
        return selected;
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

    private static Material GetSelectionMaterial()
    {
        _selectionMaterial ??= ResourceLoader.Load<Material>(SelectionMaterialPath);
        if (_selectionMaterial == null)
            GD.PushWarning(
                $"MeshRenderable could not load selection material: {SelectionMaterialPath}"
            );
        return _selectionMaterial;
    }

    private bool HasCustomMaterialOverride() =>
        MaterialOverride != null
        && !ReferenceEquals(MaterialOverride, _defaultMaterial)
        && !ReferenceEquals(MaterialOverride, _selectionMaterial);
}

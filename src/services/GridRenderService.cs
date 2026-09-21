using System;
using System.Collections.Generic;
using Godot;

// Owns one isolated analytical grid plane for each editor viewport. All instances share immutable
// geometry and material resources; the shader receives the rendering viewport's camera matrices.
internal sealed class GridRenderService : ISceneService
{
    public const string ShaderPath = "res://src/shaders/pristine_major_minor_grid.gdshader";
    public const float LodLevelCount = 8.0f;
    public const float MinorLineWidth = 0.01f;
    public const float MajorLineWidth = 0.02f;
    public const float AxisLineWidth = 0.04f;

    private const float WorldExtent = 1.0e12f;

    private readonly Dictionary<Camera3D, MeshInstance3D> _instances = [];
    private readonly QuadMesh _mesh;
    private readonly ShaderMaterial _material;
    private bool _disposed;

    public GridRenderService()
    {
        var shader = GD.Load<Shader>(ShaderPath)
            ?? throw new InvalidOperationException($"Failed to load grid shader at '{ShaderPath}'.");
        _material = new ShaderMaterial
        {
            Shader = shader,
            // The grid is background geometry and should be composited before tool previews.
            RenderPriority = -100,
        };
        _material.SetShaderParameter("plane_height", 0.0f);
        _material.SetShaderParameter("lod_level_count", LodLevelCount);
        _material.SetShaderParameter("minor_line_width", MinorLineWidth);
        _material.SetShaderParameter("major_line_width", MajorLineWidth);
        _material.SetShaderParameter("axis_line_width", AxisLineWidth);
        _mesh = new QuadMesh
        {
            Size = new Vector2(2.0f, 2.0f),
            FlipFaces = true,
        };
    }

    public int ViewportCount => _instances.Count;

    public void RegisterViewport(Camera3D camera, Node3D overlayRoot, uint layerMask)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(overlayRoot);
        if (_instances.ContainsKey(camera))
            return;

        if (layerMask == 0u || (layerMask & (layerMask - 1u)) != 0u)
            throw new ArgumentException("A viewport grid requires one render layer.", nameof(layerMask));

        var instance = new MeshInstance3D
        {
            Name = "InfiniteGrid",
            Mesh = _mesh,
            MaterialOverride = _material,
            Layers = layerMask,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            IgnoreOcclusionCulling = true,
            ExtraCullMargin = 16_384.0f,
            // The vertex shader expands this carrier quad to clip space. Keep its CPU-side bounds
            // relevant even when a viewport camera has travelled far from the world origin.
            CustomAabb = new Aabb(
                new Vector3(-WorldExtent, -WorldExtent, -WorldExtent),
                new Vector3(WorldExtent * 2.0f, WorldExtent * 2.0f, WorldExtent * 2.0f)
            ),
        };

        try
        {
            overlayRoot.AddChild(instance);
            _instances.Add(camera, instance);
        }
        catch
        {
            ReleaseInstance(instance);
            throw;
        }
    }

    public void UnregisterViewport(Camera3D camera)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(camera);
        if (_instances.Remove(camera, out MeshInstance3D? instance))
            ReleaseInstance(instance);
    }

    public void SetDocument(Document document)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(document);
        // The grid represents the coordinate system, so it remains fixed when the document changes.
    }

    public void OnDocumentChanged(DocumentChangeSet changeSet)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(changeSet);
    }

    public void OnPreviewTransformChanged(MeshObjectId id) =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    public void Flush() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (MeshInstance3D instance in _instances.Values)
            ReleaseInstance(instance);

        _instances.Clear();
        _mesh.Dispose();
        _material.Dispose();
    }

    private static void ReleaseInstance(MeshInstance3D instance)
    {
        if (!GodotObject.IsInstanceValid(instance))
            return;

        instance.MaterialOverride = null;
        instance.Mesh = null;
        if (instance.GetParent() is Node parent)
            parent.RemoveChild(instance);

        instance.Free();
    }
}

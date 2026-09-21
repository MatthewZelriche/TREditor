using System;
using Godot;

// Owns the one analytical grid plane shared by every editor viewport and loaded document.
internal sealed class GridRenderService : ISceneService
{
    public const string ShaderPath = "res://src/shaders/pristine_major_minor_grid.gdshader";

    private const float WorldExtent = 1.0e12f;

    private readonly MeshInstance3D _instance;
    private readonly QuadMesh _mesh;
    private readonly ShaderMaterial _material;
    private bool _disposed;

    public GridRenderService(Node3D mapRoot)
    {
        ArgumentNullException.ThrowIfNull(mapRoot);

        var shader = GD.Load<Shader>(ShaderPath)
            ?? throw new InvalidOperationException($"Failed to load grid shader at '{ShaderPath}'.");
        _material = new ShaderMaterial
        {
            Shader = shader,
            // The grid is background geometry and should be composited before tool previews.
            RenderPriority = -100,
        };
        _material.SetShaderParameter("plane_height", 0.0f);
        _mesh = new QuadMesh
        {
            Size = new Vector2(2.0f, 2.0f),
            FlipFaces = true,
        };
        _instance = new MeshInstance3D
        {
            Name = "InfiniteGrid",
            Mesh = _mesh,
            MaterialOverride = _material,
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
            mapRoot.AddChild(_instance);
        }
        catch
        {
            DisposeResources();
            throw;
        }
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
        DisposeResources();
    }

    private void DisposeResources()
    {
        _instance.MaterialOverride = null;
        _instance.Mesh = null;
        _instance.Free();
        _mesh.Dispose();
        _material.Dispose();
    }
}

using System;
using System.Collections.Generic;
using Godot;

// RenderService-owned renderer for transient preview geometry. It owns every Godot resource and
// node used by the preview, while RenderService only forwards preview-state changes to it.
internal sealed class PreviewGeometryRenderer : IDisposable
{
    private readonly Node3D _renderRoot;
    private readonly Node3D _previewRoot = new() { Name = "PreviewGeometry" };
    private readonly ArrayMesh _fillMesh = new();
    private readonly ArrayMesh _wireMesh = new();
    private readonly StandardMaterial3D _fillMaterial;
    private readonly StandardMaterial3D _wireMaterial;
    private readonly MeshInstance3D _fillInstance;
    private readonly MeshInstance3D _wireInstance;
    private bool _disposed;

    public PreviewGeometryRenderer(Node3D renderRoot)
    {
        ArgumentNullException.ThrowIfNull(renderRoot);
        _renderRoot = renderRoot;
        _fillMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.055f, 0.065f, 0.085f, 0.58f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            DisableFog = true,
        };
        _wireMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.82f, 0.12f, 1.0f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            NoDepthTest = true,
            DisableFog = true,
        };
        _fillInstance = new MeshInstance3D
        {
            Name = "Fill",
            Mesh = _fillMesh,
            MaterialOverride = _fillMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _wireInstance = new MeshInstance3D
        {
            Name = "Wire",
            Mesh = _wireMesh,
            MaterialOverride = _wireMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };

        try
        {
            _previewRoot.AddChild(_fillInstance);
            _previewRoot.AddChild(_wireInstance);
        }
        catch
        {
            DisposeResources();
            throw;
        }
    }

    public void Update(GeometryPreview? preview)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (preview is null)
        {
            Clear();
            return;
        }

        preview.Mesh.RebuildSurfaceIfDirty();
        RenderService.UpdateArrayMesh(_fillMesh, preview.Mesh.Surface);
        UpdateWireMesh(_wireMesh, preview.Mesh.SpatialMesh);
        _previewRoot.Transform = preview.Transform;
        if (_previewRoot.GetParent() is null)
            _renderRoot.AddChild(_previewRoot);
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _fillMesh.ClearSurfaces();
        _wireMesh.ClearSurfaces();
        if (_previewRoot.GetParent() is not null)
            _renderRoot.RemoveChild(_previewRoot);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Clear();
        _disposed = true;
        DisposeResources();
    }

    // TRMesh stores each topological edge as a twin half-edge pair. Emitting one pair at a time
    // gives the preview a true cage outline without exposing the triangles used to render n-gons.
    private static void UpdateWireMesh(ArrayMesh arrayMesh, TRMesh.Mesh.SpatialMesh mesh)
    {
        Vector3[] vertices = CreateWireVertices(mesh);
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;

        arrayMesh.ClearSurfaces();
        if (vertices.Length != 0)
            arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
    }

    internal static Vector3[] CreateWireVertices(TRMesh.Mesh.SpatialMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var vertices = new List<Vector3>(checked(mesh.EdgeCount * 2));
        mesh.WithHalfEdges((slots, topology, _) =>
        {
            for (int i = 0; i < slots.Length; i++)
            {
                TRMesh.Mesh.HalfEdge halfEdge = topology[i];
                if (slots[i] > halfEdge.TwinHalfEdge)
                    continue;

                System.Numerics.Vector3 source = mesh.GetVertexData(halfEdge.SourceVertex).position;
                int targetVertex = mesh.GetHalfEdgeData(halfEdge.TwinHalfEdge).topology.SourceVertex;
                System.Numerics.Vector3 target = mesh.GetVertexData(targetVertex).position;
                vertices.Add(new Vector3(source.X, source.Y, source.Z));
                vertices.Add(new Vector3(target.X, target.Y, target.Z));
            }
        });

        return vertices.ToArray();
    }

    private void DisposeResources()
    {
        _wireInstance.MaterialOverride = null;
        _wireInstance.Mesh = null;
        if (_wireInstance.GetParent() is null)
            _wireInstance.Free();

        _fillInstance.MaterialOverride = null;
        _fillInstance.Mesh = null;
        if (_fillInstance.GetParent() is null)
            _fillInstance.Free();

        _previewRoot.Free();
        _wireMaterial.Dispose();
        _fillMaterial.Dispose();
        _wireMesh.Dispose();
        _fillMesh.Dispose();
    }
}

using System;
using System.Collections.Generic;
using Godot;
using TREditorSharp;
using GodotArray = Godot.Collections.Array;

// TODO: We start from scratch every UpdatePreview, which is...a lot. That being said, primitives are
// very simple so it may never become an issue. But this is a performance consideration to be
// aware of for the future.
// TODO: Consider factoring out some common functionality between this and the TRMeshGD node.
public partial class PrimitiveCreationPreview : Node3D
{
    private const float WireRadius = 0.025f;
    private const int WireSegments = 8;

    private static StandardMaterial3D _faceMaterial;
    private static StandardMaterial3D _wireMaterial;

    private readonly List<Vector3> _faceVertices = [];
    private readonly List<Vector3> _faceNormals = [];
    private readonly List<int> _faceIndices = [];
    private readonly List<int> _faceTriangulation = [];
    private readonly List<Vector3> _wireVertices = [];
    private readonly List<Vector3> _wireNormals = [];
    private readonly List<int> _wireIndices = [];

    private MeshRenderable _faces;
    private MeshInstance3D _wire;

    public override void _Ready()
    {
        EnsureChildren();
        Clear();
    }

    public void UpdatePreview(PrimitiveCreationSettings settings, PrimitiveBounds bounds)
    {
        EnsureChildren();

        PrimitiveBounds clampedBounds = bounds.WithMinimumExtents();
        RebuildFaces(settings, clampedBounds);
        RebuildWire(settings, clampedBounds);
        Visible = true;
    }

    public void Clear()
    {
        if (_faces != null)
        {
            _faces.Mesh = null;
        }

        if (_wire != null)
        {
            _wire.Mesh = null;
        }

        Visible = false;
    }

    private void EnsureChildren()
    {
        if (_faces == null)
        {
            _faces = new MeshRenderable { Name = "Faces", MaterialOverride = GetFaceMaterial() };
            AddChild(_faces);
        }

        if (_wire == null)
        {
            _wire = new MeshInstance3D { Name = "Wire", MaterialOverride = GetWireMaterial() };
            AddChild(_wire);
        }
    }

    private void RebuildFaces(PrimitiveCreationSettings settings, PrimitiveBounds bounds)
    {
        ClearFaceScratch();

        using SpatialMesh mesh = PrimitiveMeshFactory.Build(settings, bounds);
        foreach (var face in mesh.EnumerateLiveFaces())
        {
            _faceTriangulation.Clear();

            if (!mesh.TriangulateFace(face, _faceTriangulation))
            {
                GD.PushWarning(
                    $"PrimitiveCreationPreview skipped face {face}: triangulation failed."
                );
                continue;
            }

            for (int i = 0; i < _faceTriangulation.Count; i += 3)
            {
                int a = _faceTriangulation[i];
                int b = _faceTriangulation[i + 1];
                int c = _faceTriangulation[i + 2];

                MeshRenderable.AppendRebuildTriangle(
                    mesh,
                    _faceVertices,
                    _faceNormals,
                    _faceIndices,
                    a,
                    c,
                    b
                );
            }
        }

        _faces.Rebuild(_faceVertices, _faceNormals, _faceIndices);
    }

    private void RebuildWire(PrimitiveCreationSettings settings, PrimitiveBounds bounds)
    {
        ClearWireScratch();

        switch (settings.Kind)
        {
            case PrimitiveKind.Box:
                AddBoxWire(bounds);
                break;
            case PrimitiveKind.Cylinder:
                AddCylinderWire(settings, bounds);
                break;
        }

        var meshArrays = new GodotArray();
        meshArrays.Resize((int)Godot.Mesh.ArrayType.Max);
        meshArrays[(int)Godot.Mesh.ArrayType.Vertex] = _wireVertices.ToArray();
        meshArrays[(int)Godot.Mesh.ArrayType.Normal] = _wireNormals.ToArray();
        meshArrays[(int)Godot.Mesh.ArrayType.Index] = _wireIndices.ToArray();

        var renderMesh = new ArrayMesh();
        renderMesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, meshArrays);
        _wire.Mesh = renderMesh;
    }

    private void AddBoxWire(PrimitiveBounds bounds)
    {
        Vector3 min = bounds.Min;
        Vector3 max = bounds.Max;

        Vector3 c000 = new(min.X, min.Y, min.Z);
        Vector3 c001 = new(min.X, min.Y, max.Z);
        Vector3 c010 = new(min.X, max.Y, min.Z);
        Vector3 c011 = new(min.X, max.Y, max.Z);
        Vector3 c100 = new(max.X, min.Y, min.Z);
        Vector3 c101 = new(max.X, min.Y, max.Z);
        Vector3 c110 = new(max.X, max.Y, min.Z);
        Vector3 c111 = new(max.X, max.Y, max.Z);

        AddTube(c000, c100);
        AddTube(c001, c101);
        AddTube(c010, c110);
        AddTube(c011, c111);
        AddTube(c000, c001);
        AddTube(c100, c101);
        AddTube(c010, c011);
        AddTube(c110, c111);
        AddTube(c000, c010);
        AddTube(c001, c011);
        AddTube(c100, c110);
        AddTube(c101, c111);
    }

    private void AddCylinderWire(PrimitiveCreationSettings settings, PrimitiveBounds bounds)
    {
        int segments = settings.CylinderRadialSegments;
        Vector3 center = bounds.Center;
        Vector3 size = bounds.Size;
        float radiusX = size.X * 0.5f;
        float radiusZ = size.Z * 0.5f;
        float bottomY = bounds.Min.Y;
        float topY = bounds.Max.Y;

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            Vector3 bottom = GetCylinderRingPoint(center, radiusX, radiusZ, bottomY, i, segments);
            Vector3 bottomNext = GetCylinderRingPoint(
                center,
                radiusX,
                radiusZ,
                bottomY,
                next,
                segments
            );
            Vector3 top = GetCylinderRingPoint(center, radiusX, radiusZ, topY, i, segments);
            Vector3 topNext = GetCylinderRingPoint(center, radiusX, radiusZ, topY, next, segments);

            AddTube(bottom, bottomNext);
            AddTube(top, topNext);
            AddTube(bottom, top);
        }
    }

    private void AddTube(Vector3 start, Vector3 end)
    {
        Vector3 axis = end - start;
        if (axis.IsZeroApprox())
        {
            return;
        }

        axis = axis.Normalized();
        Vector3 reference = Mathf.Abs(axis.Dot(Vector3.Up)) > 0.95f ? Vector3.Right : Vector3.Up;
        Vector3 sideA = axis.Cross(reference).Normalized();
        Vector3 sideB = axis.Cross(sideA).Normalized();
        int firstIndex = _wireVertices.Count;

        for (int i = 0; i < WireSegments; i++)
        {
            float angle = Mathf.Tau * i / WireSegments;
            Vector3 normal = sideA * Mathf.Cos(angle) + sideB * Mathf.Sin(angle);

            _wireVertices.Add(start + normal * WireRadius);
            _wireNormals.Add(normal);
            _wireVertices.Add(end + normal * WireRadius);
            _wireNormals.Add(normal);
        }

        for (int i = 0; i < WireSegments; i++)
        {
            int next = (i + 1) % WireSegments;
            int a = firstIndex + i * 2;
            int b = firstIndex + next * 2;
            int c = firstIndex + next * 2 + 1;
            int d = firstIndex + i * 2 + 1;

            _wireIndices.Add(a);
            _wireIndices.Add(b);
            _wireIndices.Add(c);
            _wireIndices.Add(a);
            _wireIndices.Add(c);
            _wireIndices.Add(d);
        }
    }

    private static Vector3 GetCylinderRingPoint(
        Vector3 center,
        float radiusX,
        float radiusZ,
        float y,
        int index,
        int segments
    )
    {
        float angle = Mathf.Tau * index / segments;
        return new Vector3(
            center.X + Mathf.Cos(angle) * radiusX,
            y,
            center.Z + Mathf.Sin(angle) * radiusZ
        );
    }

    private void ClearFaceScratch()
    {
        _faceVertices.Clear();
        _faceNormals.Clear();
        _faceIndices.Clear();
        _faceTriangulation.Clear();
    }

    private void ClearWireScratch()
    {
        _wireVertices.Clear();
        _wireNormals.Clear();
        _wireIndices.Clear();
    }

    private static StandardMaterial3D GetFaceMaterial()
    {
        _faceMaterial ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.75f, 1.0f, 0.22f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        return _faceMaterial;
    }

    private static StandardMaterial3D GetWireMaterial()
    {
        _wireMaterial ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(0.05f, 0.95f, 1.0f, 1.0f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        return _wireMaterial;
    }
}

using System;
using System.Collections.Generic;
using Godot;
using TREditorSharp;
using GodotArray = Godot.Collections.Array;
using NumericsVector3 = System.Numerics.Vector3;

public sealed partial class ComponentSelectionOverlay : Node3D
{
    private const float DefaultVertexScale = 0.0045f;
    private const float ActiveVertexScale = 0.007f;
    private const float DefaultEdgeScale = 0.0018f;
    private const float ActiveEdgeScale = 0.0032f;
    private const float MinVertexRadius = 0.025f;
    private const float MinEdgeRadius = 0.01f;
    private const int EdgeSegments = 8;
    private const float FaceNormalOffset = 0.001f;

    private static StandardMaterial3D _defaultEdgeMaterial;
    private static StandardMaterial3D _activeEdgeMaterial;
    private static StandardMaterial3D _defaultVertexMaterial;
    private static StandardMaterial3D _activeVertexMaterial;
    private static StandardMaterial3D _faceMaterial;

    private readonly List<Vector3> _defaultEdgeVertices = [];
    private readonly List<Vector3> _defaultEdgeNormals = [];
    private readonly List<int> _defaultEdgeIndices = [];
    private readonly List<Vector3> _activeEdgeVertices = [];
    private readonly List<Vector3> _activeEdgeNormals = [];
    private readonly List<int> _activeEdgeIndices = [];
    private readonly List<Vector3> _defaultVertexVertices = [];
    private readonly List<Vector3> _defaultVertexNormals = [];
    private readonly List<int> _defaultVertexIndices = [];
    private readonly List<Vector3> _activeVertexVertices = [];
    private readonly List<Vector3> _activeVertexNormals = [];
    private readonly List<int> _activeVertexIndices = [];
    private readonly List<Vector3> _faceVertices = [];
    private readonly List<Vector3> _faceNormals = [];
    private readonly List<int> _faceIndices = [];
    private readonly List<FaceCornerHandle> _faceTriangulation = [];

    private MeshInstance3D _defaultEdges;
    private MeshInstance3D _activeEdges;
    private MeshInstance3D _defaultVertices;
    private MeshInstance3D _activeVertices;
    private MeshInstance3D _faces;

    public override void _Ready()
    {
        EnsureChildren();
    }

    public void Rebuild(
        TRMeshGD meshNode,
        IReadOnlyList<SelectionTarget> selected,
        SelectionTarget? hover,
        Vector3 cameraOrigin
    )
    {
        ArgumentNullException.ThrowIfNull(meshNode);
        EnsureChildren();
        ClearScratch();

        SpatialMesh mesh = meshNode.SourceMesh;
        Vector3 localCameraOrigin = meshNode.GlobalTransform.AffineInverse() * cameraOrigin;

        AddDefaultComponents(mesh, localCameraOrigin);
        AddActiveComponents(mesh, selected, localCameraOrigin);
        if (hover.HasValue && !ContainsTarget(selected, hover.Value))
        {
            AddActiveComponent(mesh, hover.Value, localCameraOrigin);
        }

        RebuildMesh(_defaultEdges, _defaultEdgeVertices, _defaultEdgeNormals, _defaultEdgeIndices);
        RebuildMesh(_activeEdges, _activeEdgeVertices, _activeEdgeNormals, _activeEdgeIndices);
        RebuildMesh(
            _defaultVertices,
            _defaultVertexVertices,
            _defaultVertexNormals,
            _defaultVertexIndices
        );
        RebuildMesh(
            _activeVertices,
            _activeVertexVertices,
            _activeVertexNormals,
            _activeVertexIndices
        );
        RebuildMesh(_faces, _faceVertices, _faceNormals, _faceIndices);
    }

    private void EnsureChildren()
    {
        _defaultEdges ??= AddMeshInstance("DefaultEdges", GetDefaultEdgeMaterial());
        _activeEdges ??= AddMeshInstance("ActiveEdges", GetActiveEdgeMaterial());
        _defaultVertices ??= AddMeshInstance("DefaultVertices", GetDefaultVertexMaterial());
        _activeVertices ??= AddMeshInstance("ActiveVertices", GetActiveVertexMaterial());
        _faces ??= AddMeshInstance("SelectedFaces", GetFaceMaterial());
    }

    private MeshInstance3D AddMeshInstance(string name, Material material)
    {
        MeshInstance3D instance = new()
        {
            Name = name,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(instance);
        return instance;
    }

    private void AddDefaultComponents(SpatialMesh mesh, Vector3 localCameraOrigin)
    {
        foreach (HalfEdgeHandle edge in mesh.EnumerateLiveHalfEdges())
        {
            HalfEdge halfEdge = mesh.GetHalfEdge(edge);
            if (halfEdge.Twin.IsNull || edge.Index > halfEdge.Twin.Index)
            {
                continue;
            }

            AddEdge(mesh, edge, localCameraOrigin, active: false);
        }

        foreach (VertexHandle vertex in mesh.EnumerateLiveVertices())
        {
            AddVertex(mesh, vertex, localCameraOrigin, active: false);
        }
    }

    private void AddActiveComponents(
        SpatialMesh mesh,
        IReadOnlyList<SelectionTarget> targets,
        Vector3 localCameraOrigin
    )
    {
        foreach (SelectionTarget target in targets)
        {
            AddActiveComponent(mesh, target, localCameraOrigin);
        }
    }

    private void AddActiveComponent(
        SpatialMesh mesh,
        SelectionTarget target,
        Vector3 localCameraOrigin
    )
    {
        switch (target.Kind)
        {
            case ScenePickElementKind.Vertex:
                AddVertex(mesh, target.Vertex, localCameraOrigin, active: true);
                break;
            case ScenePickElementKind.Edge:
                AddEdge(mesh, target.Edge, localCameraOrigin, active: true);
                break;
            case ScenePickElementKind.Face:
                AddFace(mesh, target.Face);
                break;
        }
    }

    private void AddVertex(
        SpatialMesh mesh,
        VertexHandle vertex,
        Vector3 localCameraOrigin,
        bool active
    )
    {
        if (vertex.IsNull)
        {
            return;
        }

        Vector3 position = ToGodotVector3(mesh.GetVertexPosition(vertex));
        float radius = GetRadius(
            position,
            localCameraOrigin,
            active ? ActiveVertexScale : DefaultVertexScale
        );
        radius = Mathf.Max(radius, MinVertexRadius);
        AddOctahedron(position, radius, active);
    }

    private void AddEdge(
        SpatialMesh mesh,
        HalfEdgeHandle edge,
        Vector3 localCameraOrigin,
        bool active
    )
    {
        if (edge.IsNull)
        {
            return;
        }

        HalfEdge halfEdge = mesh.GetHalfEdge(edge);
        if (halfEdge.Twin.IsNull)
        {
            return;
        }

        HalfEdge twin = mesh.GetHalfEdge(halfEdge.Twin);
        Vector3 start = ToGodotVector3(mesh.GetVertexPosition(halfEdge.Origin));
        Vector3 end = ToGodotVector3(mesh.GetVertexPosition(twin.Origin));
        float radius = GetRadius(
            (start + end) * 0.5f,
            localCameraOrigin,
            active ? ActiveEdgeScale : DefaultEdgeScale
        );
        radius = Mathf.Max(radius, MinEdgeRadius);
        AddTube(start, end, radius, active);
    }

    private void AddFace(SpatialMesh mesh, FaceHandle face)
    {
        if (face.IsNull)
        {
            return;
        }

        _faceTriangulation.Clear();
        if (!mesh.TriangulateFace(face, _faceTriangulation))
        {
            return;
        }

        for (int i = 0; i < _faceTriangulation.Count; i += 3)
        {
            AddFaceTriangle(
                GetCornerPosition(mesh, _faceTriangulation[i]),
                GetCornerPosition(mesh, _faceTriangulation[i + 1]),
                GetCornerPosition(mesh, _faceTriangulation[i + 2])
            );
        }
    }

    private void AddFaceTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 outwardNormal = CalculateTriangleNormal(a, b, c);
        Vector3 offset = outwardNormal * FaceNormalOffset;
        Vector3 renderNormal = -outwardNormal;
        int firstIndex = _faceVertices.Count;

        _faceVertices.Add(a + offset);
        _faceVertices.Add(c + offset);
        _faceVertices.Add(b + offset);
        _faceNormals.Add(renderNormal);
        _faceNormals.Add(renderNormal);
        _faceNormals.Add(renderNormal);
        _faceIndices.Add(firstIndex);
        _faceIndices.Add(firstIndex + 1);
        _faceIndices.Add(firstIndex + 2);
    }

    private void AddTube(Vector3 start, Vector3 end, float radius, bool active)
    {
        Vector3 axis = end - start;
        if (axis.IsZeroApprox())
        {
            return;
        }

        List<Vector3> vertices = active ? _activeEdgeVertices : _defaultEdgeVertices;
        List<Vector3> normals = active ? _activeEdgeNormals : _defaultEdgeNormals;
        List<int> indices = active ? _activeEdgeIndices : _defaultEdgeIndices;

        axis = axis.Normalized();
        Vector3 reference = Mathf.Abs(axis.Dot(Vector3.Up)) > 0.95f ? Vector3.Right : Vector3.Up;
        Vector3 sideA = axis.Cross(reference).Normalized();
        Vector3 sideB = axis.Cross(sideA).Normalized();
        int firstIndex = vertices.Count;

        for (int i = 0; i < EdgeSegments; i++)
        {
            float angle = Mathf.Tau * i / EdgeSegments;
            Vector3 normal = sideA * Mathf.Cos(angle) + sideB * Mathf.Sin(angle);

            vertices.Add(start + normal * radius);
            normals.Add(normal);
            vertices.Add(end + normal * radius);
            normals.Add(normal);
        }

        for (int i = 0; i < EdgeSegments; i++)
        {
            int next = (i + 1) % EdgeSegments;
            int a = firstIndex + i * 2;
            int b = firstIndex + next * 2;
            int c = firstIndex + next * 2 + 1;
            int d = firstIndex + i * 2 + 1;

            indices.Add(a);
            indices.Add(c);
            indices.Add(b);
            indices.Add(a);
            indices.Add(d);
            indices.Add(c);
        }
    }

    private void AddOctahedron(Vector3 center, float radius, bool active)
    {
        Vector3 top = center + Vector3.Up * radius;
        Vector3 bottom = center + Vector3.Down * radius;
        Vector3 right = center + Vector3.Right * radius;
        Vector3 left = center + Vector3.Left * radius;
        Vector3 forward = center + Vector3.Forward * radius;
        Vector3 back = center + Vector3.Back * radius;

        AddVertexTriangle(top, right, forward, active);
        AddVertexTriangle(top, back, right, active);
        AddVertexTriangle(top, left, back, active);
        AddVertexTriangle(top, forward, left, active);
        AddVertexTriangle(bottom, forward, right, active);
        AddVertexTriangle(bottom, right, back, active);
        AddVertexTriangle(bottom, back, left, active);
        AddVertexTriangle(bottom, left, forward, active);
    }

    private void AddVertexTriangle(Vector3 a, Vector3 b, Vector3 c, bool active)
    {
        List<Vector3> vertices = active ? _activeVertexVertices : _defaultVertexVertices;
        List<Vector3> normals = active ? _activeVertexNormals : _defaultVertexNormals;
        List<int> indices = active ? _activeVertexIndices : _defaultVertexIndices;
        Vector3 normal = CalculateTriangleNormal(a, b, c);
        int firstIndex = vertices.Count;

        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);
        indices.Add(firstIndex);
        indices.Add(firstIndex + 1);
        indices.Add(firstIndex + 2);
    }

    private void ClearScratch()
    {
        _defaultEdgeVertices.Clear();
        _defaultEdgeNormals.Clear();
        _defaultEdgeIndices.Clear();
        _activeEdgeVertices.Clear();
        _activeEdgeNormals.Clear();
        _activeEdgeIndices.Clear();
        _defaultVertexVertices.Clear();
        _defaultVertexNormals.Clear();
        _defaultVertexIndices.Clear();
        _activeVertexVertices.Clear();
        _activeVertexNormals.Clear();
        _activeVertexIndices.Clear();
        _faceVertices.Clear();
        _faceNormals.Clear();
        _faceIndices.Clear();
        _faceTriangulation.Clear();
    }

    private static void RebuildMesh(
        MeshInstance3D instance,
        List<Vector3> vertices,
        List<Vector3> normals,
        List<int> indices
    )
    {
        if (indices.Count == 0)
        {
            instance.Mesh = null;
            return;
        }

        var meshArrays = new GodotArray();
        meshArrays.Resize((int)Godot.Mesh.ArrayType.Max);
        meshArrays[(int)Godot.Mesh.ArrayType.Vertex] = vertices.ToArray();
        meshArrays[(int)Godot.Mesh.ArrayType.Normal] = normals.ToArray();
        meshArrays[(int)Godot.Mesh.ArrayType.Index] = indices.ToArray();

        var renderMesh = new ArrayMesh();
        renderMesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, meshArrays);
        instance.Mesh = renderMesh;
    }

    private static StandardMaterial3D GetDefaultEdgeMaterial()
    {
        _defaultEdgeMaterial ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(0.55f, 0.62f, 0.65f, 0.55f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            NoDepthTest = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        return _defaultEdgeMaterial;
    }

    private static StandardMaterial3D GetActiveEdgeMaterial()
    {
        _activeEdgeMaterial ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.82f, 0.18f, 1.0f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            NoDepthTest = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        return _activeEdgeMaterial;
    }

    private static StandardMaterial3D GetDefaultVertexMaterial()
    {
        _defaultVertexMaterial ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(0.65f, 0.72f, 0.75f, 0.6f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            NoDepthTest = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        return _defaultVertexMaterial;
    }

    private static StandardMaterial3D GetActiveVertexMaterial()
    {
        _activeVertexMaterial ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.95f, 0.25f, 1.0f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            NoDepthTest = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        return _activeVertexMaterial;
    }

    private static StandardMaterial3D GetFaceMaterial()
    {
        _faceMaterial ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.75f, 0.12f, 0.4f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        return _faceMaterial;
    }

    private static float GetRadius(Vector3 position, Vector3 cameraOrigin, float scale) =>
        (position - cameraOrigin).Length() * scale;

    private static bool ContainsTarget(
        IReadOnlyList<SelectionTarget> targets,
        SelectionTarget target
    )
    {
        foreach (SelectionTarget existing in targets)
        {
            if (existing == target)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector3 ToGodotVector3(NumericsVector3 vector) =>
        new(vector.X, vector.Y, vector.Z);

    private static Vector3 GetCornerPosition(SpatialMesh mesh, FaceCornerHandle corner) =>
        ToGodotVector3(mesh.GetVertexPosition(mesh.GetHalfEdge(corner).Origin));

    private static Vector3 CalculateTriangleNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 normal = (b - a).Cross(c - a);
        return normal.LengthSquared() > 0.0f ? normal.Normalized() : Vector3.Up;
    }
}

using System.Collections.Generic;
using Godot;
using GodotArray = Godot.Collections.Array;

public sealed partial class EdgeCutPreview : Node3D
{
    private const float LineRadius = 0.025f;
    private const float MarkerRadius = 0.07f;
    private const int LineSegments = 8;

    private readonly List<Vector3> _vertices = [];
    private readonly List<Vector3> _normals = [];
    private readonly List<int> _indices = [];

    private MeshInstance3D _meshInstance;
    private StandardMaterial3D _material;

    public override void _Ready()
    {
        EnsureChild();
        Clear();
    }

    public void UpdatePreview(
        Transform3D meshTransform,
        Vector3 start,
        Vector3 end,
        bool hasValidTarget
    )
    {
        EnsureChild();
        _vertices.Clear();
        _normals.Clear();
        _indices.Clear();

        AddOctahedron(start, MarkerRadius);
        if (!start.IsEqualApprox(end))
            AddOctahedron(end, MarkerRadius);
        AddTube(start, end);

        GodotArray arrays = new();
        arrays.Resize((int)Godot.Mesh.ArrayType.Max);
        arrays[(int)Godot.Mesh.ArrayType.Vertex] = _vertices.ToArray();
        arrays[(int)Godot.Mesh.ArrayType.Normal] = _normals.ToArray();
        arrays[(int)Godot.Mesh.ArrayType.Index] = _indices.ToArray();

        ArrayMesh renderMesh = new();
        renderMesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, arrays);
        _meshInstance.Mesh = renderMesh;
        _material.AlbedoColor = hasValidTarget
            ? new Color(0.15f, 1f, 0.45f)
            : new Color(0.05f, 0.95f, 1f);
        GlobalTransform = meshTransform;
        Visible = true;
    }

    public void Clear()
    {
        if (_meshInstance != null)
            _meshInstance.Mesh = null;
        Visible = false;
    }

    private void EnsureChild()
    {
        if (_meshInstance != null)
            return;

        _material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.05f, 0.95f, 1f),
            NoDepthTest = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        _meshInstance = new MeshInstance3D
        {
            Name = "Cut",
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_meshInstance);
    }

    private void AddTube(Vector3 start, Vector3 end)
    {
        Vector3 direction = end - start;
        if (direction.IsZeroApprox())
            return;

        Vector3 axis = direction.Normalized();
        Vector3 reference = Mathf.Abs(axis.Dot(Vector3.Up)) > 0.95f ? Vector3.Right : Vector3.Up;
        Vector3 sideA = axis.Cross(reference).Normalized();
        Vector3 sideB = axis.Cross(sideA).Normalized();
        int firstIndex = _vertices.Count;

        for (int index = 0; index < LineSegments; index++)
        {
            float angle = Mathf.Tau * index / LineSegments;
            Vector3 normal = sideA * Mathf.Cos(angle) + sideB * Mathf.Sin(angle);
            _vertices.Add(start + normal * LineRadius);
            _normals.Add(normal);
            _vertices.Add(end + normal * LineRadius);
            _normals.Add(normal);
        }

        for (int index = 0; index < LineSegments; index++)
        {
            int next = (index + 1) % LineSegments;
            int a = firstIndex + index * 2;
            int b = firstIndex + next * 2;
            int c = firstIndex + next * 2 + 1;
            int d = firstIndex + index * 2 + 1;
            AddQuad(a, b, c, d);
        }
    }

    private void AddOctahedron(Vector3 center, float radius)
    {
        int firstIndex = _vertices.Count;
        Vector3[] offsets =
        [
            Vector3.Up * radius,
            Vector3.Right * radius,
            Vector3.Back * radius,
            Vector3.Left * radius,
            Vector3.Forward * radius,
            Vector3.Down * radius,
        ];
        foreach (Vector3 offset in offsets)
        {
            _vertices.Add(center + offset);
            _normals.Add(offset.Normalized());
        }

        AddTriangle(firstIndex, firstIndex + 1, firstIndex + 2);
        AddTriangle(firstIndex, firstIndex + 2, firstIndex + 3);
        AddTriangle(firstIndex, firstIndex + 3, firstIndex + 4);
        AddTriangle(firstIndex, firstIndex + 4, firstIndex + 1);
        AddTriangle(firstIndex + 5, firstIndex + 2, firstIndex + 1);
        AddTriangle(firstIndex + 5, firstIndex + 3, firstIndex + 2);
        AddTriangle(firstIndex + 5, firstIndex + 4, firstIndex + 3);
        AddTriangle(firstIndex + 5, firstIndex + 1, firstIndex + 4);
    }

    private void AddQuad(int a, int b, int c, int d)
    {
        AddTriangle(a, b, c);
        AddTriangle(a, c, d);
    }

    private void AddTriangle(int a, int b, int c)
    {
        _indices.Add(a);
        _indices.Add(b);
        _indices.Add(c);
    }
}

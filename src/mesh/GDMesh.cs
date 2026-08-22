using System;
using System.Collections.Generic;
using Godot;
using TRMesh.Mesh;

[GlobalClass]
public partial class GDMesh : Resource
{
    readonly GDMeshSurfaceBuilder _surfaceBuilder = new();

    public SpatialMesh SpatialMesh { get; }

    public ArrayMesh ArrayMesh { get; } = new();

    public MeshSurface Surface { get; private set; } = new();

    public bool Dirty { get; private set; } = true;

    public GDMesh()
        : this(new SpatialMesh()) { }

    public GDMesh(SpatialMesh spatialMesh)
    {
        ArgumentNullException.ThrowIfNull(spatialMesh);
        SpatialMesh = spatialMesh;
    }

    public void MarkDirty() => Dirty = true;

    public bool RebuildIfDirty()
    {
        if (!Dirty)
            return false;

        Surface = _surfaceBuilder.Build(SpatialMesh);

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = ConvertVertices(Surface.Vertices);
        arrays[(int)Mesh.ArrayType.Normal] = ConvertVertices(Surface.Normals);
        arrays[(int)Mesh.ArrayType.TexUV] = ConvertUvs(Surface.Uvs);
        arrays[(int)Mesh.ArrayType.Index] = Surface.ElementIndices.ToArray();

        ArrayMesh.ClearSurfaces();
        if (Surface.ElementIndices.Count != 0)
            ArrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        Dirty = false;

        if (Surface.SkippedFaceCount != 0)
        {
            GD.PushWarning(
                $"GDMesh skipped {Surface.SkippedFaceCount} face(s) that could not be triangulated."
            );
        }

        EmitChanged();
        return true;
    }

    static Vector3[] ConvertVertices(List<System.Numerics.Vector3> source)
    {
        var result = new Vector3[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            System.Numerics.Vector3 value = source[i];
            result[i] = new Vector3(value.X, value.Y, value.Z);
        }
        return result;
    }

    static Vector2[] ConvertUvs(List<System.Numerics.Vector2> source)
    {
        var result = new Vector2[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            System.Numerics.Vector2 value = source[i];
            result[i] = new Vector2(value.X, value.Y);
        }
        return result;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
            SpatialMesh.Dispose();
    }
}

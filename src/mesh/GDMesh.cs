using System;
using System.IO;
using Godot;
using TRMesh.Mesh;

[GlobalClass]
public partial class GDMesh : Resource
{
    readonly GDMeshSurfaceBuilder _surfaceBuilder = new();

    public SpatialMesh SpatialMesh { get; private set; }

    [Export]
    public byte[] MeshData
    {
        get
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            SpatialMesh.Serialize(writer);
            writer.Flush();
            return stream.ToArray();
        }
        set
        {
            SpatialMesh replacement;
            if (value is null || value.Length == 0)
            {
                replacement = new SpatialMesh();
            }
            else
            {
                using var stream = new MemoryStream(value, writable: false);
                using var reader = new BinaryReader(stream);
                replacement = SpatialMesh.Deserialize(reader);
            }

            SpatialMesh.Dispose();
            SpatialMesh = replacement;
            MarkDirty();
        }
    }

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

    public bool RebuildSurfaceIfDirty()
    {
        if (!Dirty)
            return false;

        Surface = _surfaceBuilder.Build(SpatialMesh);
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

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
            SpatialMesh.Dispose();
    }
}

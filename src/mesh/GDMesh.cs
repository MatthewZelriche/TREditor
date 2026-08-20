using System;
using Godot;
using TRMesh.Mesh;

[GlobalClass]
public partial class GDMesh : Resource
{
    public SpatialMesh SpatialMesh { get; }

    public ArrayMesh ArrayMesh { get; } = new();

    public GDMesh()
        : this(new SpatialMesh()) { }

    public GDMesh(SpatialMesh spatialMesh)
    {
        ArgumentNullException.ThrowIfNull(spatialMesh);
        SpatialMesh = spatialMesh;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
            SpatialMesh.Dispose();
    }
}

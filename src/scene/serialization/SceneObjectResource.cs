using Godot;

// MeshObject is the live immutable document record, while this Resource is a persistence-only
// record that exposes the object's data to Godot's ResourceSaver and ResourceLoader.
[GlobalClass]
public partial class SceneObjectResource : Resource
{
    [Export]
    public string Name { get; set; } = string.Empty;

    [Export]
    public Transform3D Transform { get; set; } = Transform3D.Identity;

    // Nullable due to lifetime shenanigans during initialization,
    // but it is expected to be non-null by the time the Document is instantiated
    // from the resource and throws otherwise during load
    [Export]
    public GDMesh? Mesh { get; set; }
}

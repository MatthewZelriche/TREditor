using System;
using Godot;
using TREditorSharp;

public sealed partial class CreateMeshCommand : EditorCommand
{
    private readonly Node3D _parent;
    private readonly MeshRenderable _renderable;

    public override string Name { get; }

    public CreateMeshCommand(Node3D parent, SpatialMesh mesh, string primitiveName)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(mesh);

        _parent = parent;
        _renderable = new MeshRenderable { Name = primitiveName };
        _renderable.TakeMesh(mesh);
        Name = $"Create {primitiveName}";
    }

    public override void Do()
    {
        if (_renderable.GetParent() == null)
        {
            _parent.AddChild(_renderable);
        }
    }

    public override void Undo()
    {
        Node parent = _renderable.GetParent();
        parent?.RemoveChild(_renderable);
    }
}

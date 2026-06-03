using System;
using Godot;
using TREditorSharp;

public sealed partial class CreateMeshCommand : EditorCommand
{
    private readonly Node3D _parent;
    private readonly TRMeshGD _meshNode;

    public override string Name { get; }

    public CreateMeshCommand(Node3D parent, SpatialMesh mesh, string primitiveName)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(mesh);

        _parent = parent;
        _meshNode = new TRMeshGD { Name = primitiveName };
        _meshNode.TakeMesh(mesh);
        Name = $"Create {primitiveName}";
    }

    public override void Do()
    {
        if (_meshNode.GetParent() == null)
        {
            _parent.AddChild(_meshNode);
        }
    }

    public override void Undo()
    {
        Node parent = _meshNode.GetParent();
        parent?.RemoveChild(_meshNode);
    }
}

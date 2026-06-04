using System;
using TREditorSharp;

public sealed partial class CreateMeshCommand : EditorCommand
{
    private readonly EditorObjectId _objectId;
    private readonly SpatialMesh _mesh;
    private readonly string _displayName;

    public override string Name { get; }

    public CreateMeshCommand(EditorObjectId objectId, SpatialMesh mesh, string displayName)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        _objectId = objectId;
        _mesh = mesh;
        _displayName = displayName;
        Name = $"Create {displayName}";
    }

    public override void Do(EditorCommandContext context)
    {
        context.Scene.CreateMeshObject(_objectId, _mesh, _displayName);
    }

    public override void Undo(EditorCommandContext context)
    {
        context.Scene.RemoveMeshObject(_objectId);
    }
}

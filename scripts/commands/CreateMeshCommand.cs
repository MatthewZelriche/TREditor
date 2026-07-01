using System;
using TREditorSharp;

public sealed class CreateMeshCommand : EditorCommand
{
    private readonly EditorObjectId _objectId;
    private readonly SpatialMesh _mesh;
    private readonly string _displayName;
    private bool _created;

    public override string Name { get; }

    public CreateMeshCommand(EditorObjectId objectId, SpatialMesh mesh, string displayName)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        _objectId = objectId;
        _mesh = mesh;
        _displayName = displayName;
        Name = $"Create {displayName}";
    }

    protected override bool Do(EditorCommandContext context)
    {
        if (_created)
            return context.Objects.RestoreMeshObject(_objectId);

        _created = context.Objects.CreateMeshObject(_objectId, _mesh, _displayName);
        return _created;
    }

    protected override void Undo(EditorCommandContext context)
    {
        context.Objects.RemoveMeshObject(_objectId);
    }

    protected override void OnDispose(
        EditorCommandContext context,
        EditorCommandState discardedState
    )
    {
        if (!_created)
        {
            _mesh.Dispose();
        }
        else if (discardedState == EditorCommandState.Undone)
        {
            context.Objects.DestroyMeshObject(_objectId);
        }
    }
}

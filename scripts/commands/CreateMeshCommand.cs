using System;
using Godot;
using TREditorSharp;

public sealed class CreateMeshCommand : EditorCommand
{
    private readonly EditorObjectModel _object;
    private bool _inScene;

    public override string Name { get; }

    public CreateMeshCommand(EditorObjectId objectId, SpatialMesh mesh, string displayName)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        _object = new EditorObjectModel(objectId, displayName, Transform3D.Identity, mesh);
        Name = $"Create {displayName}";
    }

    protected override bool Do(EditorCommandContext context)
    {
        if (!context.Lifecycle.Add(_object))
            return false;

        _inScene = true;
        return true;
    }

    protected override void Undo(EditorCommandContext context)
    {
        if (context.Lifecycle.Remove(_object.Id) != null)
            _inScene = false;
    }

    protected override void OnDispose(
        EditorCommandContext context,
        EditorCommandState discardedState
    )
    {
        if (_inScene && discardedState == EditorCommandState.Applied)
            return;

        _object.Dispose();
    }
}

#nullable enable

using TREditorSharp;

public sealed partial class ApplyTextureToFaceCommand : EditorCommand
{
    private readonly EditorObjectId _objectId;
    private readonly FaceTextureChange _change;

    public override string Name => "Apply Texture to Face";

    public ApplyTextureToFaceCommand(EditorObjectId objectId, FaceTextureChange change)
    {
        System.ArgumentNullException.ThrowIfNull(change);
        _objectId = objectId;
        _change = change;
    }

    public static ApplyTextureToFaceCommand? CreateIfValid(
        EditorObjectId objectId,
        SpatialMesh mesh,
        FaceHandle face,
        int materialSlot
    )
    {
        FaceTextureChange? change = FaceTextureChange.Create(mesh, face, materialSlot);
        return change == null ? null : new ApplyTextureToFaceCommand(objectId, change);
    }

    public override void Do(EditorCommandContext context)
    {
        context.Scene.ApplyFaceTexture(_objectId, _change, revert: false);
    }

    public override void Undo(EditorCommandContext context)
    {
        context.Scene.ApplyFaceTexture(_objectId, _change, revert: true);
    }
}

#nullable enable

using TREditorSharp;

public sealed class ApplyTextureToFaceCommand : EditorCommand
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

    protected override bool Do(EditorCommandContext context) =>
        context.Operations.ApplyFaceTexture(_objectId, _change, revert: false);

    protected override void Undo(EditorCommandContext context)
    {
        context.Operations.ApplyFaceTexture(_objectId, _change, revert: true);
    }
}

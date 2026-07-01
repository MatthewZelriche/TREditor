#nullable enable

using System.Linq;

public sealed class DetachFaceCommand : EditorCommand
{
    private readonly SelectionSnapshot _selectionBefore;
    private readonly SelectionTarget[] _faceTargets;
    private FaceDetachBatch[]? _batches;
    private SelectionSnapshot _selectionAfter;

    private DetachFaceCommand(SelectionSnapshot selection, SelectionTarget[] faceTargets)
    {
        _selectionBefore = selection;
        _faceTargets = faceTargets;
    }

    public override string Name => _faceTargets.Length == 1 ? "Detach Face" : "Detach Faces";

    public static bool CanCreate(SelectionSnapshot selection) =>
        selection.Count > 0
        && selection.Targets.All(target => target.Kind == ScenePickElementKind.Face);

    public static DetachFaceCommand? Create(SelectionSnapshot selection) =>
        CanCreate(selection) ? new DetachFaceCommand(selection, selection.Targets.ToArray()) : null;

    protected override bool Do(EditorCommandContext context)
    {
        if (_batches == null)
        {
            _batches = context.Scene.DetachFaces(_faceTargets);
            if (_batches.Length == 0)
                return false;

            _selectionAfter = SelectionSnapshot.From(
                _batches.SelectMany(batch =>
                    batch.DetachedFaces.Select(face =>
                        SelectionTarget.ForFace(batch.ObjectId, face)
                    )
                )
            );
        }
        else
        {
            context.Scene.ApplyFaceDetachAfter(_batches);
        }

        context.ApplySelection(_selectionAfter);
        return true;
    }

    protected override void Undo(EditorCommandContext context)
    {
        if (_batches == null || _batches.Length == 0)
            return;

        context.Scene.ApplyFaceDetachBefore(_batches);
        context.ApplySelection(_selectionBefore);
    }

    protected override void OnDispose(
        EditorCommandContext context,
        EditorCommandState discardedState
    )
    {
        if (_batches == null)
            return;

        foreach (FaceDetachBatch batch in _batches)
            batch.Dispose();
    }
}

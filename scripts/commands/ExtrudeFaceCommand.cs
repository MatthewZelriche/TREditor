#nullable enable

using Godot;

public sealed class ExtrudeFaceCommand : EditorCommand
{
    private readonly SelectionSnapshot _selectionBefore;
    private readonly SelectionTarget _faceTarget;
    private readonly Vector3 _worldDelta;
    private FaceExtrusionChange? _change;
    private SelectionSnapshot _selectionAfter;

    private ExtrudeFaceCommand(
        SelectionSnapshot selection,
        SelectionTarget faceTarget,
        Vector3 worldDelta
    )
    {
        _selectionBefore = selection;
        _faceTarget = faceTarget;
        _worldDelta = worldDelta;
    }

    public override string Name => "Extrude Face";

    public static bool CanCreate(SelectionSnapshot selection) =>
        selection.Count == 1 && selection.Targets[0].Kind == ScenePickElementKind.Face;

    public static ExtrudeFaceCommand? CreateIfChanged(
        SelectionSnapshot selection,
        Vector3 worldDelta
    ) =>
        !CanCreate(selection) || worldDelta.IsZeroApprox()
            ? null
            : new ExtrudeFaceCommand(selection, selection.Targets[0], worldDelta);

    protected override bool Do(EditorCommandContext context)
    {
        if (_change == null)
        {
            _change = context.Operations.ExtrudeFace(_faceTarget, _worldDelta);
            if (_change == null)
                return false;

            _selectionAfter = SelectionSnapshot.From(
                [SelectionTarget.ForFace(_change.ObjectId, _change.CapFace)]
            );
        }
        else
        {
            context.Operations.ApplyFaceExtrusionAfter(_change);
        }

        context.ApplySelection(_selectionAfter);
        return true;
    }

    protected override void Undo(EditorCommandContext context)
    {
        if (_change == null)
            return;

        context.Operations.ApplyFaceExtrusionBefore(_change);
        context.ApplySelection(_selectionBefore);
    }

    protected override void OnDispose(
        EditorCommandContext context,
        EditorCommandState discardedState
    )
    {
        _change?.Dispose();
    }
}

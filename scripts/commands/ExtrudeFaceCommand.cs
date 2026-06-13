#nullable enable

using Godot;

public sealed partial class ExtrudeFaceCommand : EditorCommand
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

    public override void Do(EditorCommandContext context)
    {
        if (_change == null)
        {
            _change = context.Scene.ExtrudeFace(_faceTarget, _worldDelta);
            if (_change == null)
                return;

            _selectionAfter = SelectionSnapshot.From([
                SelectionTarget.ForFace(_change.ObjectId, _change.CapFace),
            ]);
        }
        else
        {
            context.Scene.ApplyFaceExtrusionAfter(_change);
        }

        context.Selection.Apply(_selectionAfter);
    }

    public override void Undo(EditorCommandContext context)
    {
        if (_change == null)
            return;

        context.Scene.ApplyFaceExtrusionBefore(_change);
        context.Selection.Apply(_selectionBefore);
    }

    protected override void OnReleaseResources()
    {
        _change?.Dispose();
    }
}

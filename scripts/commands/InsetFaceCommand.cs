#nullable enable

public sealed partial class InsetFaceCommand : EditorCommand
{
    private readonly SelectionSnapshot _selectionBefore;
    private readonly SelectionTarget _faceTarget;
    private readonly float _depth;
    private FaceInsetChange? _change;
    private SelectionSnapshot _selectionAfter;

    private InsetFaceCommand(SelectionSnapshot selection, SelectionTarget faceTarget, float depth)
    {
        _selectionBefore = selection;
        _faceTarget = faceTarget;
        _depth = depth;
    }

    public override string Name => "Inset Face";

    public static bool CanCreate(SelectionSnapshot selection) =>
        selection.Count == 1 && selection.Targets[0].Kind == ScenePickElementKind.Face;

    public static InsetFaceCommand? Create(SelectionSnapshot selection, float depth) =>
        CanCreate(selection) && depth > 0f
            ? new InsetFaceCommand(selection, selection.Targets[0], depth)
            : null;

    public override void Do(EditorCommandContext context)
    {
        if (_change == null)
        {
            _change = context.Scene.InsetFace(_faceTarget, _depth);
            if (_change == null)
                return;

            _selectionAfter = SelectionSnapshot.From([
                SelectionTarget.ForFace(_change.ObjectId, _change.CapFace),
            ]);
        }
        else
        {
            context.Scene.ApplyFaceInsetAfter(_change);
        }

        context.Selection.Apply(_selectionAfter);
    }

    public override void Undo(EditorCommandContext context)
    {
        if (_change == null)
            return;

        context.Scene.ApplyFaceInsetBefore(_change);
        context.Selection.Apply(_selectionBefore);
    }

    protected override void OnReleaseResources()
    {
        _change?.Dispose();
    }
}

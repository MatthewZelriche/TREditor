#nullable enable

public sealed class CollapseFaceCommand : EditorCommand
{
    private readonly SelectionSnapshot _selectionBefore;
    private readonly SelectionTarget _faceTarget;
    private FaceCollapseChange? _change;
    private SelectionSnapshot _selectionAfter;

    private CollapseFaceCommand(SelectionSnapshot selection, SelectionTarget faceTarget)
    {
        _selectionBefore = selection;
        _faceTarget = faceTarget;
    }

    public override string Name => "Collapse Face";

    public static bool CanCreate(SelectionSnapshot selection) =>
        selection.Count == 1 && selection.Targets[0].Kind == ScenePickElementKind.Face;

    public static CollapseFaceCommand? Create(SelectionSnapshot selection) =>
        CanCreate(selection) ? new CollapseFaceCommand(selection, selection.Targets[0]) : null;

    protected override bool Do(EditorCommandContext context)
    {
        if (_change == null)
        {
            _change = context.Scene.CollapseFace(_faceTarget);
            if (_change == null)
                return false;

            _selectionAfter = SelectionSnapshot.From(
                [SelectionTarget.ForVertex(_change.ObjectId, _change.Survivor)]
            );
        }
        else
        {
            context.Scene.ApplyFaceCollapseAfter(_change);
        }

        context.ApplySelection(_selectionAfter);
        return true;
    }

    protected override void Undo(EditorCommandContext context)
    {
        if (_change == null)
            return;

        context.Scene.ApplyFaceCollapseBefore(_change);
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

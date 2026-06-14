#nullable enable

public sealed partial class FillHoleCommand : EditorCommand
{
    private readonly SelectionSnapshot _selectionBefore;
    private readonly SelectionTarget _edgeTarget;
    private FillHoleChange? _change;
    private SelectionSnapshot _selectionAfter;

    private FillHoleCommand(SelectionSnapshot selection, SelectionTarget edgeTarget)
    {
        _selectionBefore = selection;
        _edgeTarget = edgeTarget;
    }

    public override string Name => "Fill Hole";

    public static bool CanCreate(SelectionSnapshot selection) =>
        selection.Count == 1 && selection.Targets[0].Kind == ScenePickElementKind.Edge;

    public static FillHoleCommand? Create(SelectionSnapshot selection) =>
        CanCreate(selection) ? new FillHoleCommand(selection, selection.Targets[0]) : null;

    public override void Do(EditorCommandContext context)
    {
        if (_change == null)
        {
            _change = context.Scene.FillHole(_edgeTarget);
            if (_change == null)
                return;

            _selectionAfter = SelectionSnapshot.From(
                [SelectionTarget.ForFace(_change.ObjectId, _change.Face)]
            );
        }
        else
        {
            context.Scene.ApplyFillHoleAfter(_change);
        }

        context.Selection.Apply(_selectionAfter);
    }

    public override void Undo(EditorCommandContext context)
    {
        if (_change == null)
            return;

        context.Scene.ApplyFillHoleBefore(_change);
        context.Selection.Apply(_selectionBefore);
    }

    protected override void OnReleaseResources()
    {
        _change?.Dispose();
    }
}

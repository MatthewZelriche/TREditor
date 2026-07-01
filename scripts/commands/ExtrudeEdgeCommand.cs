#nullable enable

using Godot;

public sealed class ExtrudeEdgeCommand : EditorCommand
{
    private readonly SelectionSnapshot _selectionBefore;
    private readonly SelectionTarget _edgeTarget;
    private readonly Vector3 _worldDelta;
    private EdgeExtrusionChange? _change;
    private SelectionSnapshot _selectionAfter;

    private ExtrudeEdgeCommand(
        SelectionSnapshot selection,
        SelectionTarget edgeTarget,
        Vector3 worldDelta
    )
    {
        _selectionBefore = selection;
        _edgeTarget = edgeTarget;
        _worldDelta = worldDelta;
    }

    public override string Name => "Extrude Edge";

    public static bool CanCreate(SelectionSnapshot selection) =>
        selection.Count == 1 && selection.Targets[0].Kind == ScenePickElementKind.Edge;

    public static ExtrudeEdgeCommand? CreateIfChanged(
        SelectionSnapshot selection,
        Vector3 worldDelta
    ) =>
        !CanCreate(selection) || worldDelta.IsZeroApprox()
            ? null
            : new ExtrudeEdgeCommand(selection, selection.Targets[0], worldDelta);

    protected override bool Do(EditorCommandContext context)
    {
        if (_change == null)
        {
            _change = context.Scene.ExtrudeEdge(_edgeTarget, _worldDelta);
            if (_change == null)
                return false;

            _selectionAfter = SelectionSnapshot.From(
                [SelectionTarget.ForEdge(_change.ObjectId, _change.OuterEdge)]
            );
        }
        else
        {
            context.Scene.ApplyEdgeExtrusionAfter(_change);
        }

        context.ApplySelection(_selectionAfter);
        return true;
    }

    protected override void Undo(EditorCommandContext context)
    {
        if (_change == null)
            return;

        context.Scene.ApplyEdgeExtrusionBefore(_change);
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

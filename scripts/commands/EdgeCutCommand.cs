#nullable enable

public sealed class EdgeCutCommand : EditorCommand
{
    private readonly SelectionSnapshot _selectionBefore;
    private readonly SelectionTarget _face;
    private readonly HalfEdgeHandle _firstEdge;
    private readonly float _firstParameter;
    private readonly HalfEdgeHandle _secondEdge;
    private readonly float _secondParameter;
    private EdgeCutChange? _change;
    private SelectionSnapshot _selectionAfter;

    private EdgeCutCommand(
        SelectionSnapshot selection,
        SelectionTarget face,
        HalfEdgeHandle firstEdge,
        float firstParameter,
        HalfEdgeHandle secondEdge,
        float secondParameter
    )
    {
        _selectionBefore = selection;
        _face = face;
        _firstEdge = firstEdge;
        _firstParameter = firstParameter;
        _secondEdge = secondEdge;
        _secondParameter = secondParameter;
    }

    public override string Name => "Edge Cut";

    public static bool CanCreate(SelectionSnapshot selection) =>
        selection.Count == 1 && selection.Targets[0].Kind == ScenePickElementKind.Face;

    public static EdgeCutCommand? Create(
        SelectionSnapshot selection,
        HalfEdgeHandle firstEdge,
        float firstParameter,
        HalfEdgeHandle secondEdge,
        float secondParameter
    )
    {
        if (!CanCreate(selection))
            return null;

        return new EdgeCutCommand(
            selection,
            selection.Targets[0],
            firstEdge,
            firstParameter,
            secondEdge,
            secondParameter
        );
    }

    protected override bool Do(EditorCommandContext context)
    {
        if (_change == null)
        {
            _change = context.Scene.CutFace(
                _face,
                _firstEdge,
                _firstParameter,
                _secondEdge,
                _secondParameter
            );
            if (_change == null)
                return false;

            _selectionAfter = SelectionSnapshot.From(
                [SelectionTarget.ForEdge(_face.ObjectId, _change.CutEdge)]
            );
        }
        else
        {
            context.Scene.ApplyEdgeCutAfter(_change);
        }

        context.ApplySelection(_selectionAfter);
        return true;
    }

    protected override void Undo(EditorCommandContext context)
    {
        if (_change == null)
            return;

        context.Scene.ApplyEdgeCutBefore(_change);
        context.ApplySelection(_selectionBefore);
    }

    protected override void OnDispose(
        EditorCommandContext context,
        EditorCommandState discardedState
    ) => _change?.Dispose();
}

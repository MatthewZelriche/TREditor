#nullable enable

using System.Linq;

public sealed class CollapseVerticesCommand : EditorCommand
{
    private readonly SelectionSnapshot _selectionBefore;
    private readonly SelectionTarget[] _vertexTargets;
    private readonly CollapseVerticesTarget _twoVertexTarget;
    private VertexCollapseChange? _change;
    private SelectionSnapshot _selectionAfter;

    private CollapseVerticesCommand(
        SelectionSnapshot selection,
        SelectionTarget[] vertexTargets,
        CollapseVerticesTarget twoVertexTarget
    )
    {
        _selectionBefore = selection;
        _vertexTargets = vertexTargets;
        _twoVertexTarget = twoVertexTarget;
    }

    public override string Name => "Collapse Vertices";

    public static bool CanCreate(SelectionSnapshot selection) =>
        selection.Count >= 2
        && selection.Targets.All(target => target.Kind == ScenePickElementKind.Vertex)
        && selection.Targets.Select(target => target.ObjectId).Distinct().Count() == 1;

    public static CollapseVerticesCommand? Create(
        SelectionSnapshot selection,
        CollapseVerticesTarget twoVertexTarget
    ) =>
        CanCreate(selection)
            ? new CollapseVerticesCommand(selection, selection.Targets.ToArray(), twoVertexTarget)
            : null;

    protected override bool Do(EditorCommandContext context)
    {
        if (_change == null)
        {
            _change = context.Operations.CollapseVertices(_vertexTargets, _twoVertexTarget);
            if (_change == null)
                return false;

            _selectionAfter = SelectionSnapshot.From(
                [SelectionTarget.ForVertex(_change.ObjectId, _change.Survivor)]
            );
        }
        else
        {
            context.Operations.ApplyVertexCollapseAfter(_change);
        }

        context.ApplySelection(_selectionAfter);
        return true;
    }

    protected override void Undo(EditorCommandContext context)
    {
        if (_change == null)
            return;

        context.Operations.ApplyVertexCollapseBefore(_change);
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

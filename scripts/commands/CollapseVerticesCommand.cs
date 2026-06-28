#nullable enable

using System.Linq;

public sealed partial class CollapseVerticesCommand : EditorCommand
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

    public override void Do(EditorCommandContext context)
    {
        if (_change == null)
        {
            _change = context.Scene.CollapseVertices(_vertexTargets, _twoVertexTarget);
            if (_change == null)
                return;

            _selectionAfter = SelectionSnapshot.From(
                [SelectionTarget.ForVertex(_change.ObjectId, _change.Survivor)]
            );
        }
        else
        {
            context.Scene.ApplyVertexCollapseAfter(_change);
        }

        context.Selection.Apply(_selectionAfter);
    }

    public override void Undo(EditorCommandContext context)
    {
        if (_change == null)
            return;

        context.Scene.ApplyVertexCollapseBefore(_change);
        context.Selection.Apply(_selectionBefore);
    }

    protected override void OnReleaseResources()
    {
        _change?.Dispose();
    }
}

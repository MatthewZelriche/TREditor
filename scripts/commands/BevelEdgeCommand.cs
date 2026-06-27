#nullable enable

using System.Linq;

public sealed partial class BevelEdgeCommand : EditorCommand
{
    private readonly SelectionSnapshot _selectionBefore;
    private readonly SelectionTarget[] _edgeTargets;
    private readonly float _width;
    private EdgeBevelBatch[]? _batches;
    private SelectionSnapshot _selectionAfter;

    private BevelEdgeCommand(
        SelectionSnapshot selection,
        SelectionTarget[] edgeTargets,
        float width
    )
    {
        _selectionBefore = selection;
        _edgeTargets = edgeTargets;
        _width = width;
    }

    public override string Name => _edgeTargets.Length == 1 ? "Bevel Edge" : "Bevel Edges";

    public static bool CanCreate(SelectionSnapshot selection) =>
        selection.Count > 0
        && selection.Targets.All(target => target.Kind == ScenePickElementKind.Edge);

    public static BevelEdgeCommand? Create(SelectionSnapshot selection, float width) =>
        CanCreate(selection) && width > 0f && float.IsFinite(width)
            ? new BevelEdgeCommand(selection, selection.Targets.ToArray(), width)
            : null;

    public override void Do(EditorCommandContext context)
    {
        if (_batches == null)
        {
            _batches = context.Scene.BevelEdges(_edgeTargets, _width);
            if (_batches.Length == 0)
                return;

            _selectionAfter = SelectionSnapshot.From(
                _batches.SelectMany(batch =>
                    batch.BevelFaces.Select(face => SelectionTarget.ForFace(batch.ObjectId, face))
                )
            );
        }
        else
        {
            context.Scene.ApplyEdgeBevelAfter(_batches);
        }

        context.Selection.Apply(_selectionAfter);
    }

    public override void Undo(EditorCommandContext context)
    {
        if (_batches == null || _batches.Length == 0)
            return;

        context.Scene.ApplyEdgeBevelBefore(_batches);
        context.Selection.Apply(_selectionBefore);
    }

    protected override void OnReleaseResources()
    {
        if (_batches == null)
            return;

        foreach (EdgeBevelBatch batch in _batches)
            batch.Dispose();
    }
}

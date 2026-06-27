#nullable enable

using System.Linq;

public sealed partial class BevelVertexCommand : EditorCommand
{
    private readonly SelectionSnapshot _selectionBefore;
    private readonly SelectionTarget[] _vertexTargets;
    private readonly float _width;
    private VertexBevelBatch[]? _batches;
    private SelectionSnapshot _selectionAfter;

    private BevelVertexCommand(
        SelectionSnapshot selection,
        SelectionTarget[] vertexTargets,
        float width
    )
    {
        _selectionBefore = selection;
        _vertexTargets = vertexTargets;
        _width = width;
    }

    public override string Name => _vertexTargets.Length == 1 ? "Bevel Vertex" : "Bevel Vertices";

    public static bool CanCreate(SelectionSnapshot selection) =>
        selection.Count > 0
        && selection.Targets.All(target => target.Kind == ScenePickElementKind.Vertex);

    public static BevelVertexCommand? Create(SelectionSnapshot selection, float width) =>
        CanCreate(selection) && width > 0f && float.IsFinite(width)
            ? new BevelVertexCommand(selection, selection.Targets.ToArray(), width)
            : null;

    public override void Do(EditorCommandContext context)
    {
        if (_batches == null)
        {
            _batches = context.Scene.BevelVertices(_vertexTargets, _width);
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
            context.Scene.ApplyVertexBevelAfter(_batches);
        }

        context.Selection.Apply(_selectionAfter);
    }

    public override void Undo(EditorCommandContext context)
    {
        if (_batches == null || _batches.Length == 0)
            return;

        context.Scene.ApplyVertexBevelBefore(_batches);
        context.Selection.Apply(_selectionBefore);
    }

    protected override void OnReleaseResources()
    {
        if (_batches == null)
            return;

        foreach (VertexBevelBatch batch in _batches)
            batch.Dispose();
    }
}

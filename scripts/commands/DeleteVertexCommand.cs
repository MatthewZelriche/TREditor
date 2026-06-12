#nullable enable

using System.Collections.Generic;
using System.Linq;

public sealed partial class DeleteVertexCommand : EditorCommand
{
    private readonly SelectionSnapshot _selectionBefore;
    private readonly SelectionTarget[] _vertexTargets;
    private VertexDeletionBatch[]? _batches;
    private SelectionSnapshot _selectionAfterDelete;

    public override string Name => _vertexTargets.Length == 1 ? "Delete Vertex" : "Delete Vertices";

    private DeleteVertexCommand(SelectionSnapshot selection, SelectionTarget[] vertexTargets)
    {
        _selectionBefore = selection;
        _vertexTargets = vertexTargets;
    }

    public static DeleteVertexCommand? CreateIfAny(SelectionSnapshot selection)
    {
        SelectionTarget[] vertices = GetSelectedVertices(selection).ToArray();
        return vertices.Length == 0 ? null : new DeleteVertexCommand(selection, vertices);
    }

    public static IEnumerable<SelectionTarget> GetSelectedVertices(SelectionSnapshot selection) =>
        selection.Targets.Where(target => target.Kind == ScenePickElementKind.Vertex).Distinct();

    public override void Do(EditorCommandContext context)
    {
        if (_batches == null)
        {
            _batches = context.Scene.DeleteVertices(_vertexTargets);
            _selectionAfterDelete = BuildSelectionAfterDelete();
        }
        else
        {
            context.Scene.ApplyVertexDeletionAfter(_batches);
        }

        context.Selection.Apply(_selectionAfterDelete);
    }

    public override void Undo(EditorCommandContext context)
    {
        if (_batches == null || _batches.Length == 0)
            return;

        context.Scene.ApplyVertexDeletionBefore(_batches);
        context.Selection.Apply(_selectionBefore);
    }

    protected override void OnReleaseResources()
    {
        if (_batches == null)
            return;

        foreach (VertexDeletionBatch batch in _batches)
            batch.Dispose();
    }

    private SelectionSnapshot BuildSelectionAfterDelete()
    {
        HashSet<(EditorObjectId, VertexHandle)> removedVertices = [];
        HashSet<(EditorObjectId, HalfEdgeHandle)> removedEdges = [];
        HashSet<(EditorObjectId, FaceHandle)> removedFaces = [];
        foreach (VertexDeletionBatch batch in _batches!)
        {
            foreach (VertexHandle vertex in batch.RemovedVertices)
                removedVertices.Add((batch.ObjectId, vertex));
            foreach (HalfEdgeHandle edge in batch.RemovedEdges)
                removedEdges.Add((batch.ObjectId, edge));
            foreach (FaceHandle face in batch.RemovedFaces)
                removedFaces.Add((batch.ObjectId, face));
        }

        IEnumerable<SelectionTarget> kept = _selectionBefore.Targets.Where(target =>
            !(
                target.Kind == ScenePickElementKind.Vertex
                && removedVertices.Contains((target.ObjectId, target.Vertex))
            )
            && !(
                target.Kind == ScenePickElementKind.Edge
                && removedEdges.Contains((target.ObjectId, target.Edge))
            )
            && !(
                target.Kind == ScenePickElementKind.Face
                && removedFaces.Contains((target.ObjectId, target.Face))
            )
        );

        return SelectionSnapshot.From(kept);
    }
}

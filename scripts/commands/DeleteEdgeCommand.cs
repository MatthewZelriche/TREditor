#nullable enable

using System.Collections.Generic;
using System.Linq;

public sealed class DeleteEdgeCommand : EditorCommand
{
    private readonly SelectionSnapshot _selectionBefore;
    private readonly SelectionTarget[] _edgeTargets;
    private EdgeDeletionBatch[]? _batches;
    private SelectionSnapshot _selectionAfterDelete;

    public override string Name => _edgeTargets.Length == 1 ? "Delete Edge" : "Delete Edges";

    private DeleteEdgeCommand(SelectionSnapshot selection, SelectionTarget[] edgeTargets)
    {
        _selectionBefore = selection;
        _edgeTargets = edgeTargets;
    }

    public static DeleteEdgeCommand? CreateIfAny(SelectionSnapshot selection)
    {
        SelectionTarget[] edges = GetSelectedEdges(selection).ToArray();
        return edges.Length == 0 ? null : new DeleteEdgeCommand(selection, edges);
    }

    public static IEnumerable<SelectionTarget> GetSelectedEdges(SelectionSnapshot selection) =>
        selection.Targets.Where(target => target.Kind == ScenePickElementKind.Edge).Distinct();

    protected override bool Do(EditorCommandContext context)
    {
        if (_batches == null)
        {
            _batches = context.Operations.DeleteEdges(_edgeTargets);
            if (_batches.Length == 0)
                return false;

            _selectionAfterDelete = BuildSelectionAfterDelete();
        }
        else
        {
            // Redo replays the stored after-state rather than rerunning the deletion.
            context.Operations.ApplyEdgeDeletionAfter(_batches);
        }

        context.ApplySelection(_selectionAfterDelete);
        return true;
    }

    protected override void Undo(EditorCommandContext context)
    {
        if (_batches == null || _batches.Length == 0)
            return;

        context.Operations.ApplyEdgeDeletionBefore(_batches);
        context.ApplySelection(_selectionBefore);
    }

    protected override void OnDispose(
        EditorCommandContext context,
        EditorCommandState discardedState
    )
    {
        if (_batches == null)
            return;

        foreach (EdgeDeletionBatch batch in _batches)
            batch.Dispose();
    }

    private SelectionSnapshot BuildSelectionAfterDelete()
    {
        HashSet<(EditorObjectId, HalfEdgeHandle)> removedEdges = [];
        HashSet<(EditorObjectId, FaceHandle)> removedFaces = [];
        foreach (EdgeDeletionBatch batch in _batches!)
        {
            foreach (HalfEdgeHandle edge in batch.RemovedEdges)
                removedEdges.Add((batch.ObjectId, edge));
            foreach (FaceHandle face in batch.RemovedFaces)
                removedFaces.Add((batch.ObjectId, face));
        }

        IEnumerable<SelectionTarget> kept = _selectionBefore.Targets.Where(target =>
            !(
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

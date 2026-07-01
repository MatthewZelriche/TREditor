using System.Collections.Generic;
using System.Linq;

public sealed class DeleteMeshCommand : EditorCommand
{
    private readonly SelectionSnapshot _selection;
    private readonly EditorObjectId[] _objectIds;
    private EditorObjectId[] _removedObjectIds;

    public override string Name => _objectIds.Length == 1 ? "Delete Mesh" : "Delete Meshes";

    private DeleteMeshCommand(SelectionSnapshot selection, EditorObjectId[] objectIds)
    {
        _selection = selection;
        _objectIds = objectIds;
    }

    public static DeleteMeshCommand CreateIfAny(SelectionSnapshot selection)
    {
        EditorObjectId[] objectIds = GetSelectedObjectIds(selection).ToArray();
        return objectIds.Length == 0 ? null : new DeleteMeshCommand(selection, objectIds);
    }

    public static IEnumerable<EditorObjectId> GetSelectedObjectIds(SelectionSnapshot selection) =>
        selection
            .Targets.Where(target => target.Kind == ScenePickElementKind.Object)
            .Select(target => target.ObjectId)
            .Distinct();

    protected override bool Do(EditorCommandContext context)
    {
        context.ApplySelection(SelectionSnapshot.Empty);

        if (_removedObjectIds == null)
        {
            List<EditorObjectId> removed = [];
            foreach (EditorObjectId objectId in _objectIds)
            {
                if (context.Objects.RemoveMeshObject(objectId))
                    removed.Add(objectId);
            }

            if (removed.Count == 0)
            {
                context.ApplySelection(_selection);
                return false;
            }

            _removedObjectIds = removed.ToArray();
        }
        else
        {
            foreach (EditorObjectId objectId in _removedObjectIds)
                context.Objects.RemoveMeshObject(objectId);
        }

        return true;
    }

    protected override void Undo(EditorCommandContext context)
    {
        foreach (EditorObjectId objectId in _removedObjectIds)
        {
            context.Objects.RestoreMeshObject(objectId);
        }

        context.ApplySelection(_selection);
    }

    protected override void OnDispose(
        EditorCommandContext context,
        EditorCommandState discardedState
    )
    {
        if (discardedState != EditorCommandState.Applied || _removedObjectIds == null)
            return;

        foreach (EditorObjectId objectId in _removedObjectIds)
            context.Objects.DestroyMeshObject(objectId);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

public sealed class DeleteMeshCommand : EditorCommand
{
    private readonly SelectionSnapshot _selection;
    private readonly EditorObjectId[] _objectIds;
    private EditorObjectModel[] _removedObjects;

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

        if (_removedObjects == null)
        {
            List<EditorObjectModel> removed = [];
            foreach (EditorObjectId objectId in _objectIds)
            {
                EditorObjectModel removedObject = context.Lifecycle.Remove(objectId);
                if (removedObject != null)
                    removed.Add(removedObject);
            }

            if (removed.Count == 0)
            {
                context.ApplySelection(_selection);
                return false;
            }

            _removedObjects = removed.ToArray();
            return true;
        }

        foreach (EditorObjectModel removedObject in _removedObjects)
        {
            if (context.Lifecycle.Remove(removedObject.Id) == null)
            {
                throw new InvalidOperationException(
                    $"Could not remove object '{removedObject.Id}' during delete redo."
                );
            }
        }

        return true;
    }

    protected override void Undo(EditorCommandContext context)
    {
        foreach (EditorObjectModel removedObject in _removedObjects)
        {
            if (!context.Lifecycle.Add(removedObject))
            {
                throw new InvalidOperationException(
                    $"Could not restore object '{removedObject.Id}' during delete undo."
                );
            }
        }

        context.ApplySelection(_selection);
    }

    protected override void OnDispose(
        EditorCommandContext context,
        EditorCommandState discardedState
    )
    {
        if (discardedState != EditorCommandState.Applied || _removedObjects == null)
            return;

        foreach (EditorObjectModel removedObject in _removedObjects)
            removedObject.Dispose();
    }
}

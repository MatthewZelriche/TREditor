using System.Collections.Generic;
using System.Linq;

public sealed partial class DeleteMeshCommand : EditorCommand
{
    private readonly SelectionSnapshot _selection;
    private readonly EditorObjectId[] _objectIds;

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

    public override void Do(EditorCommandContext context)
    {
        context.Selection.Apply(SelectionSnapshot.Empty);

        foreach (EditorObjectId objectId in _objectIds)
        {
            context.Scene.RemoveMeshObject(objectId);
        }
    }

    public override void Undo(EditorCommandContext context)
    {
        foreach (EditorObjectId objectId in _objectIds)
        {
            context.Scene.RestoreMeshObject(objectId);
        }

        context.Selection.Apply(_selection);
    }
}

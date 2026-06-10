#nullable enable

using System.Collections.Generic;
using System.Linq;

public sealed partial class DeleteFaceCommand : EditorCommand
{
    private readonly SelectionSnapshot _selectionBefore;
    private readonly SelectionSnapshot _selectionAfterDelete;
    private readonly SelectionTarget[] _faceTargets;
    private FaceDeletionChange[]? _changes;

    public override string Name => _faceTargets.Length == 1 ? "Delete Face" : "Delete Faces";

    private DeleteFaceCommand(SelectionSnapshot selection, SelectionTarget[] faceTargets)
    {
        _selectionBefore = selection;
        _faceTargets = faceTargets;
        _selectionAfterDelete = SelectionSnapshot.From(
            selection.Targets.Where(target => target.Kind != ScenePickElementKind.Face)
        );
    }

    public static DeleteFaceCommand? CreateIfAny(SelectionSnapshot selection)
    {
        SelectionTarget[] faces = GetSelectedFaces(selection).ToArray();
        return faces.Length == 0 ? null : new DeleteFaceCommand(selection, faces);
    }

    public static IEnumerable<SelectionTarget> GetSelectedFaces(SelectionSnapshot selection) =>
        selection.Targets.Where(target => target.Kind == ScenePickElementKind.Face).Distinct();

    public override void Do(EditorCommandContext context)
    {
        _changes ??= context.Scene.CaptureFaceDeletions(_faceTargets);
        if (_changes.Length == 0)
            return;

        // Remove stale face selections before rebuilding overlays against the changed topology.
        context.Selection.Apply(_selectionAfterDelete);
        context.Scene.DeleteFaces(_changes);
    }

    public override void Undo(EditorCommandContext context)
    {
        if (_changes == null || _changes.Length == 0)
            return;

        context.Scene.RestoreFaces(_changes);
        context.Selection.Apply(BuildRestoredSelection());
    }

    private SelectionSnapshot BuildRestoredSelection()
    {
        Dictionary<(EditorObjectId ObjectId, FaceHandle Face), SelectionTarget> restored =
            _changes!.ToDictionary(
                change => (change.ObjectId, change.OriginalFace),
                change => change.SelectionTarget
            );
        List<SelectionTarget> selection = [];

        foreach (SelectionTarget target in _selectionBefore.Targets)
        {
            if (
                target.Kind == ScenePickElementKind.Face
                && restored.TryGetValue(
                    (target.ObjectId, target.Face),
                    out SelectionTarget restoredFace
                )
            )
            {
                selection.Add(restoredFace);
            }
            else if (target.Kind != ScenePickElementKind.Face)
            {
                selection.Add(target);
            }
        }

        return SelectionSnapshot.From(selection);
    }
}

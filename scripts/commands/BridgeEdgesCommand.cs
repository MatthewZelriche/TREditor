#nullable enable

using System.Linq;

public sealed partial class BridgeEdgesCommand : EditorCommand
{
    private readonly SelectionSnapshot _selectionBefore;
    private readonly SelectionTarget _first;
    private readonly SelectionTarget _second;
    private readonly int _segments;
    private readonly float _archAngleDegrees;
    private BridgeEdgesChange? _change;
    private SelectionSnapshot _selectionAfter;

    private BridgeEdgesCommand(SelectionSnapshot selection, int segments, float archAngleDegrees)
    {
        _selectionBefore = selection;
        _first = selection.Targets[0];
        _second = selection.Targets[1];
        _segments = segments;
        _archAngleDegrees = archAngleDegrees;
    }

    public override string Name => "Bridge Edges";

    public static bool CanCreate(SelectionSnapshot selection) =>
        selection.Count == 2
        && selection.Targets.All(target => target.Kind == ScenePickElementKind.Edge)
        && selection.Targets[0].ObjectId == selection.Targets[1].ObjectId;

    public static BridgeEdgesCommand? Create(
        SelectionSnapshot selection,
        int segments,
        float archAngleDegrees
    ) =>
        CanCreate(selection)
        && segments >= 1
        && archAngleDegrees >= 0f
        && archAngleDegrees <= 180f
        && float.IsFinite(archAngleDegrees)
            ? new BridgeEdgesCommand(selection, segments, archAngleDegrees)
            : null;

    public override void Do(EditorCommandContext context)
    {
        if (_change == null)
        {
            _change = context.Scene.BridgeEdges(_first, _second, _segments, _archAngleDegrees);
            if (_change == null)
                return;

            _selectionAfter = SelectionSnapshot.From(
                _change.Faces.Select(face => SelectionTarget.ForFace(_change.ObjectId, face))
            );
        }
        else
        {
            context.Scene.ApplyBridgeEdgesAfter(_change);
        }

        context.Selection.Apply(_selectionAfter);
    }

    public override void Undo(EditorCommandContext context)
    {
        if (_change == null)
            return;

        context.Scene.ApplyBridgeEdgesBefore(_change);
        context.Selection.Apply(_selectionBefore);
    }

    protected override void OnReleaseResources()
    {
        _change?.Dispose();
    }
}

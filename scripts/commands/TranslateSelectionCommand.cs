using Godot;

public sealed partial class TranslateSelectionCommand : EditorCommand
{
    private readonly SelectionSnapshot _selection;
    private readonly Vector3 _delta;

    public TranslateSelectionCommand(SelectionSnapshot selection, Vector3 delta)
    {
        _selection = selection;
        _delta = delta;
    }

    public override string Name => "Translate Selection";

    public static TranslateSelectionCommand CreateIfChanged(
        SelectionSnapshot selection,
        Vector3 delta
    ) => selection.IsEmpty || delta.IsZeroApprox() ? null : new(selection, delta);

    public override void Do(EditorCommandContext context)
    {
        context.Scene.TranslateSelection(_selection, _delta);
    }

    public override void Undo(EditorCommandContext context)
    {
        context.Scene.TranslateSelection(_selection, -_delta);
    }
}

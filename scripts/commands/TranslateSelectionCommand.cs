using Godot;

public sealed class TranslateSelectionCommand : EditorCommand
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

    protected override bool Do(EditorCommandContext context)
    {
        return context.Operations.TranslateSelection(_selection, _delta);
    }

    protected override void Undo(EditorCommandContext context)
    {
        context.Operations.TranslateSelection(_selection, -_delta);
    }
}

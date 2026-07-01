using System;

public sealed class SetSelectionCommand : EditorCommand
{
    private readonly SelectionSnapshot _before;
    private readonly SelectionSnapshot _after;

    public override string Name { get; }

    public override bool AffectsDocument => false;

    public SetSelectionCommand(SelectionSnapshot before, SelectionSnapshot after)
    {
        _before = before;
        _after = after;
        Name = after.IsEmpty ? "Clear Selection" : "Set Selection";
    }

    public static SetSelectionCommand CreateIfChanged(
        SelectionSnapshot before,
        SelectionSnapshot after
    )
    {
        return before == after ? null : new SetSelectionCommand(before, after);
    }

    protected override bool Do(EditorCommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.ApplySelection(_after);
    }

    protected override void Undo(EditorCommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.ApplySelection(_before);
    }
}

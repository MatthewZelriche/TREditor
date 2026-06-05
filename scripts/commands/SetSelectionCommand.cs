using System;

public sealed partial class SetSelectionCommand : EditorCommand
{
    private readonly SelectionSnapshot _before;
    private readonly SelectionSnapshot _after;

    public override string Name { get; }

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

    public override void Do(EditorCommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Selection.Apply(_after);
    }

    public override void Undo(EditorCommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Selection.Apply(_before);
    }
}

// The authoring entry point for reversible edits. Callers hand over a command and get no access to
// the history stacks in return.
public interface IEditorCommandSink
{
    void Submit(IEditorCommand command);

    // Executes without publishing a recoverable failure. The caller owns how that failure is shown.
    bool TrySubmit(IEditorCommand command, out string? failure);
}

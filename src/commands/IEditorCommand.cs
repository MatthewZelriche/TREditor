// A single reversible editor operation. Implementations retain every piece of state
// Undo needs and must stay valid after the code that created them is gone.
public interface IEditorCommand
{
    string DisplayName { get; }

    // Applies the operation. Also called to redo, so it must produce the same result when called again
    // after Undo. A failed call must restore the prior state before throwing.
    void Execute();

    // Reverts the operation applied by Execute. A failed call must restore the prior state before throwing.
    void Undo();
}

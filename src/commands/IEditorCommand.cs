// A single reversible editor operation. Implementations retain every piece of state Undo needs, apart
// from the document, which is supplied on each call so a command never has to capture it.
public interface IEditorCommand
{
    string DisplayName { get; }

    // Applies the operation. Also called to redo, so it must produce the same result when called again
    // after Undo. A failed call must restore the prior state before throwing.
    void Execute(Document document);

    // Reverts the operation applied by Execute. A failed call must restore the prior state before throwing.
    void Undo(Document document);
}

using System;

// A committed move of one object. Exact before and after MeshObject records are retained so undo and
// redo replace the same instances the command observed; a stale intervening replacement fails rather
// than overwriting unrelated state.
public sealed class TranslateCommand : IEditorCommand
{
    // Memory: This is fine because both are records that point to the same underlying
    // GDMesh. However, for commands that require TopologyPatches, we will need to be more careful
    // with what we store to avoid bloating memory. We store a lot of commands for history and they need
    // to be lightweight.
    private readonly MeshObject _before;
    private readonly MeshObject _after;

    public TranslateCommand(MeshObject before, MeshObject after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        _before = before;
        _after = after;
    }

    public string DisplayName => "Translate";

    public void Execute(Document document) => Replace(document, _before, _after);

    public void Undo(Document document) => Replace(document, _after, _before);

    private static void Replace(Document document, MeshObject expected, MeshObject replacement)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!document.ReplaceObject(expected, replacement))
        {
            throw new NonFatalCommandError(
                $"Object {expected.Id.Value} is missing or stale and cannot be translated."
            );
        }
    }
}

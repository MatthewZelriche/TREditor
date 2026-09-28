using System;
using System.Collections.Generic;

// A committed move of one or more objects. Exact before and after MeshObject records are retained so undo and
// redo replace the same instances the command observed; a stale intervening replacement fails rather
// than overwriting unrelated state.
public sealed class TranslateCommand : IEditorCommand
{
    // Memory: This is fine because both are records that point to the same underlying
    // GDMesh. However, for commands that require TopologyPatches, we will need to be more careful
    // with what we store to avoid bloating memory. We store a lot of commands for history and they need
    // to be lightweight.
    private readonly MeshObject[] _before;
    private readonly MeshObject[] _after;

    public TranslateCommand(MeshObject before, MeshObject after)
        : this([before], [after]) { }

    public TranslateCommand(
        IReadOnlyList<MeshObject> before,
        IReadOnlyList<MeshObject> after
    )
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (before.Count == 0)
            throw new ArgumentException("At least one object must be translated.", nameof(before));
        if (before.Count != after.Count)
            throw new ArgumentException("Before and after object counts must match.", nameof(after));

        _before = new MeshObject[before.Count];
        _after = new MeshObject[after.Count];
        for (int index = 0; index < before.Count; index++)
        {
            _before[index] = before[index]
                ?? throw new ArgumentException("Before objects cannot contain null.", nameof(before));
            _after[index] = after[index]
                ?? throw new ArgumentException("After objects cannot contain null.", nameof(after));
        }
    }

    public string DisplayName => "Translate";

    public void Execute(Document document) => Replace(document, _before, _after);

    public void Undo(Document document) => Replace(document, _after, _before);

    private static void Replace(
        Document document,
        IReadOnlyList<MeshObject> expected,
        IReadOnlyList<MeshObject> replacements
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!document.ReplaceObjects(expected, replacements))
        {
            throw new NonFatalCommandError(
                "One or more selected objects are missing or stale and cannot be translated."
            );
        }
    }
}

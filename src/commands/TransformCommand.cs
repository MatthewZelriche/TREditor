using System;
using System.Collections.Generic;

// A committed transform of one or more objects. Exact before and after records are retained so
// undo and redo cannot overwrite a stale intervening replacement.
public sealed class TransformCommand : IEditorCommand
{
    private readonly MeshObject[] _before;
    private readonly MeshObject[] _after;
    private readonly TransformOperation _operation;

    public TransformCommand(
        TransformOperation operation,
        MeshObject before,
        MeshObject after
    )
        : this(operation, [before], [after]) { }

    public TransformCommand(
        TransformOperation operation,
        IReadOnlyList<MeshObject> before,
        IReadOnlyList<MeshObject> after
    )
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (before.Count == 0)
            throw new ArgumentException("At least one object must be transformed.", nameof(before));
        if (before.Count != after.Count)
            throw new ArgumentException("Before and after object counts must match.", nameof(after));

        _operation = operation;
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

    public string DisplayName => _operation.ToString();

    public void Execute(Document document) => Replace(document, _before, _after);

    public void Undo(Document document) => Replace(document, _after, _before);

    private void Replace(
        Document document,
        IReadOnlyList<MeshObject> expected,
        IReadOnlyList<MeshObject> replacements
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!document.ReplaceObjects(expected, replacements))
        {
            throw new NonFatalCommandError(
                $"One or more selected objects are missing or stale and cannot be {_operation.ToString().ToLowerInvariant()}d."
            );
        }
    }
}

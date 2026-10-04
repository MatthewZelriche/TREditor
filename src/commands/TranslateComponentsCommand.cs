using System;
using System.Collections.Generic;

// Swaps complete mesh copies for a component move. The document owns the installed side while this
// command owns the detached side, allowing undo/redo and history eviction to transfer ownership
// without sharing or double-disposing GDMesh resources.
internal sealed class TranslateComponentsCommand : IEditorCommand, IDisposable
{
    private readonly MeshObject[] _before;
    private readonly MeshObject[] _after;
    private bool _isApplied;
    private bool _disposed;

    internal TranslateComponentsCommand(
        IReadOnlyList<MeshObject> before,
        IReadOnlyList<GDMesh> replacementMeshes
    )
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(replacementMeshes);
        if (before.Count == 0 || before.Count != replacementMeshes.Count)
            throw new ArgumentException("Object and mesh counts must be equal and non-zero.");

        _before = [.. before];
        _after = new MeshObject[before.Count];
        for (int index = 0; index < before.Count; index++)
            _after[index] = before[index] with { Mesh = replacementMeshes[index] };
    }

    public string DisplayName => "Translate";

    internal bool IsApplied => _isApplied;

    public void Execute(Document document) => Swap(document, installAfter: true);

    public void Undo(Document document) => Swap(document, installAfter: false);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (MeshObject meshObject in _isApplied ? _before : _after)
            meshObject.Mesh.Dispose();
    }

    private void Swap(Document document, bool installAfter)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        MeshObject[] expected = installAfter ? _before : _after;
        MeshObject[] replacements = installAfter ? _after : _before;
        if (_isApplied == installAfter || !document.ReplaceMeshes(expected, replacements))
        {
            throw new NonFatalCommandError(
                installAfter
                    ? "One or more selected components are missing or stale and cannot be translated."
                    : "One or more translated components are missing or stale and cannot be restored."
            );
        }

        _isApplied = installAfter;
    }
}

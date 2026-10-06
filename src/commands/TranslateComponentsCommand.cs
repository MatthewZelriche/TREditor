using System;
using System.Collections.Generic;
using TRMesh.Mesh;

// Applies component positions in place. Each execution records the sparse patch needed by the next
// undo; redo deliberately runs the requested write again because a patch is a one-way restoration.
internal sealed class TranslateComponentsCommand : IEditorCommand
{
    private readonly ComponentVertexSelection[] _edits;
    private readonly MeshObject[] _objects;
    private MeshPatch[]? _undo;

    // Each edit's Positions are the target positions of its Vertices.
    internal TranslateComponentsCommand(IReadOnlyList<ComponentVertexSelection> edits)
    {
        _edits = [.. edits];
        _objects = Array.ConvertAll(_edits, edit => edit.Object);
    }

    public string DisplayName => "Translate";

    public void Execute(Document document)
    {
        var patches = new MeshPatch[_edits.Length];
        if (_undo is not null || !document.EditGeometry(_objects, () => RecordAll(patches)))
            throw StaleError(undo: false);

        _undo = patches;
    }

    public void Undo(Document document)
    {
        MeshPatch[] undo = _undo ?? throw StaleError(undo: true);
        if (!document.EditGeometry(_objects, () => RevertAll(undo)))
            throw StaleError(undo: true);

        _undo = null;
    }

    private void RecordAll(MeshPatch[] patches)
    {
        int applied = 0;
        try
        {
            for (; applied < patches.Length; applied++)
                patches[applied] = Record(applied);
        }
        catch
        {
            for (int index = applied - 1; index >= 0; index--)
                _objects[index].Mesh.SpatialMesh.Revert(patches[index]);
            throw;
        }
    }

    private void RevertAll(MeshPatch[] undo)
    {
        int reverted = 0;
        try
        {
            for (; reverted < undo.Length; reverted++)
                _objects[reverted].Mesh.SpatialMesh.Revert(undo[reverted]);
        }
        catch (Exception exception)
        {
            for (int index = 0; index < reverted; index++)
                undo[index] = Record(index);
            if (exception is InvalidOperationException)
                throw StaleError(undo: true);
            throw;
        }
    }

    private MeshPatch Record(int index) =>
        _objects[index].Mesh.SpatialMesh.Record(mesh =>
            mesh.SetVertexPositions(_edits[index].Vertices, _edits[index].Positions)
        );

    private static NonFatalCommandError StaleError(bool undo) =>
        new(
            undo
                ? "One or more translated components are missing or stale and cannot be restored."
                : "One or more selected components are missing or stale and cannot be translated."
        );
}

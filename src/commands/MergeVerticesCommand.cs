using System;
using System.Collections.Generic;
using TRMesh.Mesh;
using NumericsVector3 = System.Numerics.Vector3;

internal sealed class MergeVerticesCommand : IEditorCommand
{
    private readonly SelectionValue _beforeSelection;
    private readonly VertexMergePosition _position;
    private readonly IEditorSelection _selection;
    private MeshObject? _object;
    private int[]? _vertices;
    private int _target;
    private NumericsVector3 _targetPosition;
    private IEditorCommand? _selectionCommand;
    private MeshPatch? _undo;

    internal MergeVerticesCommand(
        SelectionValue selection,
        VertexMergePosition position,
        IEditorSelection selectionState
    )
    {
        _beforeSelection = selection;
        _position = position;
        _selection = selectionState;
    }

    public string DisplayName => "Merge Vertices";

    public void Execute(Document document)
    {
        if (_undo is not null)
            throw StaleError();
        if (_object is null)
            Resolve(document);

        MeshObject meshObject = _object!;
        MeshPatch? patch = null;
        try
        {
            if (
                !document.EditGeometry(
                    [meshObject],
                    () =>
                        patch = meshObject.Mesh.SpatialMesh.Record(mesh =>
                            mesh.MergeVertices(_vertices!, _target, _targetPosition)
                        )
                )
            )
            {
                throw StaleError();
            }
        }
        catch (ArgumentException exception)
        {
            throw new NonFatalCommandError(exception.Message);
        }

        _undo = patch;
        _selectionCommand!.Execute(document);
    }

    public void Undo(Document document)
    {
        MeshPatch patch = _undo ?? throw StaleError();
        MeshObject meshObject = _object!;
        if (!document.EditGeometry([meshObject], () => meshObject.Mesh.SpatialMesh.Revert(patch)))
            throw StaleError();

        _undo = null;
        _selectionCommand!.Undo(document);
    }

    private void Resolve(Document document)
    {
        IReadOnlyList<MeshComponentId> components = _beforeSelection.OrderedComponents;
        if (_beforeSelection.ObjectIds.Count != 0 || components.Count < 2)
            throw InvalidSelection();

        MeshComponentId first = components[0];
        foreach (MeshComponentId component in components)
        {
            if (component.Type != MeshComponentType.Vertex || component.ObjectId != first.ObjectId)
                throw InvalidSelection();
        }

        if (!document.TryGetObject(first.ObjectId, out MeshObject meshObject))
            throw StaleError();

        var vertices = new int[components.Count];
        NumericsVector3 centroid = default;
        for (int index = 0; index < components.Count; index++)
        {
            int vertex = components[index].Slot;
            if (!meshObject.Mesh.SpatialMesh.IsVertexAlive(vertex))
                throw StaleError();
            vertices[index] = vertex;
            centroid += meshObject.Mesh.SpatialMesh.GetVertexData(vertex).position;
        }

        int targetIndex = _position == VertexMergePosition.First ? 0 : vertices.Length - 1;
        _target = vertices[targetIndex];
        _targetPosition = _position == VertexMergePosition.Centroid
            ? centroid / vertices.Length
            : meshObject.Mesh.SpatialMesh.GetVertexData(_target).position;
        _vertices = vertices;
        _object = meshObject;
        _selectionCommand = _selection.CreateCommand(
            SelectionValue.ForComponent(
                new MeshComponentId(meshObject.Id, MeshComponentType.Vertex, _target)
            )
        );
    }

    private static NonFatalCommandError InvalidSelection() =>
        new("Select at least two vertices from one mesh before merging.");

    private static NonFatalCommandError StaleError() =>
        new("The selected vertices are missing or stale and cannot be merged.");
}

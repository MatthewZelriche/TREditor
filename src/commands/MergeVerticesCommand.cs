using System;
using System.Collections.Generic;
using TRMesh.Mesh;
using NumericsVector3 = System.Numerics.Vector3;

internal sealed class MergeVerticesCommand : MeshPatchCommandBase
{
    private const string StaleSelection =
        "The selected vertices are missing or stale and cannot be merged.";

    private readonly int[] _vertices;
    private readonly int _target;
    private readonly NumericsVector3 _targetPosition;

    private MergeVerticesCommand(
        MeshObject meshObject,
        int[] vertices,
        int target,
        NumericsVector3 targetPosition,
        IEditorSelection selection
    )
        : base(selection)
    {
        Object = meshObject;
        _vertices = vertices;
        _target = target;
        _targetPosition = targetPosition;
    }

    public override string DisplayName => "Merge Vertices";

    protected override MeshObject Object { get; }

    protected override string StaleMessage => StaleSelection;

    protected override MeshPatch RecordEdit() =>
        Object.Mesh.SpatialMesh.Record(mesh =>
            mesh.MergeVertices(_vertices, _target, _targetPosition)
        );

    protected override SelectionValue SelectionAfterEdit() =>
        SelectionValue.ForComponent(
            new MeshComponentId(Object.Id, MeshComponentType.Vertex, _target)
        );

    internal static EditorToolAction CreateAction(
        IEditorToolContext context,
        VertexMergePosition position
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        return ActionOrReason(() =>
            Resolve(context.Document, context.Selection.Current, position, context.Selection)
        );
    }

    private static MergeVerticesCommand Resolve(
        IEditorDocumentReader document,
        SelectionValue selection,
        VertexMergePosition position,
        IEditorSelection editorSelection
    )
    {
        IReadOnlyList<MeshComponentId> components = selection.OrderedComponents;
        if (selection.ObjectIds.Count != 0)
        {
            throw new ArgumentException(
                "Deselect all objects; Merge Vertices only accepts vertex selections."
            );
        }
        if (components.Count < 2)
        {
            throw new ArgumentException(
                components.Count == 0
                    ? "Select at least two vertices from one mesh."
                    : "Select at least one more vertex from the same mesh."
            );
        }

        MeshComponentId first = components[0];
        foreach (MeshComponentId component in components)
        {
            if (component.Type != MeshComponentType.Vertex)
            {
                throw new ArgumentException(
                    "Only vertices can be merged; deselect all selected edges and faces."
                );
            }
            if (component.ObjectId != first.ObjectId)
                throw new ArgumentException("Selected vertices must belong to one mesh.");
        }
        if (!document.TryGetObject(first.ObjectId, out MeshObject meshObject))
            throw new ArgumentException(StaleSelection);

        SpatialMesh mesh = meshObject.Mesh.SpatialMesh;
        var vertices = new int[components.Count];
        NumericsVector3 centroid = default;
        for (int index = 0; index < components.Count; index++)
        {
            int vertex = components[index].Slot;
            if (!mesh.IsVertexAlive(vertex))
                throw new ArgumentException(StaleSelection);

            vertices[index] = vertex;
            centroid += mesh.GetVertexData(vertex).position;
        }

        int target = position == VertexMergePosition.First ? vertices[0] : vertices[^1];
        NumericsVector3 targetPosition =
            position == VertexMergePosition.Centroid
                ? centroid / vertices.Length
                : mesh.GetVertexData(target).position;
        return new MergeVerticesCommand(
            meshObject,
            vertices,
            target,
            targetPosition,
            editorSelection
        );
    }
}

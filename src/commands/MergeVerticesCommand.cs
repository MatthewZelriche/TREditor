using System;
using System.Collections.Generic;
using TRMesh.Mesh;
using NumericsVector3 = System.Numerics.Vector3;

internal sealed class MergeVerticesCommand : IEditorCommand
{
    private const string StaleSelection =
        "The selected vertices are missing or stale and cannot be merged.";

    private readonly MergeVerticesPlan _plan;
    private readonly IEditorCommand _selectionCommand;
    private MeshPatch? _undo;

    private MergeVerticesCommand(MergeVerticesPlan plan, IEditorCommand selectionCommand)
    {
        _plan = plan;
        _selectionCommand = selectionCommand;
    }

    public string DisplayName => "Merge Vertices";

    internal static EditorToolAction CreateAction(
        IEditorToolContext context,
        VertexMergePosition position
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            MergeVerticesPlan plan = Resolve(context.Document, context.Selection.Current, position);
            return new EditorToolAction(
                new MergeVerticesCommand(
                    plan,
                    context.Selection.CreateCommand(
                        SelectionValue.ForComponent(
                            new MeshComponentId(
                                plan.Object.Id,
                                MeshComponentType.Vertex,
                                plan.Target
                            )
                        )
                    )
                ),
                null
            );
        }
        catch (ArgumentException exception)
        {
            return new EditorToolAction(null, FirstLine(exception.Message));
        }
    }

    public void Execute(Document document)
    {
        if (_undo is not null)
            throw StaleError();

        MeshPatch? patch = null;
        try
        {
            if (!document.EditGeometry([_plan.Object], () => patch = _plan.Record()))
                throw StaleError();
        }
        catch (ArgumentException exception)
        {
            throw new NonFatalCommandError(FirstLine(exception.Message));
        }

        _undo = patch;
        _selectionCommand.Execute(document);
    }

    public void Undo(Document document)
    {
        MeshPatch patch = _undo ?? throw StaleError();
        if (!document.EditGeometry([_plan.Object], () => _plan.Object.Mesh.SpatialMesh.Revert(patch)))
            throw StaleError();

        _undo = null;
        _selectionCommand.Undo(document);
    }

    private static MergeVerticesPlan Resolve(
        IEditorDocumentReader document,
        SelectionValue selection,
        VertexMergePosition position
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
        NumericsVector3 targetPosition = position == VertexMergePosition.Centroid
            ? centroid / vertices.Length
            : mesh.GetVertexData(target).position;
        return new MergeVerticesPlan(meshObject, vertices, target, targetPosition);
    }

    private static string FirstLine(string message)
    {
        int lineBreak = message.IndexOfAny(['\r', '\n']);
        return lineBreak < 0 ? message : message[..lineBreak];
    }

    private static NonFatalCommandError StaleError() => new(StaleSelection);
}

internal readonly record struct MergeVerticesPlan(
    MeshObject Object,
    int[] Vertices,
    int Target,
    NumericsVector3 TargetPosition
)
{
    internal MeshPatch Record() => Object.Mesh.SpatialMesh.Record(Apply);

    private void Apply(SpatialMesh mesh) => mesh.MergeVertices(Vertices, Target, TargetPosition);
}

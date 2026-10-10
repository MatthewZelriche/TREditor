using System;
using System.Collections.Generic;
using TRMesh.Mesh;

internal sealed class CollapseEdgesCommand : MeshPatchCommandBase
{
    private const string StaleSelection =
        "The selected edges are missing or stale and cannot be collapsed.";

    private readonly int[] _edges;
    private readonly int[] _endpoints;

    private CollapseEdgesCommand(
        MeshObject meshObject,
        int[] edges,
        int[] endpoints,
        IEditorSelection selection
    )
        : base(selection)
    {
        Object = meshObject;
        _edges = edges;
        _endpoints = endpoints;
    }

    public override string DisplayName => "Collapse Edges";

    protected override MeshObject Object { get; }

    protected override string StaleMessage => StaleSelection;

    protected override MeshPatch RecordEdit() =>
        Object.Mesh.SpatialMesh.Record(mesh => mesh.CollapseEdges(_edges));

    protected override SelectionValue SelectionAfterEdit()
    {
        SpatialMesh mesh = Object.Mesh.SpatialMesh;
        var survivors = new List<MeshComponentId>(_endpoints.Length);
        foreach (int vertex in _endpoints)
        {
            if (mesh.IsVertexAlive(vertex))
                survivors.Add(new MeshComponentId(Object.Id, MeshComponentType.Vertex, vertex));
        }

        return SelectionValue.ForSelections([], survivors);
    }

    internal static EditorToolAction CreateAction(IEditorToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ActionOrReason(() =>
            Resolve(context.Document, context.Selection.Current, context.Selection)
        );
    }

    private static CollapseEdgesCommand Resolve(
        IEditorDocumentReader document,
        SelectionValue selection,
        IEditorSelection editorSelection
    )
    {
        IReadOnlyList<MeshComponentId> components = selection.OrderedComponents;
        if (selection.ObjectIds.Count != 0)
        {
            throw new ArgumentException(
                "Deselect all objects; Collapse Edges only accepts edge selections."
            );
        }
        if (components.Count == 0)
            throw new ArgumentException("Select at least one edge from one mesh.");

        MeshComponentId first = components[0];
        foreach (MeshComponentId component in components)
        {
            if (component.Type != MeshComponentType.Edge)
            {
                throw new ArgumentException(
                    "Only edges can be collapsed; deselect all selected vertices and faces."
                );
            }
            if (component.ObjectId != first.ObjectId)
                throw new ArgumentException("Selected edges must belong to one mesh.");
        }
        if (!document.TryGetObject(first.ObjectId, out MeshObject meshObject))
            throw new ArgumentException(StaleSelection);

        SpatialMesh mesh = meshObject.Mesh.SpatialMesh;
        var edges = new int[components.Count];
        var endpoints = new HashSet<int>();
        for (int index = 0; index < components.Count; index++)
        {
            int halfEdge = components[index].Slot;
            if (!mesh.IsHalfEdgeAlive(halfEdge))
                throw new ArgumentException(StaleSelection);

            HalfEdge topology = mesh.GetHalfEdgeData(halfEdge).topology;
            edges[index] = halfEdge;
            endpoints.Add(topology.SourceVertex);
            endpoints.Add(mesh.GetHalfEdgeData(topology.TwinHalfEdge).topology.SourceVertex);
        }

        return new CollapseEdgesCommand(meshObject, edges, [.. endpoints], editorSelection);
    }
}

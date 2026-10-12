using System;
using System.Collections.Generic;
using TRMesh.Mesh;

internal sealed class DissolveEdgesCommand : MeshPatchCommandBase
{
    private const string StaleSelection =
        "The selected edges are missing or stale and cannot be dissolved.";

    private readonly int[] _edges;

    private DissolveEdgesCommand(
        MeshObject meshObject,
        int[] edges,
        IEditorSelection selection
    )
        : base(selection)
    {
        Object = meshObject;
        _edges = edges;
    }

    public override string DisplayName => "Dissolve Edges";

    protected override MeshObject Object { get; }

    protected override string StaleMessage => StaleSelection;

    protected override MeshPatch RecordEdit() =>
        Object.Mesh.SpatialMesh.Record(mesh => mesh.DissolveEdges(_edges));

    protected override SelectionValue SelectionAfterEdit() => SelectionValue.Empty;

    internal static EditorToolAction CreateAction(IEditorToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ActionOrReason(() =>
            Resolve(context.Document, context.Selection.Current, context.Selection)
        );
    }

    private static DissolveEdgesCommand Resolve(
        IEditorDocumentReader document,
        SelectionValue selection,
        IEditorSelection editorSelection
    )
    {
        MeshObject meshObject = RequireHomogeneousComponents(
            document,
            selection,
            MeshComponentType.Edge,
            StaleSelection,
            "Deselect all objects; Dissolve Edges only accepts edge selections.",
            "Select at least one interior edge from one mesh.",
            "Only edges can be dissolved; deselect all selected vertices and faces.",
            out IReadOnlyList<MeshComponentId> components
        );

        SpatialMesh mesh = meshObject.Mesh.SpatialMesh;
        var edges = new int[components.Count];
        var seen = new HashSet<int>();
        for (int index = 0; index < components.Count; index++)
        {
            int edge = components[index].Slot;
            if (!mesh.IsHalfEdgeAlive(edge))
                throw new ArgumentException(StaleSelection);

            HalfEdge topology = mesh.GetHalfEdgeData(edge).topology;
            int twin = topology.TwinHalfEdge;
            if (!mesh.IsHalfEdgeAlive(twin))
                throw new ArgumentException(StaleSelection);
            if (!seen.Add(Math.Min(edge, twin)))
                throw new ArgumentException("Each edge can only be selected once.");

            int firstFace = topology.AdjacentFace;
            int secondFace = mesh.GetHalfEdgeData(twin).topology.AdjacentFace;
            if (
                !mesh.IsFaceAlive(firstFace)
                || !mesh.IsFaceAlive(secondFace)
                || firstFace == secondFace
            )
            {
                throw new ArgumentException(
                    "Every selected edge must lie between two distinct faces."
                );
            }

            edges[index] = edge;
        }

        return new DissolveEdgesCommand(meshObject, edges, editorSelection);
    }
}

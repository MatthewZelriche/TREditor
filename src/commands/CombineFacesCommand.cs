using System;
using System.Collections.Generic;
using TRMesh.Mesh;

internal sealed class CombineFacesCommand : MeshPatchCommandBase
{
    private const string StaleSelection =
        "The selected faces are missing or stale and cannot be combined.";

    private readonly int[] _edges;
    private readonly int _survivor;

    private CombineFacesCommand(
        MeshObject meshObject,
        int[] edges,
        int survivor,
        IEditorSelection selection
    )
        : base(selection)
    {
        Object = meshObject;
        _edges = edges;
        _survivor = survivor;
    }

    public override string DisplayName => "Combine Faces";

    protected override MeshObject Object { get; }

    protected override string StaleMessage => StaleSelection;

    protected override MeshPatch RecordEdit() =>
        Object.Mesh.SpatialMesh.Record(mesh => mesh.DissolveEdges(_edges));

    protected override SelectionValue SelectionAfterEdit() =>
        SelectionValue.ForComponent(
            new MeshComponentId(Object.Id, MeshComponentType.Face, _survivor)
        );

    internal static EditorToolAction CreateAction(IEditorToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ActionOrReason(() =>
            Resolve(context.Document, context.Selection.Current, context.Selection)
        );
    }

    private static CombineFacesCommand Resolve(
        IEditorDocumentReader document,
        SelectionValue selection,
        IEditorSelection editorSelection
    )
    {
        MeshObject meshObject = RequireHomogeneousComponents(
            document,
            selection,
            MeshComponentType.Face,
            StaleSelection,
            "Deselect all objects; Combine Faces only accepts face selections.",
            "Select at least two connected faces from one mesh.",
            "Only faces can be combined; deselect all selected vertices and edges.",
            out IReadOnlyList<MeshComponentId> components,
            minimumCount: 2,
            insufficientMessage: "Select at least two connected faces from one mesh."
        );

        SpatialMesh mesh = meshObject.Mesh.SpatialMesh;
        var selectedFaces = new HashSet<int>();
        int survivor = int.MaxValue;
        foreach (MeshComponentId component in components)
        {
            int face = component.Slot;
            if (!mesh.IsFaceAlive(face))
                throw new ArgumentException(StaleSelection);
            if (!selectedFaces.Add(face))
                throw new ArgumentException("Each face can only be selected once.");
            survivor = Math.Min(survivor, face);
        }

        var visited = new HashSet<int> { survivor };
        var pending = new Queue<int>();
        var internalEdges = new HashSet<int>();
        pending.Enqueue(survivor);
        while (pending.TryDequeue(out int face))
        {
            int start = mesh.GetFaceData(face).topology.AdjacentHalfEdge;
            int edge = start;
            do
            {
                if (!mesh.IsHalfEdgeAlive(edge))
                    throw new ArgumentException(StaleSelection);

                HalfEdge topology = mesh.GetHalfEdgeData(edge).topology;
                int twin = topology.TwinHalfEdge;
                if (!mesh.IsHalfEdgeAlive(twin))
                    throw new ArgumentException(StaleSelection);

                int adjacentFace = mesh.GetHalfEdgeData(twin).topology.AdjacentFace;
                if (selectedFaces.Contains(adjacentFace))
                {
                    internalEdges.Add(Math.Min(edge, twin));
                    if (visited.Add(adjacentFace))
                        pending.Enqueue(adjacentFace);
                }

                edge = topology.NextHalfEdge;
            } while (edge != start);
        }

        if (visited.Count != selectedFaces.Count)
        {
            throw new ArgumentException(
                "Selected faces must form one connected face island."
            );
        }

        return new CombineFacesCommand(
            meshObject,
            [.. internalEdges],
            survivor,
            editorSelection
        );
    }
}

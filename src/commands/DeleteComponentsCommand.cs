using System;
using System.Collections.Generic;
using TRMesh.Mesh;

internal sealed class DeleteComponentsCommand : MeshPatchCommandBase
{
    private const string StaleSelection =
        "The selected components are missing or stale and cannot be deleted.";

    private readonly int[] _faces;
    private readonly int[] _edges;
    private readonly int[] _vertices;

    internal DeleteComponentsCommand(
        IEditorDocumentReader document,
        SelectionValue selection,
        IEditorSelection editorSelection
    )
        : base(editorSelection)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(editorSelection);
        if (selection.ObjectIds.Count != 0)
        {
            throw new ArgumentException(
                "Deselect all objects; Delete only accepts component selections."
            );
        }

        IReadOnlyList<MeshComponentId> components = selection.OrderedComponents;
        if (components.Count == 0)
            throw new ArgumentException("Select at least one component to delete.");

        MeshComponentId first = components[0];
        List<int>[] slotsByType = [[], [], []];
        foreach (MeshComponentId component in components)
        {
            if (component.ObjectId != first.ObjectId)
                throw new ArgumentException("Selected components must belong to one mesh.");

            int type = (int)component.Type;
            if ((uint)type >= slotsByType.Length)
                throw new ArgumentException(StaleSelection);
            slotsByType[type].Add(component.Slot);
        }

        if (!document.TryGetObject(first.ObjectId, out MeshObject meshObject))
            throw new ArgumentException(StaleSelection);

        List<int> vertices = slotsByType[(int)MeshComponentType.Vertex];
        List<int> edges = slotsByType[(int)MeshComponentType.Edge];
        List<int> faces = slotsByType[(int)MeshComponentType.Face];
        SpatialMesh mesh = meshObject.Mesh.SpatialMesh;
        if (
            faces.Exists(face => !mesh.IsFaceAlive(face))
            || edges.Exists(edge => !mesh.IsHalfEdgeAlive(edge))
            || vertices.Exists(vertex => !mesh.IsVertexAlive(vertex))
        )
        {
            throw new ArgumentException(StaleSelection);
        }

        _faces = [.. faces];
        _edges = [.. edges];
        _vertices = [.. vertices];
        if (!mesh.WouldDeleteAnyFace(_faces, _edges, _vertices))
        {
            throw new ArgumentException(
                "The selection does not bound any faces that can be deleted."
            );
        }

        Object = meshObject;
    }

    public override string DisplayName => "Delete";

    protected override MeshObject Object { get; }

    protected override string StaleMessage => StaleSelection;

    protected override MeshPatch RecordEdit() =>
        Object.Mesh.SpatialMesh.Record(mesh =>
            mesh.DeleteComponents(_faces, _edges, _vertices)
        );

    protected override SelectionValue SelectionAfterEdit() => SelectionValue.Empty;
}

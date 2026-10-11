using System;
using System.Collections.Generic;
using TRMesh.Mesh;
using NumericsVector3 = System.Numerics.Vector3;

internal sealed class CollapseFacesCommand : MeshPatchCommandBase
{
    private const string StaleSelection =
        "The selected faces are missing or stale and cannot be collapsed.";

    private readonly int[] _vertices;
    private readonly int _target;
    private readonly NumericsVector3 _targetPosition;

    private CollapseFacesCommand(
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

    public override string DisplayName => "Collapse Faces";

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

    internal static EditorToolAction CreateAction(IEditorToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ActionOrReason(() =>
            Resolve(context.Document, context.Selection.Current, context.Selection)
        );
    }

    private static CollapseFacesCommand Resolve(
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
            "Deselect all objects; Collapse Faces only accepts face selections.",
            "Select at least one face from one mesh.",
            "Only faces can be collapsed; deselect all selected vertices and edges.",
            out IReadOnlyList<MeshComponentId> _
        );
        if (
            !ComponentVertexSelectionResolver.TryResolve(
                document,
                selection.Components,
                ComponentMask.Face,
                previews: null,
                out ComponentVertexSelection[] selections
            )
        )
        {
            throw new ArgumentException(StaleSelection);
        }

        ComponentVertexSelection resolved = selections[0];
        NumericsVector3 centroid = default;
        foreach (NumericsVector3 position in resolved.Positions)
            centroid += position;

        int target = resolved.Vertices[0];
        return new CollapseFacesCommand(
            meshObject,
            resolved.Vertices,
            target,
            centroid / resolved.Vertices.Length,
            editorSelection
        );
    }
}

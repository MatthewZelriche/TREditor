using System;
using System.Collections.Generic;
using Godot;
using TRMesh.Mesh;
using NumericsVector2 = System.Numerics.Vector2;
using NumericsVector3 = System.Numerics.Vector3;

internal sealed class ExtendEdgesCommand : MeshPatchCommandBase
{
    private const string StaleSelection =
        "The selected edges are missing or stale and cannot be extended.";
    private static readonly NumericsVector2[] QuadUvs =
    [
        NumericsVector2.Zero,
        NumericsVector2.UnitX,
        NumericsVector2.One,
        NumericsVector2.UnitY,
    ];

    private readonly int[] _boundaryEdges;
    private readonly int[] _vertices;
    private readonly NumericsVector3[] _newPositions;
    private int[] _outerEdges = [];

    private ExtendEdgesCommand(
        MeshObject meshObject,
        int[] boundaryEdges,
        int[] vertices,
        NumericsVector3[] newPositions,
        IEditorSelection selection
    )
        : base(selection)
    {
        Object = meshObject;
        _boundaryEdges = boundaryEdges;
        _vertices = vertices;
        _newPositions = newPositions;
    }

    public override string DisplayName => "Extend Edges";

    protected override MeshObject Object { get; }

    protected override string StaleMessage => StaleSelection;

    protected override MeshPatch RecordEdit() =>
        Object.Mesh.SpatialMesh.Record(Extend);

    protected override SelectionValue SelectionAfterEdit()
    {
        var selected = new MeshComponentId[_outerEdges.Length];
        for (int index = 0; index < selected.Length; index++)
        {
            selected[index] = new MeshComponentId(
                Object.Id,
                MeshComponentType.Edge,
                _outerEdges[index]
            );
        }

        return SelectionValue.ForSelections([], selected);
    }

    internal static EditorToolAction CreateAction(IEditorToolContext context, float length)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ActionOrReason(() =>
            Resolve(context.Document, context.Selection.Current, context.Selection, length)
        );
    }

    private void Extend(SpatialMesh mesh)
    {
        var copies = new Dictionary<int, int>(_vertices.Length);
        for (int index = 0; index < _vertices.Length; index++)
            copies.Add(_vertices[index], mesh.AddVertex(_newPositions[index]));

        _outerEdges = new int[_boundaryEdges.Length];
        for (int index = 0; index < _boundaryEdges.Length; index++)
        {
            HalfEdge boundary = mesh.GetHalfEdgeData(_boundaryEdges[index]).topology;
            int source = boundary.SourceVertex;
            int target = mesh.GetHalfEdgeData(boundary.TwinHalfEdge).topology.SourceVertex;
            int face = mesh.AddFace(
                [source, target, copies[target], copies[source]],
                QuadUvs
            );
            // Face half-edges walk source→target→copy(target)→copy(source); the third is outer.
            int first = mesh.GetFaceData(face).topology.AdjacentHalfEdge;
            int outer = mesh.GetHalfEdgeData(
                mesh.GetHalfEdgeData(first).topology.NextHalfEdge
            ).topology.NextHalfEdge;
            _outerEdges[index] = Math.Min(
                outer,
                mesh.GetHalfEdgeData(outer).topology.TwinHalfEdge
            );
        }
    }

    private static ExtendEdgesCommand Resolve(
        IEditorDocumentReader document,
        SelectionValue selection,
        IEditorSelection editorSelection,
        float length
    )
    {
        if (!float.IsFinite(length) || length < ExtendEdgesTool.MinimumLength)
            throw new ArgumentException("Extend length must be a positive finite value.");

        MeshObject meshObject = RequireHomogeneousComponents(
            document,
            selection,
            MeshComponentType.Edge,
            StaleSelection,
            "Deselect all objects; Extend Edges only accepts edge selections.",
            "Select at least one boundary edge from one mesh.",
            "Only edges can be extended; deselect all selected vertices and faces.",
            out IReadOnlyList<MeshComponentId> components
        );

        SpatialMesh mesh = meshObject.Mesh.SpatialMesh;
        Transform3D transform = meshObject.Transform;
        float determinant = transform.Basis.Determinant();
        if (!float.IsFinite(determinant) || Mathf.IsZeroApprox(determinant))
            throw new ArgumentException("The mesh object's transform must be invertible.");

        Transform3D inverse = transform.AffineInverse();
        Basis normalTransform = transform.Basis.Inverse().Transposed();
        float orientation = determinant < 0.0f ? -1.0f : 1.0f;
        var boundaryEdges = new int[components.Count];
        var seenEdges = new HashSet<int>();
        var outwardSums = new Dictionary<int, Vector3>();

        for (int index = 0; index < components.Count; index++)
        {
            int selected = components[index].Slot;
            if (!mesh.IsHalfEdgeAlive(selected))
                throw new ArgumentException(StaleSelection);

            HalfEdge selectedData = mesh.GetHalfEdgeData(selected).topology;
            int twin = selectedData.TwinHalfEdge;
            if (!mesh.IsHalfEdgeAlive(twin))
                throw new ArgumentException(StaleSelection);
            if (!seenEdges.Add(Math.Min(selected, twin)))
                throw new ArgumentException("Each boundary edge can only be selected once.");

            HalfEdge twinData = mesh.GetHalfEdgeData(twin).topology;
            bool selectedHasFace = mesh.IsFaceAlive(selectedData.AdjacentFace);
            bool twinHasFace = mesh.IsFaceAlive(twinData.AdjacentFace);
            if (selectedHasFace == twinHasFace)
                throw new ArgumentException("Every selected edge must be an open boundary edge.");

            int boundaryEdge = selectedHasFace ? twin : selected;
            HalfEdge boundary = mesh.GetHalfEdgeData(boundaryEdge).topology;
            HalfEdge incident = mesh.GetHalfEdgeData(boundary.TwinHalfEdge).topology;
            Vector3 edgeDirection =
                transform * ToGodot(mesh.GetVertexData(incident.SourceVertex).position)
                - transform * ToGodot(mesh.GetVertexData(boundary.SourceVertex).position);
            Vector3 faceNormal =
                orientation
                * (normalTransform * ToGodot(mesh.GetFaceData(incident.AdjacentFace).normal));
            if (edgeDirection.IsZeroApprox() || faceNormal.IsZeroApprox())
                throw new ArgumentException("A selected edge or its incident face is degenerate.");

            Vector3 outward = faceNormal.Normalized().Cross(edgeDirection.Normalized());
            if (outward.IsZeroApprox() || !outward.IsFinite())
                throw new ArgumentException("A selected edge has no finite outward direction.");

            outward = outward.Normalized();
            boundaryEdges[index] = boundaryEdge;
            AddOutward(outwardSums, boundary.SourceVertex, outward);
            AddOutward(outwardSums, incident.SourceVertex, outward);
        }

        var vertices = new int[outwardSums.Count];
        var newPositions = new NumericsVector3[outwardSums.Count];
        int vertexIndex = 0;
        foreach ((int vertex, Vector3 combined) in outwardSums)
        {
            // Shared vertex copies combine unit outward vectors so order does not fold quads.
            if (combined.IsZeroApprox() || !combined.IsFinite())
            {
                throw new ArgumentException(
                    "Selected edges have opposing extension directions at a shared vertex."
                );
            }

            Vector3 newLocal =
                inverse
                * (
                    transform * ToGodot(mesh.GetVertexData(vertex).position)
                    + combined.Normalized() * length
                );
            if (!newLocal.IsFinite())
                throw new ArgumentException("Extending the selected edges produced an invalid vertex.");

            vertices[vertexIndex] = vertex;
            newPositions[vertexIndex] = ToNumerics(newLocal);
            vertexIndex++;
        }

        return new ExtendEdgesCommand(
            meshObject,
            boundaryEdges,
            vertices,
            newPositions,
            editorSelection
        );
    }

    private static void AddOutward(
        Dictionary<int, Vector3> outwardSums,
        int vertex,
        Vector3 outward
    )
    {
        outwardSums[vertex] = outwardSums.TryGetValue(vertex, out Vector3 sum)
            ? sum + outward
            : outward;
    }

    private static Vector3 ToGodot(NumericsVector3 value) => new(value.X, value.Y, value.Z);

    private static NumericsVector3 ToNumerics(Vector3 value) => new(value.X, value.Y, value.Z);
}

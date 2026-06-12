#nullable enable

using System;
using System.Collections.Generic;
using TREditorSharp;

/// <summary>
/// Owns the reversible <see cref="TopologyPatch"/> produced by deleting a set of vertices on one
/// mesh. Deletion removes incident faces, then incident edges, then the selected vertices.
/// </summary>
public sealed class VertexDeletionBatch : IDisposable
{
    private readonly TopologyPatch _patch;
    private readonly VertexHandle[] _removedVertices;
    private readonly HalfEdgeHandle[] _removedEdges;
    private readonly FaceHandle[] _removedFaces;
    private bool _disposed;

    public EditorObjectId ObjectId { get; }

    public IReadOnlyList<VertexHandle> RemovedVertices => _removedVertices;

    /// <summary>Both half-edges of every incident edge that was removed.</summary>
    public IReadOnlyList<HalfEdgeHandle> RemovedEdges => _removedEdges;

    public IReadOnlyList<FaceHandle> RemovedFaces => _removedFaces;

    private VertexDeletionBatch(
        EditorObjectId objectId,
        TopologyPatch patch,
        VertexHandle[] removedVertices,
        HalfEdgeHandle[] removedEdges,
        FaceHandle[] removedFaces
    )
    {
        ObjectId = objectId;
        _patch = patch;
        _removedVertices = removedVertices;
        _removedEdges = removedEdges;
        _removedFaces = removedFaces;
    }

    /// <summary>
    /// Delete <paramref name="vertices"/> and all incident topology within one reversible edit.
    /// Returns <c>null</c> when no supplied vertex is live.
    /// </summary>
    public static VertexDeletionBatch? Delete(
        EditorObjectId objectId,
        SpatialMesh mesh,
        IEnumerable<VertexHandle> vertices
    )
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(vertices);

        HashSet<VertexHandle> selectedVertices = [];
        HashSet<VertexHandle> affectedVertices = [];
        HashSet<HalfEdgeHandle> seenHalfEdges = [];
        HashSet<FaceHandle> incidentFaces = [];
        List<HalfEdgeHandle> incidentEdges = [];

        foreach (VertexHandle vertex in vertices)
        {
            if (!mesh.IsVertexAlive(vertex) || !selectedVertices.Add(vertex))
                continue;

            affectedVertices.Add(vertex);
            foreach (HalfEdgeHandle edge in mesh.HalfEdgesAroundVertex(vertex))
            {
                HalfEdge halfEdge = mesh.GetHalfEdge(edge);
                HalfEdgeHandle twinHandle = halfEdge.Twin;
                if (!mesh.IsHalfEdgeAlive(twinHandle))
                    throw new InvalidOperationException(
                        $"Cannot delete vertex {vertex}: incident half-edge {edge} has no live twin."
                    );

                HalfEdge twin = mesh.GetHalfEdge(twinHandle);
                affectedVertices.Add(twin.Origin);
                AddIncidentFace(incidentFaces, mesh, halfEdge.Face);
                AddIncidentFace(incidentFaces, mesh, twin.Face);

                if (!seenHalfEdges.Add(edge))
                    continue;

                seenHalfEdges.Add(twinHandle);
                incidentEdges.Add(edge);
            }
        }

        if (selectedVertices.Count == 0)
            return null;

        List<HalfEdgeHandle> removedEdges = [];
        TopologyPatch patch;
        using (TopologyEditScope edit = mesh.BeginTopologyEdit(affectedVertices))
        {
            foreach (FaceHandle face in incidentFaces)
            {
                if (!mesh.RemoveFace(face))
                    throw new InvalidOperationException($"Failed to remove incident face {face}.");
            }

            foreach (HalfEdgeHandle edge in incidentEdges)
            {
                HalfEdgeHandle twin = mesh.GetHalfEdge(edge).Twin;
                if (!mesh.RemoveEdge(edge))
                    throw new InvalidOperationException($"Failed to remove incident edge {edge}.");

                removedEdges.Add(edge);
                removedEdges.Add(twin);
            }

            foreach (VertexHandle vertex in selectedVertices)
            {
                if (!mesh.RemoveVertex(vertex))
                    throw new InvalidOperationException(
                        $"Failed to remove vertex {vertex} after deleting its incident topology."
                    );
            }

            patch = edit.Commit();
        }

        return new VertexDeletionBatch(
            objectId,
            patch,
            [.. selectedVertices],
            removedEdges.ToArray(),
            [.. incidentFaces]
        );
    }

    public void ApplyBefore() => _patch.ApplyBefore();

    public void ApplyAfter() => _patch.ApplyAfter();

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _patch.Dispose();
    }

    private static void AddIncidentFace(
        HashSet<FaceHandle> incidentFaces,
        SpatialMesh mesh,
        FaceHandle face
    )
    {
        if (!face.IsNull && mesh.IsFaceAlive(face))
            incidentFaces.Add(face);
    }
}

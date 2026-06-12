#nullable enable

using System;
using System.Collections.Generic;
using TREditorSharp;

/// <summary>
/// Owns the reversible <see cref="TopologyPatch"/> produced by deleting a set of edges on one
/// mesh. Deletion removes each unique incident face once and then removes the selected edges as
/// ordinary forward topology operations, so undo/redo restore exact handles and every registered
/// component value through the patch rather than rerunning the operation.
/// </summary>
public sealed class EdgeDeletionBatch : IDisposable
{
    private readonly TopologyPatch _patch;
    private readonly HalfEdgeHandle[] _removedEdges;
    private readonly FaceHandle[] _removedFaces;
    private bool _disposed;

    public EditorObjectId ObjectId { get; }

    /// <summary>Both half-edges of every edge that was removed, so twin selections clear too.</summary>
    public IReadOnlyList<HalfEdgeHandle> RemovedEdges => _removedEdges;

    /// <summary>Every incident face that was removed to make the selected edges deletable.</summary>
    public IReadOnlyList<FaceHandle> RemovedFaces => _removedFaces;

    private EdgeDeletionBatch(
        EditorObjectId objectId,
        TopologyPatch patch,
        HalfEdgeHandle[] removedEdges,
        FaceHandle[] removedFaces
    )
    {
        ObjectId = objectId;
        _patch = patch;
        _removedEdges = removedEdges;
        _removedFaces = removedFaces;
    }

    /// <summary>
    /// Delete <paramref name="edges"/> (after deduplicating twin selections) on
    /// <paramref name="mesh"/> within a single topology edit. Returns <c>null</c> when nothing was
    /// removed, in which case the mesh is left unchanged.
    /// </summary>
    public static EdgeDeletionBatch? Delete(
        EditorObjectId objectId,
        SpatialMesh mesh,
        IEnumerable<HalfEdgeHandle> edges
    )
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(edges);

        // Read-only pre-pass: deduplicate twin selections into unique live edges and gather the
        // endpoints and incident faces this deletion will touch before mutating anything.
        HashSet<HalfEdgeHandle> seenHalfEdges = [];
        HashSet<VertexHandle> endpoints = [];
        HashSet<FaceHandle> incidentFaces = [];
        List<HalfEdgeHandle> uniqueEdges = [];

        foreach (HalfEdgeHandle edge in edges)
        {
            if (edge.IsNull || !mesh.IsHalfEdgeAlive(edge))
                continue;

            HalfEdge halfEdge = mesh.GetHalfEdge(edge);
            if (halfEdge.Twin.IsNull || !mesh.IsHalfEdgeAlive(halfEdge.Twin))
                continue;

            if (!seenHalfEdges.Add(edge) || !seenHalfEdges.Add(halfEdge.Twin))
                continue;

            HalfEdge twin = mesh.GetHalfEdge(halfEdge.Twin);
            uniqueEdges.Add(edge);
            endpoints.Add(halfEdge.Origin);
            endpoints.Add(twin.Origin);
            AddIncidentFace(incidentFaces, mesh, halfEdge.Face);
            AddIncidentFace(incidentFaces, mesh, twin.Face);
        }

        if (uniqueEdges.Count == 0)
            return null;

        List<HalfEdgeHandle> removedEdges = [];
        List<FaceHandle> removedFaces = [];
        TopologyPatch patch;

        using (TopologyEditScope edit = mesh.BeginTopologyEdit(endpoints))
        {
            // Adjacent faces must be removed before their shared edges so each selected edge sits
            // on a boundary loop and can be removed as a one-way operation.
            foreach (FaceHandle face in incidentFaces)
            {
                if (mesh.RemoveFace(face))
                    removedFaces.Add(face);
            }

            foreach (HalfEdgeHandle edge in uniqueEdges)
            {
                HalfEdgeHandle twin = mesh.GetHalfEdge(edge).Twin;
                if (mesh.RemoveEdge(edge))
                {
                    removedEdges.Add(edge);
                    removedEdges.Add(twin);
                }
            }

            patch = edit.Commit();
        }

        return new EdgeDeletionBatch(
            objectId,
            patch,
            removedEdges.ToArray(),
            removedFaces.ToArray()
        );
    }

    /// <summary>Restore the pre-deletion state (undo).</summary>
    public void ApplyBefore() => _patch.ApplyBefore();

    /// <summary>Re-apply the post-deletion state without rerunning the deletion (redo).</summary>
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

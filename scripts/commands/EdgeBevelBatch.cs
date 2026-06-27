#nullable enable

using System;
using System.Collections.Generic;
using TREditorSharp;

/// <summary>Owns one reversible patch containing a set of non-touching edge bevels.</summary>
public sealed class EdgeBevelBatch : IDisposable
{
    private readonly TopologyPatch _patch;
    private bool _disposed;

    public EditorObjectId ObjectId { get; }
    public IReadOnlyList<FaceHandle> BevelFaces { get; }

    private EdgeBevelBatch(EditorObjectId objectId, TopologyPatch patch, FaceHandle[] bevelFaces)
    {
        ObjectId = objectId;
        _patch = patch;
        BevelFaces = bevelFaces;
    }

    public static bool TryGetMaximumWidth(
        SpatialMesh mesh,
        IEnumerable<HalfEdgeHandle> edges,
        out float maximumWidth
    )
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(edges);
        return TryCollectEdges(mesh, edges, out _, out _, out maximumWidth);
    }

    public static EdgeBevelBatch? Bevel(
        EditorObjectId objectId,
        SpatialMesh mesh,
        IEnumerable<HalfEdgeHandle> edges,
        float width
    )
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(edges);
        if (
            !(width > 0f)
            || !float.IsFinite(width)
            || !TryCollectEdges(
                mesh,
                edges,
                out List<HalfEdgeHandle> uniqueEdges,
                out HashSet<VertexHandle> affectedVertices,
                out float maximumWidth
            )
            || width > maximumWidth
        )
        {
            return null;
        }

        List<FaceHandle> bevelFaces = [];
        TopologyPatch patch;
        using (TopologyEditScope edit = mesh.BeginTopologyEdit(affectedVertices))
        {
            foreach (HalfEdgeHandle edge in uniqueEdges)
            {
                SpatialMesh.BevelEdgeResult result = mesh.BevelEdge(edge, width);
                InitializeGeneratedUvs(mesh, result);
                bevelFaces.Add(result.BevelFace);
            }

            patch = edit.Commit();
        }

        return new EdgeBevelBatch(objectId, patch, bevelFaces.ToArray());
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

    private static bool TryCollectEdges(
        SpatialMesh mesh,
        IEnumerable<HalfEdgeHandle> edges,
        out List<HalfEdgeHandle> uniqueEdges,
        out HashSet<VertexHandle> affectedVertices,
        out float maximumWidth
    )
    {
        uniqueEdges = [];
        affectedVertices = [];
        maximumWidth = float.PositiveInfinity;
        HashSet<HalfEdgeHandle> seenHalfEdges = [];

        foreach (HalfEdgeHandle edge in edges)
        {
            if (edge.IsNull || !mesh.IsHalfEdgeAlive(edge))
                return false;

            HalfEdge data = mesh.GetHalfEdge(edge);
            if (
                data.Twin.IsNull
                || !mesh.IsHalfEdgeAlive(data.Twin)
                || seenHalfEdges.Contains(edge)
                || seenHalfEdges.Contains(data.Twin)
            )
            {
                continue;
            }

            HalfEdge twin = mesh.GetHalfEdge(data.Twin);
            if (
                !affectedVertices.Add(data.Origin)
                || !affectedVertices.Add(twin.Origin)
                || !mesh.TryGetMaximumEdgeBevelWidth(edge, out float edgeMaximumWidth)
            )
            {
                uniqueEdges.Clear();
                affectedVertices.Clear();
                maximumWidth = 0f;
                return false;
            }

            seenHalfEdges.Add(edge);
            seenHalfEdges.Add(data.Twin);
            uniqueEdges.Add(edge);
            maximumWidth = MathF.Min(maximumWidth, edgeMaximumWidth);
        }

        if (uniqueEdges.Count > 0 && maximumWidth > 0f && float.IsFinite(maximumWidth))
            return true;

        maximumWidth = 0f;
        return false;
    }

    private static void InitializeGeneratedUvs(SpatialMesh mesh, SpatialMesh.BevelEdgeResult result)
    {
        foreach (SpatialMesh.FaceReplacement replacement in result.RebuiltFaces)
        {
            if (replacement.SourceHadInitializedUvs)
                InitializeFaceUvs(mesh, replacement.ReplacementFace);
        }

        if (result.BevelFaceSourceHadInitializedUvs)
            InitializeFaceUvs(mesh, result.BevelFace);
    }

    private static void InitializeFaceUvs(SpatialMesh mesh, FaceHandle face)
    {
        List<ProjectedFaceCornerUv> projected = [];
        if (!FaceUvProjector.TryProject(mesh, face, projected))
            return;

        foreach (ProjectedFaceCornerUv corner in projected)
            mesh.SetFaceCornerUv(corner.Corner, corner.Uv);
        mesh.SetFaceUvsInitialized(face, true);
    }
}

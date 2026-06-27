#nullable enable

using System;
using System.Collections.Generic;
using TREditorSharp;

/// <summary>Owns one reversible patch containing a set of non-adjacent vertex bevels.</summary>
public sealed class VertexBevelBatch : IDisposable
{
    private readonly TopologyPatch _patch;
    private bool _disposed;

    public EditorObjectId ObjectId { get; }
    public IReadOnlyList<FaceHandle> BevelFaces { get; }

    private VertexBevelBatch(EditorObjectId objectId, TopologyPatch patch, FaceHandle[] bevelFaces)
    {
        ObjectId = objectId;
        _patch = patch;
        BevelFaces = bevelFaces;
    }

    public static bool TryGetMaximumWidth(
        SpatialMesh mesh,
        IEnumerable<VertexHandle> vertices,
        out float maximumWidth
    )
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(vertices);
        return TryCollectVertices(mesh, vertices, out _, out maximumWidth);
    }

    public static VertexBevelBatch? Bevel(
        EditorObjectId objectId,
        SpatialMesh mesh,
        IEnumerable<VertexHandle> vertices,
        float width
    )
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(vertices);
        if (
            !(width > 0f)
            || !float.IsFinite(width)
            || !TryCollectVertices(
                mesh,
                vertices,
                out List<VertexHandle> uniqueVertices,
                out float maximumWidth
            )
            || width > maximumWidth
        )
        {
            return null;
        }

        List<FaceHandle> bevelFaces = [];
        TopologyPatch patch;
        using (TopologyEditScope edit = mesh.BeginTopologyEdit(uniqueVertices))
        {
            foreach (VertexHandle vertex in uniqueVertices)
            {
                SpatialMesh.BevelVertexResult result = mesh.BevelVertex(vertex, width);
                InitializeGeneratedUvs(mesh, result);
                bevelFaces.Add(result.BevelFace);
            }

            patch = edit.Commit();
        }

        return new VertexBevelBatch(objectId, patch, bevelFaces.ToArray());
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

    private static bool TryCollectVertices(
        SpatialMesh mesh,
        IEnumerable<VertexHandle> vertices,
        out List<VertexHandle> uniqueVertices,
        out float maximumWidth
    )
    {
        uniqueVertices = [];
        maximumWidth = float.PositiveInfinity;
        HashSet<VertexHandle> selectedVertices = [];

        foreach (VertexHandle vertex in vertices)
        {
            if (vertex.IsNull || !mesh.IsVertexAlive(vertex))
                return false;
            if (!selectedVertices.Add(vertex))
                continue;
            if (!mesh.TryGetMaximumVertexBevelWidth(vertex, out float vertexMaximumWidth))
            {
                uniqueVertices.Clear();
                maximumWidth = 0f;
                return false;
            }

            uniqueVertices.Add(vertex);
            maximumWidth = MathF.Min(maximumWidth, vertexMaximumWidth);
        }

        foreach (VertexHandle vertex in uniqueVertices)
        {
            foreach (HalfEdgeHandle edge in mesh.HalfEdgesAroundVertex(vertex))
            {
                VertexHandle neighbor = mesh.GetHalfEdge(mesh.GetHalfEdge(edge).Twin).Origin;
                if (selectedVertices.Contains(neighbor))
                {
                    uniqueVertices.Clear();
                    maximumWidth = 0f;
                    return false;
                }
            }
        }

        if (uniqueVertices.Count > 0 && maximumWidth > 0f && float.IsFinite(maximumWidth))
            return true;

        maximumWidth = 0f;
        return false;
    }

    private static void InitializeGeneratedUvs(
        SpatialMesh mesh,
        SpatialMesh.BevelVertexResult result
    )
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

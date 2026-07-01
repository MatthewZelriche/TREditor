#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using TREditorSharp;

/// <summary>Owns the reversible topology patch produced by extruding one open edge.</summary>
public sealed class EdgeExtrusionChange : IDisposable
{
    private readonly TopologyPatch _patch;
    private bool _disposed;

    public EditorObjectId ObjectId { get; }
    public HalfEdgeHandle SourceEdge { get; }
    public FaceHandle Face { get; }
    public HalfEdgeHandle OuterEdge { get; }
    public IReadOnlyList<VertexHandle> NewVertices { get; }

    private EdgeExtrusionChange(
        EditorObjectId objectId,
        HalfEdgeHandle sourceEdge,
        FaceHandle face,
        HalfEdgeHandle outerEdge,
        VertexHandle[] newVertices,
        TopologyPatch patch
    )
    {
        ObjectId = objectId;
        SourceEdge = sourceEdge;
        Face = face;
        OuterEdge = outerEdge;
        NewVertices = newVertices;
        _patch = patch;
    }

    public static bool CanExtrude(SpatialMesh mesh, HalfEdgeHandle edge)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return mesh.CanExtrudeEdge(edge);
    }

    public static EdgeExtrusionChange? Extrude(
        EditorObjectId objectId,
        SpatialMesh mesh,
        HalfEdgeHandle edge,
        Vector3 delta
    )
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (!mesh.CanExtrudeEdge(edge))
            return null;

        HalfEdge source = mesh.GetHalfEdge(edge);
        VertexHandle[] affectedVertices = [source.Origin, mesh.GetHalfEdge(source.Twin).Origin];

        TopologyPatch patch;
        SpatialMesh.ExtrudeEdgeResult result;
        using (TopologyEditScope edit = mesh.BeginTopologyEdit(affectedVertices))
        {
            result = mesh.ExtrudeEdge(edge, delta);
            if (result.SourceHadInitializedUvs)
                FaceUvProjector.TryProjectAndApply(mesh, result.Face);
            patch = edit.Commit();
        }

        return new EdgeExtrusionChange(
            objectId,
            edge,
            result.Face,
            result.OuterEdge,
            result.NewVertices,
            patch
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
}

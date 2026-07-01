#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using TREditorSharp;

public sealed class FaceCollapseChange : IDisposable
{
    private readonly TopologyPatch _patch;
    private bool _disposed;

    public EditorObjectId ObjectId { get; }
    public FaceHandle SourceFace { get; }
    public VertexHandle Survivor { get; }

    private FaceCollapseChange(
        EditorObjectId objectId,
        FaceHandle sourceFace,
        VertexHandle survivor,
        TopologyPatch patch
    )
    {
        ObjectId = objectId;
        SourceFace = sourceFace;
        Survivor = survivor;
        _patch = patch;
    }

    public static bool CanCollapse(SpatialMesh mesh, FaceHandle face)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        using TopologyEditScope? edit = TryCollapse(mesh, face, out _);
        return edit != null;
    }

    public static FaceCollapseChange? Collapse(
        EditorObjectId objectId,
        SpatialMesh mesh,
        FaceHandle face
    )
    {
        ArgumentNullException.ThrowIfNull(mesh);
        using TopologyEditScope? edit = TryCollapse(mesh, face, out VertexHandle survivor);
        if (edit == null)
            return null;

        TopologyPatch patch = edit.Commit();
        return new FaceCollapseChange(objectId, face, survivor, patch);
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

    private static TopologyEditScope? TryCollapse(
        SpatialMesh mesh,
        FaceHandle face,
        out VertexHandle survivor
    )
    {
        survivor = VertexHandle.Null;
        if (!mesh.IsFaceAlive(face))
            return null;

        List<VertexHandle> vertices = [];
        foreach (HalfEdgeHandle edge in mesh.HalfEdgesAroundFace(face))
            vertices.Add(mesh.GetHalfEdge(edge).Origin);
        if (vertices.Count < 3)
            return null;

        Vector3 centroid = mesh.ComputeFaceCentroid(face);
        TopologyEditScope edit = mesh.BeginTopologyEdit(vertices);
        survivor = vertices[0];
        for (int i = 1; i < vertices.Count; i++)
        {
            HalfEdgeHandle edge = mesh.FindHalfEdge(survivor, vertices[i]);
            if (
                edge.IsNull
                || !mesh.TryCollapseEdge(edge, out VertexHandle collapsedSurvivor, 0f)
                || collapsedSurvivor != survivor
            )
            {
                edit.Dispose();
                survivor = VertexHandle.Null;
                return null;
            }
        }

        mesh.SetVertexPosition(survivor, centroid);
        FaceUvProjector.ReprojectInitializedFacesAroundVertices(mesh, [survivor]);
        return edit;
    }
}

#nullable enable

using System;
using System.Collections.Generic;
using TREditorSharp;

public sealed class BridgeEdgesChange : IDisposable
{
    private readonly TopologyPatch _patch;
    private bool _disposed;

    public EditorObjectId ObjectId { get; }
    public IReadOnlyList<FaceHandle> Faces { get; }

    private BridgeEdgesChange(EditorObjectId objectId, FaceHandle[] faces, TopologyPatch patch)
    {
        ObjectId = objectId;
        Faces = faces;
        _patch = patch;
    }

    public static bool CanBridge(SpatialMesh mesh, HalfEdgeHandle first, HalfEdgeHandle second)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return mesh.CanBridgeEdges(first, second);
    }

    public static BridgeEdgesChange? Bridge(
        EditorObjectId objectId,
        SpatialMesh mesh,
        HalfEdgeHandle first,
        HalfEdgeHandle second,
        int segments,
        float archAngleDegrees
    )
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (
            segments < 1
            || archAngleDegrees < 0f
            || archAngleDegrees > 180f
            || !float.IsFinite(archAngleDegrees)
            || !mesh.CanBridgeEdges(first, second)
        )
        {
            return null;
        }

        HalfEdge firstData = mesh.GetHalfEdge(first);
        HalfEdge secondData = mesh.GetHalfEdge(second);
        VertexHandle[] affectedVertices =
        [
            firstData.Origin,
            mesh.GetHalfEdge(firstData.Twin).Origin,
            secondData.Origin,
            mesh.GetHalfEdge(secondData.Twin).Origin,
        ];

        TopologyPatch patch;
        SpatialMesh.BridgeEdgesResult result;
        using (TopologyEditScope edit = mesh.BeginTopologyEdit(affectedVertices))
        {
            result = mesh.BridgeEdges(first, second, segments, archAngleDegrees);
            if (result.SourceHadInitializedUvs)
            {
                foreach (FaceHandle face in result.Faces)
                    FaceUvProjector.TryProjectAndApply(mesh, face);
            }
            patch = edit.Commit();
        }

        return new BridgeEdgesChange(objectId, result.Faces, patch);
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

#nullable enable

using System;
using System.Collections.Generic;
using TREditorSharp;

public sealed class FillHoleChange : IDisposable
{
    private readonly TopologyPatch _patch;
    private bool _disposed;

    public EditorObjectId ObjectId { get; }
    public FaceHandle Face { get; }

    private FillHoleChange(EditorObjectId objectId, FaceHandle face, TopologyPatch patch)
    {
        ObjectId = objectId;
        Face = face;
        _patch = patch;
    }

    public static FillHoleChange? Fill(
        EditorObjectId objectId,
        SpatialMesh mesh,
        HalfEdgeHandle edge
    )
    {
        ArgumentNullException.ThrowIfNull(mesh);
        List<VertexHandle> boundaryVertices = [];
        if (!mesh.TryGetHoleBoundaryVertices(edge, boundaryVertices))
            return null;

        TopologyPatch patch;
        FaceHandle face;
        using (TopologyEditScope edit = mesh.BeginTopologyEdit(boundaryVertices))
        {
            if (!mesh.TryFillHole(edge, out face))
                return null;

            FaceUvProjector.TryProjectAndApply(mesh, face);
            patch = edit.Commit();
        }

        return new FillHoleChange(objectId, face, patch);
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

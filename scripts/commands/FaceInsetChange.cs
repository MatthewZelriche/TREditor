#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TREditorSharp;

public sealed class FaceInsetChange : IDisposable
{
    private readonly TopologyPatch _patch;
    private bool _disposed;

    public EditorObjectId ObjectId { get; }
    public FaceHandle SourceFace { get; }
    public FaceHandle CapFace { get; }
    public IReadOnlyList<FaceHandle> RingFaces { get; }

    private FaceInsetChange(
        EditorObjectId objectId,
        FaceHandle sourceFace,
        FaceHandle capFace,
        FaceHandle[] ringFaces,
        TopologyPatch patch
    )
    {
        ObjectId = objectId;
        SourceFace = sourceFace;
        CapFace = capFace;
        RingFaces = ringFaces;
        _patch = patch;
    }

    public static FaceInsetChange? Inset(
        EditorObjectId objectId,
        SpatialMesh mesh,
        FaceHandle face,
        float depth
    )
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (!mesh.IsFaceAlive(face) || !(depth > 0f) || depth > mesh.ComputeMaximumInsetDepth(face))
            return null;

        bool initializeGeneratedUvs = mesh.AreFaceUvsInitialized(face);
        List<VertexHandle> affectedVertices = [];
        foreach (HalfEdgeHandle edge in mesh.HalfEdgesAroundFace(face))
            affectedVertices.Add(mesh.GetHalfEdge(edge).Origin);

        TopologyPatch patch;
        SpatialMesh.InsetFaceResult result;
        using (TopologyEditScope edit = mesh.BeginTopologyEdit(affectedVertices))
        {
            result = mesh.InsetFace(face, depth);
            if (initializeGeneratedUvs)
            {
                foreach (FaceHandle generatedFace in result.RingFaces.Append(result.CapFace))
                    FaceUvProjector.TryProjectAndApply(mesh, generatedFace);
            }

            patch = edit.Commit();
        }

        return new FaceInsetChange(objectId, face, result.CapFace, result.RingFaces, patch);
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

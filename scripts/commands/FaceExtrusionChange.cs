#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using TREditorSharp;

/// <summary>
/// Owns the reversible topology patch produced by extruding one face and translating its cap.
/// </summary>
public sealed class FaceExtrusionChange : IDisposable
{
    private readonly TopologyPatch _patch;
    private bool _disposed;

    public EditorObjectId ObjectId { get; }
    public FaceHandle SourceFace { get; }
    public FaceHandle CapFace { get; }
    public IReadOnlyList<FaceHandle> SideFaces { get; }
    public IReadOnlyList<VertexHandle> CapVertices { get; }

    private FaceExtrusionChange(
        EditorObjectId objectId,
        FaceHandle sourceFace,
        FaceHandle capFace,
        FaceHandle[] sideFaces,
        VertexHandle[] capVertices,
        TopologyPatch patch
    )
    {
        ObjectId = objectId;
        SourceFace = sourceFace;
        CapFace = capFace;
        SideFaces = sideFaces;
        CapVertices = capVertices;
        _patch = patch;
    }

    public static FaceExtrusionChange? Extrude(
        EditorObjectId objectId,
        SpatialMesh mesh,
        FaceHandle face,
        Vector3 capDelta
    )
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (!mesh.IsFaceAlive(face))
            return null;

        bool initializeGeneratedUvs = mesh.AreFaceUvsInitialized(face);
        List<VertexHandle> affectedVertices = [];
        foreach (HalfEdgeHandle edge in mesh.HalfEdgesAroundFace(face))
            affectedVertices.Add(mesh.GetHalfEdge(edge).Origin);

        TopologyPatch patch;
        SpatialMesh.ExtrudeFaceResult result;
        using (TopologyEditScope edit = mesh.BeginTopologyEdit(affectedVertices))
        {
            result = mesh.ExtrudeFace(face, 0f);
            foreach (VertexHandle vertex in result.NewVertices)
                mesh.SetVertexPosition(vertex, mesh.GetVertexPosition(vertex) + capDelta);

            if (initializeGeneratedUvs)
                InitializeGeneratedUvs(mesh, result);

            patch = edit.Commit();
        }

        return new FaceExtrusionChange(
            objectId,
            face,
            result.CapFace,
            result.SideFaces,
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

    private static void InitializeGeneratedUvs(
        SpatialMesh mesh,
        SpatialMesh.ExtrudeFaceResult result
    )
    {
        List<ProjectedFaceCornerUv> projected = [];
        foreach (FaceHandle face in result.SideFaces)
            InitializeFaceUvs(mesh, face, projected);
        InitializeFaceUvs(mesh, result.CapFace, projected);
    }

    private static void InitializeFaceUvs(
        SpatialMesh mesh,
        FaceHandle face,
        List<ProjectedFaceCornerUv> projected
    )
    {
        projected.Clear();
        if (!FaceUvProjector.TryProject(mesh, face, projected))
            return;

        foreach (ProjectedFaceCornerUv corner in projected)
        {
            mesh.SetFaceCornerUv(corner.Corner, corner.Uv);
        }
        mesh.SetFaceUvsInitialized(face, true);
    }
}

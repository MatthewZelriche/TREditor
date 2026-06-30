#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using TREditorSharp;

public sealed class EdgeCutChange : IDisposable
{
    public const float MinimumEdgeParameter = 0.0001f;

    private readonly TopologyPatch _patch;
    private bool _disposed;

    public EditorObjectId ObjectId { get; }
    public FaceHandle SourceFace { get; }
    public VertexHandle FirstVertex { get; }
    public VertexHandle SecondVertex { get; }
    public HalfEdgeHandle CutEdge { get; }
    public IReadOnlyList<FaceHandle> Faces { get; }

    private EdgeCutChange(
        EditorObjectId objectId,
        FaceHandle sourceFace,
        VertexHandle firstVertex,
        VertexHandle secondVertex,
        HalfEdgeHandle cutEdge,
        FaceHandle[] faces,
        TopologyPatch patch
    )
    {
        ObjectId = objectId;
        SourceFace = sourceFace;
        FirstVertex = firstVertex;
        SecondVertex = secondVertex;
        CutEdge = cutEdge;
        Faces = faces;
        _patch = patch;
    }

    public static bool CanCut(
        SpatialMesh mesh,
        FaceHandle face,
        HalfEdgeHandle firstEdge,
        float firstParameter,
        HalfEdgeHandle secondEdge,
        float secondParameter
    )
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (
            !TryResolveCutLocation(mesh, face, firstEdge, firstParameter, out CutLocation first)
            || !TryResolveCutLocation(
                mesh,
                face,
                secondEdge,
                secondParameter,
                out CutLocation second
            )
        )
        {
            return false;
        }

        if (first.FaceEdge == second.FaceEdge || AreSameOrAdjacent(mesh, first, second))
            return false;

        return first.ExistingVertex.IsNull
            || second.ExistingVertex.IsNull
            || !AreVerticesConnected(mesh, first.ExistingVertex, second.ExistingVertex);
    }

    public static EdgeCutChange? Cut(
        EditorObjectId objectId,
        SpatialMesh mesh,
        FaceHandle face,
        HalfEdgeHandle firstEdge,
        float firstParameter,
        HalfEdgeHandle secondEdge,
        float secondParameter
    )
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (!CanCut(mesh, face, firstEdge, firstParameter, secondEdge, secondParameter))
        {
            return null;
        }

        TryResolveCutLocation(mesh, face, firstEdge, firstParameter, out CutLocation first);
        TryResolveCutLocation(mesh, face, secondEdge, secondParameter, out CutLocation second);
        int materialSlot = mesh.GetFaceMaterialSlot(face);
        bool hadInitializedUvs = mesh.AreFaceUvsInitialized(face);

        List<VertexHandle> affectedVertices = [];
        foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(face))
            affectedVertices.Add(mesh.GetHalfEdge(corner).Origin);

        TopologyPatch patch;
        VertexHandle firstVertex;
        VertexHandle secondVertex;
        HalfEdgeHandle cutEdge;
        FaceHandle[] faces;
        using (TopologyEditScope edit = mesh.BeginTopologyEdit(affectedVertices))
        {
            firstVertex = ResolveVertex(mesh, first);
            secondVertex = ResolveVertex(mesh, second);

            Dictionary<VertexHandle, Vector2> sourceUvs = [];
            FaceCornerHandle firstCorner = FaceCornerHandle.Null;
            FaceCornerHandle secondCorner = FaceCornerHandle.Null;
            foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(face))
            {
                VertexHandle vertex = mesh.GetHalfEdge(corner).Origin;
                sourceUvs[vertex] = mesh.GetFaceCornerUv(corner);
                if (vertex == firstVertex)
                    firstCorner = corner;
                else if (vertex == secondVertex)
                    secondCorner = corner;
            }

            (FaceHandle firstFace, FaceHandle secondFace) = mesh.SplitFace(
                firstCorner,
                secondCorner
            );
            faces = [firstFace, secondFace];
            foreach (FaceHandle splitFace in faces)
            {
                mesh.SetFaceMaterialSlot(splitFace, materialSlot);
                foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(splitFace))
                {
                    VertexHandle vertex = mesh.GetHalfEdge(corner).Origin;
                    mesh.SetFaceCornerUv(corner, sourceUvs[vertex]);
                }
                mesh.SetFaceUvsInitialized(splitFace, hadInitializedUvs);
            }

            cutEdge = FindDirectedEdge(mesh, firstVertex, secondVertex);
            patch = edit.Commit();
        }

        return new EdgeCutChange(objectId, face, firstVertex, secondVertex, cutEdge, faces, patch);
    }

    public static bool TryGetFaceEdge(
        SpatialMesh mesh,
        FaceHandle face,
        HalfEdgeHandle edge,
        out HalfEdgeHandle faceEdge
    )
    {
        faceEdge = HalfEdgeHandle.Null;
        if (!mesh.IsFaceAlive(face) || !mesh.IsHalfEdgeAlive(edge))
            return false;

        HalfEdge data = mesh.GetHalfEdge(edge);
        if (data.Face == face)
        {
            faceEdge = edge;
            return true;
        }
        if (mesh.IsHalfEdgeAlive(data.Twin) && mesh.GetHalfEdge(data.Twin).Face == face)
        {
            faceEdge = data.Twin;
            return true;
        }
        return false;
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

    private static bool IsInteriorParameter(float parameter) =>
        float.IsFinite(parameter)
        && parameter >= MinimumEdgeParameter
        && parameter <= 1f - MinimumEdgeParameter;

    private static bool TryResolveCutLocation(
        SpatialMesh mesh,
        FaceHandle face,
        HalfEdgeHandle edge,
        float parameter,
        out CutLocation location
    )
    {
        location = default;
        if (
            !(parameter == 0f || parameter == 1f || IsInteriorParameter(parameter))
            || !TryGetFaceEdge(mesh, face, edge, out HalfEdgeHandle faceEdge)
        )
        {
            return false;
        }

        float faceParameter = edge == faceEdge ? parameter : 1f - parameter;
        VertexHandle existingVertex = VertexHandle.Null;
        if (faceParameter == 0f)
        {
            existingVertex = mesh.GetHalfEdge(faceEdge).Origin;
        }
        else if (faceParameter == 1f)
        {
            HalfEdgeHandle next = mesh.GetHalfEdge(faceEdge).Next;
            existingVertex = mesh.GetHalfEdge(next).Origin;
        }

        location = new CutLocation(faceEdge, faceParameter, existingVertex);
        return true;
    }

    private static VertexHandle ResolveVertex(SpatialMesh mesh, CutLocation location) =>
        location.ExistingVertex.IsNull
            ? mesh.SplitEdge(location.FaceEdge, location.Parameter)
            : location.ExistingVertex;

    private static bool AreSameOrAdjacent(SpatialMesh mesh, CutLocation first, CutLocation second)
    {
        if (!first.ExistingVertex.IsNull && !second.ExistingVertex.IsNull)
        {
            if (first.ExistingVertex == second.ExistingVertex)
                return true;

            return IsIncident(mesh, first.FaceEdge, second.ExistingVertex)
                || IsIncident(mesh, second.FaceEdge, first.ExistingVertex);
        }

        if (!first.ExistingVertex.IsNull)
            return IsIncident(mesh, second.FaceEdge, first.ExistingVertex);
        if (!second.ExistingVertex.IsNull)
            return IsIncident(mesh, first.FaceEdge, second.ExistingVertex);
        return false;
    }

    private static bool IsIncident(SpatialMesh mesh, HalfEdgeHandle faceEdge, VertexHandle vertex)
    {
        HalfEdge edge = mesh.GetHalfEdge(faceEdge);
        return edge.Origin == vertex || mesh.GetHalfEdge(edge.Next).Origin == vertex;
    }

    private static bool AreVerticesConnected(
        SpatialMesh mesh,
        VertexHandle first,
        VertexHandle second
    )
    {
        foreach (HalfEdgeHandle edge in mesh.EnumerateLiveHalfEdges())
        {
            HalfEdge data = mesh.GetHalfEdge(edge);
            if (
                data.Origin == first
                && mesh.IsHalfEdgeAlive(data.Twin)
                && mesh.GetHalfEdge(data.Twin).Origin == second
            )
            {
                return true;
            }
        }
        return false;
    }

    private static HalfEdgeHandle FindDirectedEdge(
        SpatialMesh mesh,
        VertexHandle origin,
        VertexHandle destination
    )
    {
        foreach (HalfEdgeHandle edge in mesh.EnumerateLiveHalfEdges())
        {
            HalfEdge data = mesh.GetHalfEdge(edge);
            if (
                data.Origin == origin
                && mesh.IsHalfEdgeAlive(data.Twin)
                && mesh.GetHalfEdge(data.Twin).Origin == destination
            )
            {
                return edge;
            }
        }
        return HalfEdgeHandle.Null;
    }

    private readonly record struct CutLocation(
        HalfEdgeHandle FaceEdge,
        float Parameter,
        VertexHandle ExistingVertex
    );
}

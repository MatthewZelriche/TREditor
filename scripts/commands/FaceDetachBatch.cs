#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TREditorSharp;

/// <summary>
/// Owns the reversible topology patch produced by separating a selected face region from one mesh.
/// Faces in the region continue sharing duplicated vertices and edges with each other.
/// </summary>
public sealed class FaceDetachBatch : IDisposable
{
    private readonly TopologyPatch _patch;
    private bool _disposed;

    public EditorObjectId ObjectId { get; }
    public IReadOnlyList<FaceHandle> DetachedFaces { get; }

    private FaceDetachBatch(
        EditorObjectId objectId,
        TopologyPatch patch,
        FaceHandle[] detachedFaces
    )
    {
        ObjectId = objectId;
        _patch = patch;
        DetachedFaces = detachedFaces;
    }

    public static bool CanDetach(SpatialMesh mesh, IEnumerable<FaceHandle> faces)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(faces);

        bool hasFace = false;
        HashSet<FaceHandle> uniqueFaces = [];
        foreach (FaceHandle face in faces)
        {
            if (face.IsNull || !mesh.IsFaceAlive(face))
                return false;
            hasFace |= uniqueFaces.Add(face);
        }
        return hasFace;
    }

    public static FaceDetachBatch? Detach(
        EditorObjectId objectId,
        SpatialMesh mesh,
        IEnumerable<FaceHandle> faces
    )
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(faces);

        List<DetachedFaceSnapshot> snapshots = [];
        HashSet<FaceHandle> uniqueFaces = [];
        HashSet<VertexHandle> affectedVertices = [];
        HashSet<HalfEdgeHandle> seenEdges = [];
        List<HalfEdgeHandle> regionEdges = [];
        Dictionary<VertexHandle, Vector3> sourcePositions = [];
        foreach (FaceHandle face in faces)
        {
            if (face.IsNull || !mesh.IsFaceAlive(face))
                return null;
            if (!uniqueFaces.Add(face))
                continue;

            List<VertexHandle> vertices = [];
            List<Vector2> uvs = [];
            foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(face))
            {
                HalfEdge data = mesh.GetHalfEdge(corner);
                vertices.Add(data.Origin);
                uvs.Add(mesh.GetFaceCornerUv(corner));
                affectedVertices.Add(data.Origin);
                sourcePositions.TryAdd(data.Origin, mesh.GetVertexPosition(data.Origin));

                if (seenEdges.Add(corner) && seenEdges.Add(data.Twin))
                    regionEdges.Add(corner);
            }
            snapshots.Add(
                new DetachedFaceSnapshot(
                    face,
                    vertices.ToArray(),
                    uvs.ToArray(),
                    mesh.GetFaceMaterialSlot(face),
                    mesh.AreFaceUvsInitialized(face)
                )
            );
        }

        if (snapshots.Count == 0)
            return null;

        // Vertex-touching face islands need separate duplicates or they form a bow-tie vertex.
        Dictionary<FaceHandle, int> componentByFace = FindEdgeConnectedComponents(
            mesh,
            uniqueFaces
        );
        TopologyPatch patch;
        FaceHandle[] detachedFaces = new FaceHandle[snapshots.Count];
        using (TopologyEditScope edit = mesh.BeginTopologyEdit(affectedVertices))
        {
            foreach (DetachedFaceSnapshot snapshot in snapshots)
                mesh.RemoveFace(snapshot.SourceFace);

            foreach (HalfEdgeHandle edge in regionEdges)
            {
                if (!mesh.IsHalfEdgeAlive(edge))
                    continue;
                HalfEdge data = mesh.GetHalfEdge(edge);
                if (
                    !mesh.IsFaceAlive(data.Face)
                    && !mesh.IsFaceAlive(mesh.GetHalfEdge(data.Twin).Face)
                )
                {
                    mesh.RemoveEdge(edge);
                }
            }

            foreach (VertexHandle vertex in affectedVertices)
                mesh.RemoveVertex(vertex);

            Dictionary<(int Component, VertexHandle Source), VertexHandle> duplicates = [];
            foreach (DetachedFaceSnapshot snapshot in snapshots)
            {
                int component = componentByFace[snapshot.SourceFace];
                foreach (VertexHandle source in snapshot.Vertices)
                {
                    (int Component, VertexHandle Source) key = (component, source);
                    if (!duplicates.ContainsKey(key))
                        duplicates.Add(key, mesh.AddVertex(sourcePositions[source]));
                }
            }

            for (int index = 0; index < snapshots.Count; index++)
            {
                DetachedFaceSnapshot snapshot = snapshots[index];
                int component = componentByFace[snapshot.SourceFace];
                VertexHandle[] detachedVertices = snapshot
                    .Vertices.Select(vertex => duplicates[(component, vertex)])
                    .ToArray();
                FaceHandle detachedFace = mesh.AddFace(detachedVertices);
                mesh.SetFaceMaterialSlot(detachedFace, snapshot.MaterialSlot);
                int cornerIndex = 0;
                foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(detachedFace))
                    mesh.SetFaceCornerUv(corner, snapshot.Uvs[cornerIndex++]);
                mesh.SetFaceUvsInitialized(detachedFace, snapshot.HadInitializedUvs);
                detachedFaces[index] = detachedFace;
            }

            patch = edit.Commit();
        }

        return new FaceDetachBatch(objectId, patch, detachedFaces);
    }

    private static Dictionary<FaceHandle, int> FindEdgeConnectedComponents(
        SpatialMesh mesh,
        HashSet<FaceHandle> selectedFaces
    )
    {
        Dictionary<FaceHandle, int> componentByFace = [];
        Queue<FaceHandle> pending = [];
        int component = 0;
        foreach (FaceHandle seed in selectedFaces)
        {
            if (!componentByFace.TryAdd(seed, component))
                continue;

            pending.Enqueue(seed);
            while (pending.Count > 0)
            {
                FaceHandle face = pending.Dequeue();
                foreach (HalfEdgeHandle edge in mesh.HalfEdgesAroundFace(face))
                {
                    FaceHandle adjacent = mesh.GetHalfEdge(mesh.GetHalfEdge(edge).Twin).Face;
                    if (
                        selectedFaces.Contains(adjacent)
                        && componentByFace.TryAdd(adjacent, component)
                    )
                    {
                        pending.Enqueue(adjacent);
                    }
                }
            }
            component++;
        }

        return componentByFace;
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

    private readonly record struct DetachedFaceSnapshot(
        FaceHandle SourceFace,
        VertexHandle[] Vertices,
        Vector2[] Uvs,
        int MaterialSlot,
        bool HadInitializedUvs
    );
}

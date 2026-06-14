using System;
using System.Collections.Generic;
using Godot;
using TREditorSharp;
using NumericVector3 = System.Numerics.Vector3;

// Centralizes committed scene-node mutation for now. This is intentionally a step toward
// a proper editor document/model layer, where commands would mutate model state and the
// Godot scene would become a synchronized view of that model.
public sealed class EditorSceneService : IDisposable
{
    private readonly Node3D _worldRoot;
    private readonly TextureMaterialLibrary _textureMaterials;
    private readonly Dictionary<EditorObjectId, TRMeshGD> _meshNodes = [];

    private bool _disposed;

    public EditorSceneService(Node3D worldRoot, TextureMaterialLibrary textureMaterials)
    {
        ArgumentNullException.ThrowIfNull(worldRoot);
        ArgumentNullException.ThrowIfNull(textureMaterials);

        _worldRoot = worldRoot;
        _textureMaterials = textureMaterials;
    }

    public void CreateMeshObject(EditorObjectId objectId, SpatialMesh mesh, string displayName)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (_meshNodes.TryGetValue(objectId, out TRMeshGD existingNode))
        {
            existingNode.ObjectId = objectId;
            if (existingNode.GetParent() == null)
            {
                _worldRoot.AddChild(existingNode);
            }

            return;
        }

        TRMeshGD meshNode = new() { Name = displayName, ObjectId = objectId };
        meshNode.SetTextureMaterialLibrary(_textureMaterials);
        meshNode.TakeMesh(mesh);
        _meshNodes.Add(objectId, meshNode);
        _worldRoot.AddChild(meshNode);
    }

    /// <summary>
    /// Creates a mesh object and restores its local transform. Used when rebuilding a loaded
    /// document, where each object's placement is persisted alongside its geometry.
    /// </summary>
    public void CreateMeshObject(
        EditorObjectId objectId,
        SpatialMesh mesh,
        string displayName,
        Transform3D transform
    )
    {
        CreateMeshObject(objectId, mesh, displayName);
        if (_meshNodes.TryGetValue(objectId, out TRMeshGD meshNode))
        {
            meshNode.Transform = transform;
        }
    }

    /// <summary>
    /// Removes and frees every mesh object, leaving an empty scene. Unlike <see cref="Dispose"/>,
    /// the service stays usable, so this is the reset step for loading or starting a new document.
    /// </summary>
    public void ClearAll()
    {
        foreach (TRMeshGD meshNode in _meshNodes.Values)
        {
            Node parent = meshNode.GetParent();
            parent?.RemoveChild(meshNode);
            meshNode.QueueFree();
        }

        _meshNodes.Clear();
    }

    public void RemoveMeshObject(EditorObjectId objectId)
    {
        if (!_meshNodes.TryGetValue(objectId, out TRMeshGD meshNode))
        {
            return;
        }

        Node parent = meshNode.GetParent();
        parent?.RemoveChild(meshNode);
    }

    public void RestoreMeshObject(EditorObjectId objectId)
    {
        if (_meshNodes.TryGetValue(objectId, out TRMeshGD meshNode) && meshNode.GetParent() == null)
        {
            _worldRoot.AddChild(meshNode);
        }
    }

    public IEnumerable<KeyValuePair<EditorObjectId, TRMeshGD>> EnumerateMeshObjects() => _meshNodes;

    public bool TryGetSelectionCenter(SelectionSnapshot selection, out Vector3 center)
    {
        center = Vector3.Zero;
        if (selection.IsEmpty)
        {
            return false;
        }

        int count = 0;
        foreach (SelectionTarget target in selection.Targets)
        {
            if (!TryGetSelectionTargetCenter(target, out Vector3 targetCenter))
            {
                continue;
            }

            center += targetCenter;
            count++;
        }

        if (count == 0)
        {
            center = Vector3.Zero;
            return false;
        }

        center /= count;
        return true;
    }

    public bool TryGetFaceWorldNormal(SelectionTarget target, out Vector3 normal)
    {
        normal = Vector3.Zero;
        if (
            target.Kind != ScenePickElementKind.Face
            || !_meshNodes.TryGetValue(target.ObjectId, out TRMeshGD meshNode)
        )
        {
            return false;
        }

        Vector3 localNormal = ToGodotVector3(meshNode.SourceMesh.ComputeFaceNormal(target.Face));
        normal = (meshNode.GlobalTransform.Basis.Inverse().Transposed() * localNormal).Normalized();
        return !normal.IsZeroApprox();
    }

    public void TranslateSelection(SelectionSnapshot selection, Vector3 worldDelta)
    {
        if (selection.IsEmpty || worldDelta.IsZeroApprox())
        {
            return;
        }

        HashSet<EditorObjectId> translatedObjects = [];
        Dictionary<EditorObjectId, HashSet<VertexHandle>> componentVertices = [];

        foreach (SelectionTarget target in selection.Targets)
        {
            if (target.Kind == ScenePickElementKind.Object)
            {
                if (_meshNodes.TryGetValue(target.ObjectId, out TRMeshGD meshNode))
                {
                    meshNode.GlobalPosition += worldDelta;
                    translatedObjects.Add(target.ObjectId);
                }

                continue;
            }

            if (translatedObjects.Contains(target.ObjectId))
            {
                continue;
            }

            if (!_meshNodes.TryGetValue(target.ObjectId, out TRMeshGD componentMeshNode))
            {
                continue;
            }

            if (!componentVertices.TryGetValue(target.ObjectId, out HashSet<VertexHandle> vertices))
            {
                vertices = [];
                componentVertices[target.ObjectId] = vertices;
            }

            AddTargetVertices(componentMeshNode.SourceMesh, target, vertices);
        }

        foreach ((EditorObjectId objectId, HashSet<VertexHandle> vertices) in componentVertices)
        {
            if (translatedObjects.Contains(objectId) || vertices.Count == 0)
            {
                continue;
            }

            if (!_meshNodes.TryGetValue(objectId, out TRMeshGD meshNode))
            {
                continue;
            }

            Vector3 localDelta = meshNode.GlobalTransform.Basis.Inverse() * worldDelta;
            NumericVector3 numericDelta = ToNumericVector3(localDelta);
            SpatialMesh mesh = meshNode.SourceMesh;

            foreach (VertexHandle vertex in vertices)
            {
                mesh.SetVertexPosition(vertex, mesh.GetVertexPosition(vertex) + numericDelta);
            }

            FaceUvProjector.ReprojectInitializedFacesAroundVertices(mesh, vertices);
            meshNode.Rebuild();
        }
    }

    public FaceExtrusionChange ExtrudeFace(SelectionTarget target, Vector3 worldDelta)
    {
        if (
            target.Kind != ScenePickElementKind.Face
            || worldDelta.IsZeroApprox()
            || !_meshNodes.TryGetValue(target.ObjectId, out TRMeshGD meshNode)
        )
        {
            return null;
        }

        Vector3 localDelta = meshNode.GlobalTransform.Basis.Inverse() * worldDelta;
        FaceExtrusionChange change = FaceExtrusionChange.Extrude(
            target.ObjectId,
            meshNode.SourceMesh,
            target.Face,
            ToNumericVector3(localDelta)
        );
        if (change != null)
            meshNode.Rebuild();
        return change;
    }

    public void ApplyFaceExtrusionBefore(FaceExtrusionChange change) =>
        ApplyFaceExtrusion(change, before: true);

    public void ApplyFaceExtrusionAfter(FaceExtrusionChange change) =>
        ApplyFaceExtrusion(change, before: false);

    private void ApplyFaceExtrusion(FaceExtrusionChange change, bool before)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (!_meshNodes.TryGetValue(change.ObjectId, out TRMeshGD meshNode))
            return;

        if (before)
            change.ApplyBefore();
        else
            change.ApplyAfter();
        meshNode.Rebuild();
    }

    public bool ApplyFaceTexture(EditorObjectId objectId, FaceTextureChange change, bool revert)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (!_meshNodes.TryGetValue(objectId, out TRMeshGD meshNode))
            return false;

        if (revert)
            change.Revert(meshNode.SourceMesh);
        else
            change.Apply(meshNode.SourceMesh);
        meshNode.RebuildRender();
        return true;
    }

    public FaceDeletionChange[] CaptureFaceDeletions(IReadOnlyList<SelectionTarget> targets)
    {
        List<FaceDeletionChange> changes = [];
        foreach (SelectionTarget target in targets)
        {
            if (
                target.Kind == ScenePickElementKind.Face
                && _meshNodes.TryGetValue(target.ObjectId, out TRMeshGD meshNode)
                && FaceDeletionChange.Capture(target.ObjectId, meshNode.SourceMesh, target.Face)
                    is FaceDeletionChange change
            )
            {
                changes.Add(change);
            }
        }

        return changes.ToArray();
    }

    public void DeleteFaces(IReadOnlyList<FaceDeletionChange> changes)
    {
        HashSet<EditorObjectId> changedObjects = [];
        foreach (FaceDeletionChange change in changes)
        {
            if (
                _meshNodes.TryGetValue(change.ObjectId, out TRMeshGD meshNode)
                && change.Delete(meshNode.SourceMesh)
            )
            {
                changedObjects.Add(change.ObjectId);
            }
        }

        RebuildObjects(changedObjects);
    }

    public void RestoreFaces(IReadOnlyList<FaceDeletionChange> changes)
    {
        HashSet<EditorObjectId> changedObjects = [];
        for (int i = changes.Count - 1; i >= 0; i--)
        {
            FaceDeletionChange change = changes[i];
            if (!_meshNodes.TryGetValue(change.ObjectId, out TRMeshGD meshNode))
                continue;

            change.Restore(meshNode.SourceMesh);
            changedObjects.Add(change.ObjectId);
        }

        RebuildObjects(changedObjects);
    }

    /// <summary>
    /// Delete the selected edges, grouped per mesh, through reversible topology patches. Each
    /// affected mesh is rebuilt once.
    /// </summary>
    public EdgeDeletionBatch[] DeleteEdges(IReadOnlyList<SelectionTarget> targets)
    {
        Dictionary<EditorObjectId, List<HalfEdgeHandle>> edgesByObject = [];
        foreach (SelectionTarget target in targets)
        {
            if (
                target.Kind != ScenePickElementKind.Edge
                || !_meshNodes.ContainsKey(target.ObjectId)
            )
            {
                continue;
            }

            if (!edgesByObject.TryGetValue(target.ObjectId, out List<HalfEdgeHandle> edges))
            {
                edges = [];
                edgesByObject[target.ObjectId] = edges;
            }

            edges.Add(target.Edge);
        }

        List<EdgeDeletionBatch> batches = [];
        HashSet<EditorObjectId> changedObjects = [];
        foreach ((EditorObjectId objectId, List<HalfEdgeHandle> edges) in edgesByObject)
        {
            TRMeshGD meshNode = _meshNodes[objectId];
            if (
                EdgeDeletionBatch.Delete(objectId, meshNode.SourceMesh, edges)
                is EdgeDeletionBatch batch
            )
            {
                batches.Add(batch);
                changedObjects.Add(objectId);
            }
        }

        RebuildObjects(changedObjects);
        return batches.ToArray();
    }

    /// <summary>Apply the before-state of every edge-deletion patch (undo) and rebuild once.</summary>
    public void ApplyEdgeDeletionBefore(IReadOnlyList<EdgeDeletionBatch> batches) =>
        ApplyEdgeDeletion(batches, before: true);

    /// <summary>Apply the after-state of every edge-deletion patch (redo) and rebuild once.</summary>
    public void ApplyEdgeDeletionAfter(IReadOnlyList<EdgeDeletionBatch> batches) =>
        ApplyEdgeDeletion(batches, before: false);

    private void ApplyEdgeDeletion(IReadOnlyList<EdgeDeletionBatch> batches, bool before)
    {
        HashSet<EditorObjectId> changedObjects = [];
        foreach (EdgeDeletionBatch batch in batches)
        {
            if (!_meshNodes.ContainsKey(batch.ObjectId))
                continue;

            if (before)
                batch.ApplyBefore();
            else
                batch.ApplyAfter();
            changedObjects.Add(batch.ObjectId);
        }

        RebuildObjects(changedObjects);
    }

    /// <summary>
    /// Delete selected vertices, grouped per mesh, together with all incident edges and faces.
    /// Each affected mesh is rebuilt once.
    /// </summary>
    public VertexDeletionBatch[] DeleteVertices(IReadOnlyList<SelectionTarget> targets)
    {
        Dictionary<EditorObjectId, List<VertexHandle>> verticesByObject = [];
        foreach (SelectionTarget target in targets)
        {
            if (
                target.Kind != ScenePickElementKind.Vertex
                || !_meshNodes.ContainsKey(target.ObjectId)
            )
            {
                continue;
            }

            if (!verticesByObject.TryGetValue(target.ObjectId, out List<VertexHandle> vertices))
            {
                vertices = [];
                verticesByObject[target.ObjectId] = vertices;
            }

            vertices.Add(target.Vertex);
        }

        List<VertexDeletionBatch> batches = [];
        HashSet<EditorObjectId> changedObjects = [];
        foreach ((EditorObjectId objectId, List<VertexHandle> vertices) in verticesByObject)
        {
            TRMeshGD meshNode = _meshNodes[objectId];
            if (
                VertexDeletionBatch.Delete(objectId, meshNode.SourceMesh, vertices)
                is VertexDeletionBatch batch
            )
            {
                batches.Add(batch);
                changedObjects.Add(objectId);
            }
        }

        RebuildObjects(changedObjects);
        return batches.ToArray();
    }

    public void ApplyVertexDeletionBefore(IReadOnlyList<VertexDeletionBatch> batches) =>
        ApplyVertexDeletion(batches, before: true);

    public void ApplyVertexDeletionAfter(IReadOnlyList<VertexDeletionBatch> batches) =>
        ApplyVertexDeletion(batches, before: false);

    private void ApplyVertexDeletion(IReadOnlyList<VertexDeletionBatch> batches, bool before)
    {
        HashSet<EditorObjectId> changedObjects = [];
        foreach (VertexDeletionBatch batch in batches)
        {
            if (!_meshNodes.ContainsKey(batch.ObjectId))
                continue;

            if (before)
                batch.ApplyBefore();
            else
                batch.ApplyAfter();
            changedObjects.Add(batch.ObjectId);
        }

        RebuildObjects(changedObjects);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (TRMeshGD meshNode in _meshNodes.Values)
        {
            Node parent = meshNode.GetParent();
            parent?.RemoveChild(meshNode);
            meshNode.QueueFree();
        }

        _meshNodes.Clear();
    }

    private bool TryGetSelectionTargetCenter(SelectionTarget target, out Vector3 center)
    {
        center = Vector3.Zero;
        if (!_meshNodes.TryGetValue(target.ObjectId, out TRMeshGD meshNode))
        {
            return false;
        }

        SpatialMesh mesh = meshNode.SourceMesh;

        switch (target.Kind)
        {
            case ScenePickElementKind.Object:
                return TryGetMeshBoundsCenter(meshNode, out center);
            case ScenePickElementKind.Vertex:
                center =
                    meshNode.GlobalTransform
                    * ToGodotVector3(mesh.GetVertexPosition(target.Vertex));
                return true;
            case ScenePickElementKind.Edge:
                if (!TryGetEdgeCenter(mesh, target.Edge, out Vector3 edgeCenter))
                {
                    return false;
                }

                center = meshNode.GlobalTransform * edgeCenter;
                return true;
            case ScenePickElementKind.Face:
                if (!TryGetFaceCenter(mesh, target.Face, out Vector3 faceCenter))
                {
                    return false;
                }

                center = meshNode.GlobalTransform * faceCenter;
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetMeshBoundsCenter(TRMeshGD meshNode, out Vector3 center)
    {
        center = Vector3.Zero;
        bool hasVertex = false;
        Vector3 min = Vector3.Zero;
        Vector3 max = Vector3.Zero;

        foreach (VertexHandle vertex in meshNode.SourceMesh.EnumerateLiveVertices())
        {
            Vector3 position = ToGodotVector3(meshNode.SourceMesh.GetVertexPosition(vertex));
            if (!hasVertex)
            {
                min = position;
                max = position;
                hasVertex = true;
                continue;
            }

            min = new Vector3(
                Mathf.Min(min.X, position.X),
                Mathf.Min(min.Y, position.Y),
                Mathf.Min(min.Z, position.Z)
            );
            max = new Vector3(
                Mathf.Max(max.X, position.X),
                Mathf.Max(max.Y, position.Y),
                Mathf.Max(max.Z, position.Z)
            );
        }

        if (!hasVertex)
        {
            return false;
        }

        center = meshNode.GlobalTransform * ((min + max) * 0.5f);
        return true;
    }

    private static void AddTargetVertices(
        SpatialMesh mesh,
        SelectionTarget target,
        HashSet<VertexHandle> vertices
    )
    {
        switch (target.Kind)
        {
            case ScenePickElementKind.Vertex:
                vertices.Add(target.Vertex);
                break;
            case ScenePickElementKind.Edge:
                AddEdgeVertices(mesh, target.Edge, vertices);
                break;
            case ScenePickElementKind.Face:
                foreach (HalfEdgeHandle halfEdge in mesh.HalfEdgesAroundFace(target.Face))
                {
                    vertices.Add(mesh.GetHalfEdge(halfEdge).Origin);
                }

                break;
        }
    }

    private static void AddEdgeVertices(
        SpatialMesh mesh,
        HalfEdgeHandle edge,
        HashSet<VertexHandle> vertices
    )
    {
        HalfEdge halfEdge = mesh.GetHalfEdge(edge);
        HalfEdge twin = mesh.GetHalfEdge(halfEdge.Twin);
        vertices.Add(halfEdge.Origin);
        vertices.Add(twin.Origin);
    }

    private static bool TryGetEdgeCenter(SpatialMesh mesh, HalfEdgeHandle edge, out Vector3 center)
    {
        center = Vector3.Zero;

        try
        {
            HalfEdge halfEdge = mesh.GetHalfEdge(edge);
            HalfEdge twin = mesh.GetHalfEdge(halfEdge.Twin);
            center =
                (
                    ToGodotVector3(mesh.GetVertexPosition(halfEdge.Origin))
                    + ToGodotVector3(mesh.GetVertexPosition(twin.Origin))
                ) * 0.5f;
            return true;
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or IndexOutOfRangeException
            )
        {
            return false;
        }
    }

    private static bool TryGetFaceCenter(SpatialMesh mesh, FaceHandle face, out Vector3 center)
    {
        center = Vector3.Zero;
        int count = 0;

        try
        {
            foreach (HalfEdgeHandle halfEdge in mesh.HalfEdgesAroundFace(face))
            {
                VertexHandle vertex = mesh.GetHalfEdge(halfEdge).Origin;
                center += ToGodotVector3(mesh.GetVertexPosition(vertex));
                count++;
            }
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or IndexOutOfRangeException
            )
        {
            center = Vector3.Zero;
            return false;
        }

        if (count == 0)
        {
            return false;
        }

        center /= count;
        return true;
    }

    private static Vector3 ToGodotVector3(NumericVector3 value) => new(value.X, value.Y, value.Z);

    private static NumericVector3 ToNumericVector3(Vector3 value) => new(value.X, value.Y, value.Z);

    private void RebuildObjects(IEnumerable<EditorObjectId> objectIds)
    {
        foreach (EditorObjectId objectId in objectIds)
        {
            if (_meshNodes.TryGetValue(objectId, out TRMeshGD meshNode))
                meshNode.Rebuild();
        }
    }
}

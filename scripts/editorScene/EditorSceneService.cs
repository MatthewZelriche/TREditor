using System;
using System.Collections.Generic;
using Godot;
using TREditorSharp;
using NumericVector3 = System.Numerics.Vector3;

// Centralizes committed scene-node mutation for now. This is intentionally a step toward
// a proper editor document/model layer, where commands would mutate model state and the
// Godot scene would become a synchronized view of that model.
// TODO: This class is getting huge
public sealed class EditorSceneService : IDisposable, IEditorObjectLifecycle
{
    private readonly EditorSceneModel _model;
    private readonly IEditorSceneView _view;
    private readonly EditorObjectLifecycle _lifecycle;
    private readonly Dictionary<EditorObjectId, EditorObjectModel> _detachedObjects = [];
    private bool _disposed;

    public EditorSceneService(Node3D worldRoot, TextureMaterialLibrary textureMaterials)
    {
        ArgumentNullException.ThrowIfNull(worldRoot);
        ArgumentNullException.ThrowIfNull(textureMaterials);

        _model = new EditorSceneModel();
        _view = new EditorSceneView(worldRoot, textureMaterials);
        _lifecycle = new EditorObjectLifecycle(_model, _view);
    }

    internal EditorSceneService(
        EditorObjectLifecycle lifecycle,
        EditorSceneModel model,
        IEditorSceneView view
    )
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(view);

        _lifecycle = lifecycle;
        _model = model;
        _view = view;
    }

    internal EditorSceneModel Model => _model;

    public bool CreateMeshObject(EditorObjectId objectId, SpatialMesh mesh, string displayName)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (_model.Contains(objectId) || _detachedObjects.ContainsKey(objectId))
            return false;

        EditorObjectModel obj = new(objectId, displayName, Transform3D.Identity, mesh);
        if (!_lifecycle.Add(obj))
        {
            obj.ReleaseOwnedMesh();
            return false;
        }

        return true;
    }

    /// <summary>
    /// Creates a mesh object and restores its local transform. Used when rebuilding a loaded
    /// document, where each object's placement is persisted alongside its geometry.
    /// </summary>
    public bool CreateMeshObject(
        EditorObjectId objectId,
        SpatialMesh mesh,
        string displayName,
        Transform3D transform
    )
    {
        if (!CreateMeshObject(objectId, mesh, displayName))
            return false;

        if (_model.TryGet(objectId, out EditorObjectModel obj))
        {
            obj.SetLocalTransform(transform);
            _view.SyncTransform(obj);
        }

        return true;
    }

    /// <summary>
    /// Removes and frees every mesh object, leaving an empty scene. Unlike <see cref="Dispose"/>,
    /// the service stays usable, so this is the reset step for loading or starting a new document.
    /// </summary>
    public void ClearAll()
    {
        _view.Clear();
        _model.Clear();

        foreach (EditorObjectModel obj in _detachedObjects.Values)
            obj.Dispose();

        _detachedObjects.Clear();
    }

    public bool RemoveMeshObject(EditorObjectId objectId)
    {
        EditorObjectModel removed = _lifecycle.Remove(objectId);
        if (removed == null)
            return false;

        _detachedObjects.Add(objectId, removed);
        return true;
    }

    public bool RestoreMeshObject(EditorObjectId objectId)
    {
        if (!_detachedObjects.TryGetValue(objectId, out EditorObjectModel obj))
            return false;

        if (!_lifecycle.Add(obj))
            return false;

        _detachedObjects.Remove(objectId);
        return true;
    }

    /// <summary>Permanently frees a mesh object that no longer participates in history.</summary>
    public bool DestroyMeshObject(EditorObjectId objectId)
    {
        if (!_detachedObjects.Remove(objectId, out EditorObjectModel obj))
            return false;

        obj.Dispose();
        return true;
    }

    public IEnumerable<KeyValuePair<EditorObjectId, TRMeshGD>> EnumerateMeshObjects() =>
        _view.Nodes;

    public bool TryGetMeshNode(EditorObjectId objectId, out TRMeshGD meshNode) =>
        _view.TryGetNode(objectId, out meshNode);

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
            || !_view.TryGetNode(target.ObjectId, out TRMeshGD meshNode)
            || !meshNode.SourceMesh.IsFaceAlive(target.Face)
        )
        {
            return false;
        }

        Vector3 localNormal = ToGodotVector3(meshNode.SourceMesh.ComputeFaceNormal(target.Face));
        normal = (meshNode.GlobalTransform.Basis.Inverse().Transposed() * localNormal).Normalized();
        return !normal.IsZeroApprox();
    }

    public bool TranslateSelection(SelectionSnapshot selection, Vector3 worldDelta)
    {
        if (selection.IsEmpty || worldDelta.IsZeroApprox())
            return false;

        HashSet<EditorObjectId> translatedObjects = [];
        Dictionary<EditorObjectId, HashSet<VertexHandle>> componentVertices = [];
        bool changed = false;

        foreach (SelectionTarget target in selection.Targets)
        {
            if (target.Kind == ScenePickElementKind.Object)
            {
                if (_view.TryGetNode(target.ObjectId, out TRMeshGD meshNode))
                {
                    meshNode.GlobalPosition += worldDelta;
                    translatedObjects.Add(target.ObjectId);
                    changed = true;
                }

                continue;
            }

            if (translatedObjects.Contains(target.ObjectId))
            {
                continue;
            }

            if (!_view.TryGetNode(target.ObjectId, out TRMeshGD componentMeshNode))
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

            if (!_view.TryGetNode(objectId, out TRMeshGD meshNode))
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
            changed = true;
        }

        return changed;
    }

    public FaceExtrusionChange ExtrudeFace(SelectionTarget target, Vector3 worldDelta)
    {
        if (
            target.Kind != ScenePickElementKind.Face
            || worldDelta.IsZeroApprox()
            || !_view.TryGetNode(target.ObjectId, out TRMeshGD meshNode)
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

    public bool CanExtrudeEdge(SelectionTarget target) =>
        target.Kind == ScenePickElementKind.Edge
        && _view.TryGetNode(target.ObjectId, out TRMeshGD meshNode)
        && EdgeExtrusionChange.CanExtrude(meshNode.SourceMesh, target.Edge);

    public EdgeExtrusionChange ExtrudeEdge(SelectionTarget target, Vector3 worldDelta)
    {
        if (
            target.Kind != ScenePickElementKind.Edge
            || worldDelta.IsZeroApprox()
            || !_view.TryGetNode(target.ObjectId, out TRMeshGD meshNode)
        )
        {
            return null;
        }

        Vector3 localDelta = meshNode.GlobalTransform.Basis.Inverse() * worldDelta;
        EdgeExtrusionChange change = EdgeExtrusionChange.Extrude(
            target.ObjectId,
            meshNode.SourceMesh,
            target.Edge,
            ToNumericVector3(localDelta)
        );
        if (change != null)
            meshNode.Rebuild();
        return change;
    }

    public FaceInsetChange InsetFace(SelectionTarget target, float depth)
    {
        if (
            target.Kind != ScenePickElementKind.Face
            || !(depth > 0f)
            || !_view.TryGetNode(target.ObjectId, out TRMeshGD meshNode)
        )
        {
            return null;
        }

        FaceInsetChange change = FaceInsetChange.Inset(
            target.ObjectId,
            meshNode.SourceMesh,
            target.Face,
            depth
        );
        if (change != null)
            meshNode.Rebuild();
        return change;
    }

    public bool TryGetMaximumFaceInsetDepth(SelectionTarget target, out float maximumDepth)
    {
        maximumDepth = 0f;
        if (
            target.Kind != ScenePickElementKind.Face
            || !_view.TryGetNode(target.ObjectId, out TRMeshGD meshNode)
            || !meshNode.SourceMesh.IsFaceAlive(target.Face)
        )
        {
            return false;
        }

        maximumDepth = meshNode.SourceMesh.ComputeMaximumInsetDepth(target.Face);
        return maximumDepth > 0f;
    }

    public bool TryGetMaximumEdgeBevelWidth(
        IReadOnlyList<SelectionTarget> targets,
        out float maximumWidth
    )
    {
        maximumWidth = float.PositiveInfinity;
        if (targets.Count == 0)
            return false;

        Dictionary<EditorObjectId, List<HalfEdgeHandle>> edgesByObject = [];
        foreach (SelectionTarget target in targets)
        {
            if (target.Kind != ScenePickElementKind.Edge || !_model.Contains(target.ObjectId))
            {
                maximumWidth = 0f;
                return false;
            }

            if (!edgesByObject.TryGetValue(target.ObjectId, out List<HalfEdgeHandle> edges))
            {
                edges = [];
                edgesByObject[target.ObjectId] = edges;
            }
            edges.Add(target.Edge);
        }

        foreach ((EditorObjectId objectId, List<HalfEdgeHandle> edges) in edgesByObject)
        {
            if (
                !_model.TryGet(objectId, out EditorObjectModel edgeBevelObject)
                || !EdgeBevelBatch.TryGetMaximumWidth(
                    edgeBevelObject.Mesh,
                    edges,
                    out float objectMaximumWidth
                )
            )
            {
                maximumWidth = 0f;
                return false;
            }
            maximumWidth = MathF.Min(maximumWidth, objectMaximumWidth);
        }

        return maximumWidth > 0f && float.IsFinite(maximumWidth);
    }

    public EdgeBevelBatch[] BevelEdges(IReadOnlyList<SelectionTarget> targets, float width)
    {
        if (
            !(width > 0f)
            || !float.IsFinite(width)
            || !TryGetMaximumEdgeBevelWidth(targets, out float maximumWidth)
            || width > maximumWidth
        )
        {
            return [];
        }

        Dictionary<EditorObjectId, List<HalfEdgeHandle>> edgesByObject = [];
        foreach (SelectionTarget target in targets)
        {
            if (!edgesByObject.TryGetValue(target.ObjectId, out List<HalfEdgeHandle> edges))
            {
                edges = [];
                edgesByObject[target.ObjectId] = edges;
            }
            edges.Add(target.Edge);
        }

        List<EdgeBevelBatch> batches = [];
        HashSet<EditorObjectId> changedObjects = [];
        foreach ((EditorObjectId objectId, List<HalfEdgeHandle> edges) in edgesByObject)
        {
            if (
                !_model.TryGet(objectId, out EditorObjectModel bevelObject)
                || EdgeBevelBatch.Bevel(objectId, bevelObject.Mesh, edges, width)
                    is not EdgeBevelBatch batch
            )
            {
                continue;
            }

            batches.Add(batch);
            changedObjects.Add(objectId);
        }

        RebuildObjects(changedObjects);
        return batches.ToArray();
    }

    public bool TryGetMaximumVertexBevelWidth(
        IReadOnlyList<SelectionTarget> targets,
        out float maximumWidth
    )
    {
        maximumWidth = float.PositiveInfinity;
        if (targets.Count == 0)
            return false;

        Dictionary<EditorObjectId, List<VertexHandle>> verticesByObject = [];
        foreach (SelectionTarget target in targets)
        {
            if (target.Kind != ScenePickElementKind.Vertex || !_model.Contains(target.ObjectId))
            {
                maximumWidth = 0f;
                return false;
            }

            if (!verticesByObject.TryGetValue(target.ObjectId, out List<VertexHandle> vertices))
            {
                vertices = [];
                verticesByObject[target.ObjectId] = vertices;
            }
            vertices.Add(target.Vertex);
        }

        foreach ((EditorObjectId objectId, List<VertexHandle> vertices) in verticesByObject)
        {
            if (
                !_model.TryGet(objectId, out EditorObjectModel vertexBevelObject)
                || !VertexBevelBatch.TryGetMaximumWidth(
                    vertexBevelObject.Mesh,
                    vertices,
                    out float objectMaximumWidth
                )
            )
            {
                maximumWidth = 0f;
                return false;
            }
            maximumWidth = MathF.Min(maximumWidth, objectMaximumWidth);
        }

        return maximumWidth > 0f && float.IsFinite(maximumWidth);
    }

    public VertexBevelBatch[] BevelVertices(IReadOnlyList<SelectionTarget> targets, float width)
    {
        if (
            !(width > 0f)
            || !float.IsFinite(width)
            || !TryGetMaximumVertexBevelWidth(targets, out float maximumWidth)
            || width > maximumWidth
        )
        {
            return [];
        }

        Dictionary<EditorObjectId, List<VertexHandle>> verticesByObject = [];
        foreach (SelectionTarget target in targets)
        {
            if (!verticesByObject.TryGetValue(target.ObjectId, out List<VertexHandle> vertices))
            {
                vertices = [];
                verticesByObject[target.ObjectId] = vertices;
            }
            vertices.Add(target.Vertex);
        }

        List<VertexBevelBatch> batches = [];
        HashSet<EditorObjectId> changedObjects = [];
        foreach ((EditorObjectId objectId, List<VertexHandle> vertices) in verticesByObject)
        {
            if (
                !_model.TryGet(objectId, out EditorObjectModel vertexBevelObject)
                || VertexBevelBatch.Bevel(objectId, vertexBevelObject.Mesh, vertices, width)
                    is not VertexBevelBatch batch
            )
            {
                continue;
            }

            batches.Add(batch);
            changedObjects.Add(objectId);
        }

        RebuildObjects(changedObjects);
        return batches.ToArray();
    }

    public bool CanFillHole(SelectionTarget target)
    {
        if (
            target.Kind != ScenePickElementKind.Edge
            || !_view.TryGetNode(target.ObjectId, out TRMeshGD meshNode)
        )
        {
            return false;
        }

        List<VertexHandle> boundaryVertices = [];
        return meshNode.SourceMesh.TryGetHoleBoundaryVertices(target.Edge, boundaryVertices);
    }

    public FillHoleChange FillHole(SelectionTarget target)
    {
        if (
            target.Kind != ScenePickElementKind.Edge
            || !_view.TryGetNode(target.ObjectId, out TRMeshGD meshNode)
        )
        {
            return null;
        }

        FillHoleChange change = FillHoleChange.Fill(
            target.ObjectId,
            meshNode.SourceMesh,
            target.Edge
        );
        if (change != null)
            meshNode.Rebuild();
        return change;
    }

    public bool CanCollapseFace(SelectionTarget target) =>
        target.Kind == ScenePickElementKind.Face
        && _view.TryGetNode(target.ObjectId, out TRMeshGD meshNode)
        && FaceCollapseChange.CanCollapse(meshNode.SourceMesh, target.Face);

    public FaceCollapseChange CollapseFace(SelectionTarget target)
    {
        if (
            target.Kind != ScenePickElementKind.Face
            || !_view.TryGetNode(target.ObjectId, out TRMeshGD meshNode)
        )
        {
            return null;
        }

        FaceCollapseChange change = FaceCollapseChange.Collapse(
            target.ObjectId,
            meshNode.SourceMesh,
            target.Face
        );
        if (change != null)
            meshNode.Rebuild();
        return change;
    }

    public bool CanCollapseVertices(
        IReadOnlyList<SelectionTarget> targets,
        CollapseVerticesTarget twoVertexTarget
    )
    {
        if (
            targets.Count < 2
            || targets[0].Kind != ScenePickElementKind.Vertex
            || !_view.TryGetNode(targets[0].ObjectId, out TRMeshGD meshNode)
        )
        {
            return false;
        }

        List<VertexHandle> vertices = [];
        foreach (SelectionTarget target in targets)
        {
            if (
                target.Kind != ScenePickElementKind.Vertex
                || target.ObjectId != targets[0].ObjectId
            )
            {
                return false;
            }
            vertices.Add(target.Vertex);
        }

        return VertexCollapseChange.CanCollapse(meshNode.SourceMesh, vertices, twoVertexTarget);
    }

    public VertexCollapseChange CollapseVertices(
        IReadOnlyList<SelectionTarget> targets,
        CollapseVerticesTarget twoVertexTarget
    )
    {
        if (
            targets.Count < 2
            || targets[0].Kind != ScenePickElementKind.Vertex
            || !_view.TryGetNode(targets[0].ObjectId, out TRMeshGD meshNode)
        )
        {
            return null;
        }

        List<VertexHandle> vertices = [];
        foreach (SelectionTarget target in targets)
        {
            if (
                target.Kind != ScenePickElementKind.Vertex
                || target.ObjectId != targets[0].ObjectId
            )
            {
                return null;
            }
            vertices.Add(target.Vertex);
        }

        VertexCollapseChange change = VertexCollapseChange.Collapse(
            targets[0].ObjectId,
            meshNode.SourceMesh,
            vertices,
            twoVertexTarget
        );
        if (change != null)
            meshNode.Rebuild();
        return change;
    }

    public bool CanBridgeEdges(SelectionTarget first, SelectionTarget second) =>
        first.Kind == ScenePickElementKind.Edge
        && second.Kind == ScenePickElementKind.Edge
        && first.ObjectId == second.ObjectId
        && _view.TryGetNode(first.ObjectId, out TRMeshGD meshNode)
        && BridgeEdgesChange.CanBridge(meshNode.SourceMesh, first.Edge, second.Edge);

    public BridgeEdgesChange BridgeEdges(
        SelectionTarget first,
        SelectionTarget second,
        int segments,
        float archAngleDegrees
    )
    {
        if (
            first.Kind != ScenePickElementKind.Edge
            || second.Kind != ScenePickElementKind.Edge
            || first.ObjectId != second.ObjectId
            || !_view.TryGetNode(first.ObjectId, out TRMeshGD meshNode)
        )
        {
            return null;
        }

        BridgeEdgesChange change = BridgeEdgesChange.Bridge(
            first.ObjectId,
            meshNode.SourceMesh,
            first.Edge,
            second.Edge,
            segments,
            archAngleDegrees
        );
        if (change != null)
            meshNode.Rebuild();
        return change;
    }

    public bool CanDetachFaces(IReadOnlyList<SelectionTarget> targets)
    {
        if (targets.Count == 0)
            return false;

        Dictionary<EditorObjectId, List<FaceHandle>> facesByObject = [];
        foreach (SelectionTarget target in targets)
        {
            if (target.Kind != ScenePickElementKind.Face || !_model.Contains(target.ObjectId))
            {
                return false;
            }
            if (!facesByObject.TryGetValue(target.ObjectId, out List<FaceHandle> faces))
            {
                faces = [];
                facesByObject[target.ObjectId] = faces;
            }
            faces.Add(target.Face);
        }

        foreach ((EditorObjectId objectId, List<FaceHandle> faces) in facesByObject)
        {
            if (
                !_model.TryGet(objectId, out EditorObjectModel detachObject)
                || !FaceDetachBatch.CanDetach(detachObject.Mesh, faces)
            )
                return false;
        }
        return true;
    }

    public FaceDetachBatch[] DetachFaces(IReadOnlyList<SelectionTarget> targets)
    {
        if (!CanDetachFaces(targets))
            return [];

        Dictionary<EditorObjectId, List<FaceHandle>> facesByObject = [];
        foreach (SelectionTarget target in targets)
        {
            if (!facesByObject.TryGetValue(target.ObjectId, out List<FaceHandle> faces))
            {
                faces = [];
                facesByObject[target.ObjectId] = faces;
            }
            faces.Add(target.Face);
        }

        List<FaceDetachBatch> batches = [];
        HashSet<EditorObjectId> changedObjects = [];
        foreach ((EditorObjectId objectId, List<FaceHandle> faces) in facesByObject)
        {
            if (
                !_model.TryGet(objectId, out EditorObjectModel detachObject)
                || FaceDetachBatch.Detach(objectId, detachObject.Mesh, faces)
                    is not FaceDetachBatch batch
            )
            {
                continue;
            }

            batches.Add(batch);
            changedObjects.Add(objectId);
        }

        RebuildObjects(changedObjects);
        return batches.ToArray();
    }

    public EdgeCutChange CutFace(
        SelectionTarget face,
        HalfEdgeHandle firstEdge,
        float firstParameter,
        HalfEdgeHandle secondEdge,
        float secondParameter
    )
    {
        if (
            face.Kind != ScenePickElementKind.Face
            || !_view.TryGetNode(face.ObjectId, out TRMeshGD meshNode)
        )
        {
            return null;
        }

        EdgeCutChange change = EdgeCutChange.Cut(
            face.ObjectId,
            meshNode.SourceMesh,
            face.Face,
            firstEdge,
            firstParameter,
            secondEdge,
            secondParameter
        );
        if (change != null)
            meshNode.Rebuild();
        return change;
    }

    public void ApplyFaceExtrusionBefore(FaceExtrusionChange change) =>
        ApplyFaceExtrusion(change, before: true);

    public void ApplyFaceExtrusionAfter(FaceExtrusionChange change) =>
        ApplyFaceExtrusion(change, before: false);

    public void ApplyEdgeExtrusionBefore(EdgeExtrusionChange change) =>
        ApplyEdgeExtrusion(change, before: true);

    public void ApplyEdgeExtrusionAfter(EdgeExtrusionChange change) =>
        ApplyEdgeExtrusion(change, before: false);

    public void ApplyFaceInsetBefore(FaceInsetChange change) =>
        ApplyFaceInset(change, before: true);

    public void ApplyFaceInsetAfter(FaceInsetChange change) =>
        ApplyFaceInset(change, before: false);

    public void ApplyEdgeBevelBefore(IReadOnlyList<EdgeBevelBatch> batches) =>
        ApplyEdgeBevel(batches, before: true);

    public void ApplyEdgeBevelAfter(IReadOnlyList<EdgeBevelBatch> batches) =>
        ApplyEdgeBevel(batches, before: false);

    public void ApplyVertexBevelBefore(IReadOnlyList<VertexBevelBatch> batches) =>
        ApplyVertexBevel(batches, before: true);

    public void ApplyVertexBevelAfter(IReadOnlyList<VertexBevelBatch> batches) =>
        ApplyVertexBevel(batches, before: false);

    public void ApplyFillHoleBefore(FillHoleChange change) => ApplyFillHole(change, before: true);

    public void ApplyFillHoleAfter(FillHoleChange change) => ApplyFillHole(change, before: false);

    public void ApplyFaceCollapseBefore(FaceCollapseChange change) =>
        ApplyFaceCollapse(change, before: true);

    public void ApplyFaceCollapseAfter(FaceCollapseChange change) =>
        ApplyFaceCollapse(change, before: false);

    public void ApplyVertexCollapseBefore(VertexCollapseChange change) =>
        ApplyVertexCollapse(change, before: true);

    public void ApplyVertexCollapseAfter(VertexCollapseChange change) =>
        ApplyVertexCollapse(change, before: false);

    public void ApplyBridgeEdgesBefore(BridgeEdgesChange change) =>
        ApplyBridgeEdges(change, before: true);

    public void ApplyBridgeEdgesAfter(BridgeEdgesChange change) =>
        ApplyBridgeEdges(change, before: false);

    public void ApplyFaceDetachBefore(IReadOnlyList<FaceDetachBatch> batches) =>
        ApplyFaceDetach(batches, before: true);

    public void ApplyFaceDetachAfter(IReadOnlyList<FaceDetachBatch> batches) =>
        ApplyFaceDetach(batches, before: false);

    public void ApplyEdgeCutBefore(EdgeCutChange change) => ApplyEdgeCut(change, before: true);

    public void ApplyEdgeCutAfter(EdgeCutChange change) => ApplyEdgeCut(change, before: false);

    private void ApplyFaceExtrusion(FaceExtrusionChange change, bool before)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (!_view.TryGetNode(change.ObjectId, out TRMeshGD meshNode))
            return;

        if (before)
            change.ApplyBefore();
        else
            change.ApplyAfter();
        meshNode.Rebuild();
    }

    private void ApplyEdgeExtrusion(EdgeExtrusionChange change, bool before)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (!_view.TryGetNode(change.ObjectId, out TRMeshGD meshNode))
            return;

        if (before)
            change.ApplyBefore();
        else
            change.ApplyAfter();
        meshNode.Rebuild();
    }

    private void ApplyFaceInset(FaceInsetChange change, bool before)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (!_view.TryGetNode(change.ObjectId, out TRMeshGD meshNode))
            return;

        if (before)
            change.ApplyBefore();
        else
            change.ApplyAfter();
        meshNode.Rebuild();
    }

    private void ApplyEdgeBevel(IReadOnlyList<EdgeBevelBatch> batches, bool before)
    {
        HashSet<EditorObjectId> changedObjects = [];
        foreach (EdgeBevelBatch batch in batches)
        {
            if (!_model.Contains(batch.ObjectId))
                continue;

            if (before)
                batch.ApplyBefore();
            else
                batch.ApplyAfter();
            changedObjects.Add(batch.ObjectId);
        }

        RebuildObjects(changedObjects);
    }

    private void ApplyVertexBevel(IReadOnlyList<VertexBevelBatch> batches, bool before)
    {
        HashSet<EditorObjectId> changedObjects = [];
        foreach (VertexBevelBatch batch in batches)
        {
            if (!_model.Contains(batch.ObjectId))
                continue;

            if (before)
                batch.ApplyBefore();
            else
                batch.ApplyAfter();
            changedObjects.Add(batch.ObjectId);
        }

        RebuildObjects(changedObjects);
    }

    private void ApplyFillHole(FillHoleChange change, bool before)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (!_view.TryGetNode(change.ObjectId, out TRMeshGD meshNode))
            return;

        if (before)
            change.ApplyBefore();
        else
            change.ApplyAfter();
        meshNode.Rebuild();
    }

    private void ApplyFaceCollapse(FaceCollapseChange change, bool before)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (!_view.TryGetNode(change.ObjectId, out TRMeshGD meshNode))
            return;

        if (before)
            change.ApplyBefore();
        else
            change.ApplyAfter();
        meshNode.Rebuild();
    }

    private void ApplyVertexCollapse(VertexCollapseChange change, bool before)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (!_view.TryGetNode(change.ObjectId, out TRMeshGD meshNode))
            return;

        if (before)
            change.ApplyBefore();
        else
            change.ApplyAfter();
        meshNode.Rebuild();
    }

    private void ApplyBridgeEdges(BridgeEdgesChange change, bool before)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (!_view.TryGetNode(change.ObjectId, out TRMeshGD meshNode))
            return;

        if (before)
            change.ApplyBefore();
        else
            change.ApplyAfter();
        meshNode.Rebuild();
    }

    private void ApplyFaceDetach(IReadOnlyList<FaceDetachBatch> batches, bool before)
    {
        HashSet<EditorObjectId> changedObjects = [];
        foreach (FaceDetachBatch batch in batches)
        {
            if (!_model.Contains(batch.ObjectId))
                continue;

            if (before)
                batch.ApplyBefore();
            else
                batch.ApplyAfter();
            changedObjects.Add(batch.ObjectId);
        }

        RebuildObjects(changedObjects);
    }

    private void ApplyEdgeCut(EdgeCutChange change, bool before)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (!_view.TryGetNode(change.ObjectId, out TRMeshGD meshNode))
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
        if (!_view.TryGetNode(objectId, out TRMeshGD meshNode))
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
                && _view.TryGetNode(target.ObjectId, out TRMeshGD meshNode)
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
                _view.TryGetNode(change.ObjectId, out TRMeshGD meshNode)
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
            if (!_view.TryGetNode(change.ObjectId, out TRMeshGD meshNode))
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
            if (target.Kind != ScenePickElementKind.Edge || !_model.Contains(target.ObjectId))
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
            if (
                !_model.TryGet(objectId, out EditorObjectModel edgeDeleteObject)
                || EdgeDeletionBatch.Delete(objectId, edgeDeleteObject.Mesh, edges)
                    is not EdgeDeletionBatch batch
            )
            {
                continue;
            }

            batches.Add(batch);
            changedObjects.Add(objectId);
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
            if (!_model.Contains(batch.ObjectId))
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
            if (target.Kind != ScenePickElementKind.Vertex || !_model.Contains(target.ObjectId))
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
            if (
                !_model.TryGet(objectId, out EditorObjectModel vertexDeleteObject)
                || VertexDeletionBatch.Delete(objectId, vertexDeleteObject.Mesh, vertices)
                    is not VertexDeletionBatch batch
            )
            {
                continue;
            }

            batches.Add(batch);
            changedObjects.Add(objectId);
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
            if (!_model.Contains(batch.ObjectId))
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
            return;

        _disposed = true;

        foreach (EditorObjectModel obj in _detachedObjects.Values)
            obj.Dispose();

        _detachedObjects.Clear();

        if (_view is IDisposable disposableView)
            disposableView.Dispose();

        _model.Dispose();
    }

    private bool TryGetSelectionTargetCenter(SelectionTarget target, out Vector3 center)
    {
        center = Vector3.Zero;
        if (!_view.TryGetNode(target.ObjectId, out TRMeshGD meshNode))
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
            if (_view.TryGetNode(objectId, out TRMeshGD meshNode))
                meshNode.Rebuild();
        }
    }
}

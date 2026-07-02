using System;
using System.Collections.Generic;
using Godot;
using TREditorSharp;

/// <summary>
/// Compatibility façade over object lifecycle, authoritative model state, view synchronization,
/// and mesh operations. Callers continue to use this type until migration completes in Step 4.8.
/// </summary>
public sealed class EditorSceneService : IDisposable
{
    private readonly EditorSceneModel _model;
    private readonly IEditorSceneView _view;
    private readonly EditorObjectLifecycle _lifecycle;
    private readonly EditorMeshOperations _operations;
    private bool _disposed;

    public EditorSceneService(Node3D worldRoot, TextureMaterialLibrary textureMaterials)
    {
        ArgumentNullException.ThrowIfNull(worldRoot);
        ArgumentNullException.ThrowIfNull(textureMaterials);

        _model = new EditorSceneModel();
        _view = new EditorSceneView(worldRoot, textureMaterials);
        _lifecycle = new EditorObjectLifecycle(_model, _view);
        _operations = new EditorMeshOperations(_model, _view, worldRoot);
    }

    internal EditorSceneService(
        EditorObjectLifecycle lifecycle,
        EditorSceneModel model,
        IEditorSceneView view,
        EditorMeshOperations operations
    )
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(operations);

        _lifecycle = lifecycle;
        _model = model;
        _view = view;
        _operations = operations;
    }

    internal EditorSceneModel Model => _model;

    internal EditorObjectLifecycle Lifecycle => _lifecycle;

    internal EditorMeshOperations Operations => _operations;

    public bool CreateMeshObject(EditorObjectId objectId, SpatialMesh mesh, string displayName)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (_model.Contains(objectId))
            return false;

        EditorObjectModel obj = new(objectId, displayName, Transform3D.Identity, mesh);
        if (!_lifecycle.Add(obj))
        {
            obj.ReleaseOwnedMesh();
            return false;
        }

        return true;
    }

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

    internal bool TryAddObject(EditorObjectModel obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        if (_model.Contains(obj.Id))
            return false;

        return _lifecycle.Add(obj);
    }

    public void ClearAll()
    {
        _view.Clear();
        _model.Clear();
    }

    public IEnumerable<KeyValuePair<EditorObjectId, TRMeshGD>> EnumerateMeshObjects() =>
        _view.Nodes;

    public bool TryGetMeshNode(EditorObjectId objectId, out TRMeshGD meshNode) =>
        _view.TryGetNode(objectId, out meshNode);

    public bool TryGetSelectionCenter(SelectionSnapshot selection, out Vector3 center) =>
        _operations.TryGetSelectionCenter(selection, out center);

    public bool TryGetFaceWorldNormal(SelectionTarget target, out Vector3 normal) =>
        _operations.TryGetFaceWorldNormal(target, out normal);

    public bool TranslateSelection(SelectionSnapshot selection, Vector3 worldDelta) =>
        _operations.TranslateSelection(selection, worldDelta);

    public FaceExtrusionChange ExtrudeFace(SelectionTarget target, Vector3 worldDelta) =>
        _operations.ExtrudeFace(target, worldDelta);

    public bool CanExtrudeEdge(SelectionTarget target) => _operations.CanExtrudeEdge(target);

    public EdgeExtrusionChange ExtrudeEdge(SelectionTarget target, Vector3 worldDelta) =>
        _operations.ExtrudeEdge(target, worldDelta);

    public FaceInsetChange InsetFace(SelectionTarget target, float depth) =>
        _operations.InsetFace(target, depth);

    public bool TryGetMaximumFaceInsetDepth(SelectionTarget target, out float maximumDepth) =>
        _operations.TryGetMaximumFaceInsetDepth(target, out maximumDepth);

    public bool TryGetMaximumEdgeBevelWidth(
        IReadOnlyList<SelectionTarget> targets,
        out float maximumWidth
    ) => _operations.TryGetMaximumEdgeBevelWidth(targets, out maximumWidth);

    public EdgeBevelBatch[] BevelEdges(IReadOnlyList<SelectionTarget> targets, float width) =>
        _operations.BevelEdges(targets, width);

    public bool TryGetMaximumVertexBevelWidth(
        IReadOnlyList<SelectionTarget> targets,
        out float maximumWidth
    ) => _operations.TryGetMaximumVertexBevelWidth(targets, out maximumWidth);

    public VertexBevelBatch[] BevelVertices(IReadOnlyList<SelectionTarget> targets, float width) =>
        _operations.BevelVertices(targets, width);

    public bool CanFillHole(SelectionTarget target) => _operations.CanFillHole(target);

    public FillHoleChange FillHole(SelectionTarget target) => _operations.FillHole(target);

    public bool CanCollapseFace(SelectionTarget target) => _operations.CanCollapseFace(target);

    public FaceCollapseChange CollapseFace(SelectionTarget target) =>
        _operations.CollapseFace(target);

    public bool CanCollapseVertices(
        IReadOnlyList<SelectionTarget> targets,
        CollapseVerticesTarget twoVertexTarget
    ) => _operations.CanCollapseVertices(targets, twoVertexTarget);

    public VertexCollapseChange CollapseVertices(
        IReadOnlyList<SelectionTarget> targets,
        CollapseVerticesTarget twoVertexTarget
    ) => _operations.CollapseVertices(targets, twoVertexTarget);

    public bool CanBridgeEdges(SelectionTarget first, SelectionTarget second) =>
        _operations.CanBridgeEdges(first, second);

    public BridgeEdgesChange BridgeEdges(
        SelectionTarget first,
        SelectionTarget second,
        int segments,
        float archAngleDegrees
    ) => _operations.BridgeEdges(first, second, segments, archAngleDegrees);

    public bool CanDetachFaces(IReadOnlyList<SelectionTarget> targets) =>
        _operations.CanDetachFaces(targets);

    public FaceDetachBatch[] DetachFaces(IReadOnlyList<SelectionTarget> targets) =>
        _operations.DetachFaces(targets);

    public EdgeCutChange CutFace(
        SelectionTarget face,
        HalfEdgeHandle firstEdge,
        float firstParameter,
        HalfEdgeHandle secondEdge,
        float secondParameter
    ) => _operations.CutFace(face, firstEdge, firstParameter, secondEdge, secondParameter);

    public void ApplyFaceExtrusionBefore(FaceExtrusionChange change) =>
        _operations.ApplyFaceExtrusionBefore(change);

    public void ApplyFaceExtrusionAfter(FaceExtrusionChange change) =>
        _operations.ApplyFaceExtrusionAfter(change);

    public void ApplyEdgeExtrusionBefore(EdgeExtrusionChange change) =>
        _operations.ApplyEdgeExtrusionBefore(change);

    public void ApplyEdgeExtrusionAfter(EdgeExtrusionChange change) =>
        _operations.ApplyEdgeExtrusionAfter(change);

    public void ApplyFaceInsetBefore(FaceInsetChange change) =>
        _operations.ApplyFaceInsetBefore(change);

    public void ApplyFaceInsetAfter(FaceInsetChange change) =>
        _operations.ApplyFaceInsetAfter(change);

    public void ApplyEdgeBevelBefore(IReadOnlyList<EdgeBevelBatch> batches) =>
        _operations.ApplyEdgeBevelBefore(batches);

    public void ApplyEdgeBevelAfter(IReadOnlyList<EdgeBevelBatch> batches) =>
        _operations.ApplyEdgeBevelAfter(batches);

    public void ApplyVertexBevelBefore(IReadOnlyList<VertexBevelBatch> batches) =>
        _operations.ApplyVertexBevelBefore(batches);

    public void ApplyVertexBevelAfter(IReadOnlyList<VertexBevelBatch> batches) =>
        _operations.ApplyVertexBevelAfter(batches);

    public void ApplyFillHoleBefore(FillHoleChange change) =>
        _operations.ApplyFillHoleBefore(change);

    public void ApplyFillHoleAfter(FillHoleChange change) => _operations.ApplyFillHoleAfter(change);

    public void ApplyFaceCollapseBefore(FaceCollapseChange change) =>
        _operations.ApplyFaceCollapseBefore(change);

    public void ApplyFaceCollapseAfter(FaceCollapseChange change) =>
        _operations.ApplyFaceCollapseAfter(change);

    public void ApplyVertexCollapseBefore(VertexCollapseChange change) =>
        _operations.ApplyVertexCollapseBefore(change);

    public void ApplyVertexCollapseAfter(VertexCollapseChange change) =>
        _operations.ApplyVertexCollapseAfter(change);

    public void ApplyBridgeEdgesBefore(BridgeEdgesChange change) =>
        _operations.ApplyBridgeEdgesBefore(change);

    public void ApplyBridgeEdgesAfter(BridgeEdgesChange change) =>
        _operations.ApplyBridgeEdgesAfter(change);

    public void ApplyFaceDetachBefore(IReadOnlyList<FaceDetachBatch> batches) =>
        _operations.ApplyFaceDetachBefore(batches);

    public void ApplyFaceDetachAfter(IReadOnlyList<FaceDetachBatch> batches) =>
        _operations.ApplyFaceDetachAfter(batches);

    public void ApplyEdgeCutBefore(EdgeCutChange change) => _operations.ApplyEdgeCutBefore(change);

    public void ApplyEdgeCutAfter(EdgeCutChange change) => _operations.ApplyEdgeCutAfter(change);

    public bool ApplyFaceTexture(EditorObjectId objectId, FaceTextureChange change, bool revert) =>
        _operations.ApplyFaceTexture(objectId, change, revert);

    public FaceDeletionChange[] CaptureFaceDeletions(IReadOnlyList<SelectionTarget> targets) =>
        _operations.CaptureFaceDeletions(targets);

    public void DeleteFaces(IReadOnlyList<FaceDeletionChange> changes) =>
        _operations.DeleteFaces(changes);

    public void RestoreFaces(IReadOnlyList<FaceDeletionChange> changes) =>
        _operations.RestoreFaces(changes);

    public EdgeDeletionBatch[] DeleteEdges(IReadOnlyList<SelectionTarget> targets) =>
        _operations.DeleteEdges(targets);

    public void ApplyEdgeDeletionBefore(IReadOnlyList<EdgeDeletionBatch> batches) =>
        _operations.ApplyEdgeDeletionBefore(batches);

    public void ApplyEdgeDeletionAfter(IReadOnlyList<EdgeDeletionBatch> batches) =>
        _operations.ApplyEdgeDeletionAfter(batches);

    public VertexDeletionBatch[] DeleteVertices(IReadOnlyList<SelectionTarget> targets) =>
        _operations.DeleteVertices(targets);

    public void ApplyVertexDeletionBefore(IReadOnlyList<VertexDeletionBatch> batches) =>
        _operations.ApplyVertexDeletionBefore(batches);

    public void ApplyVertexDeletionAfter(IReadOnlyList<VertexDeletionBatch> batches) =>
        _operations.ApplyVertexDeletionAfter(batches);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_view is IDisposable disposableView)
            disposableView.Dispose();

        _model.Dispose();
    }
}

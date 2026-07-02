using System;
using Godot;

// TODO: Not sure how I feel about storing one type of each preview in this service.
public sealed class EditorPreviewService : IDisposable
{
    private readonly Node3D _previewParent;
    private readonly EditorMeshOperations _operations;
    private readonly Action _scenePreviewChanged;

    private PrimitiveCreationPreview _primitivePreview;
    private EditorPreviewRequest.TranslateSelection _translationPreview;
    private FaceExtrusionChange _faceExtrusionPreview;
    private EdgeExtrusionChange _edgeExtrusionPreview;
    private FaceInsetChange _faceInsetPreview;
    private EdgeBevelBatch[] _edgeBevelPreview;
    private VertexBevelBatch[] _vertexBevelPreview;
    private FillHoleChange _fillHolePreview;
    private FaceCollapseChange _faceCollapsePreview;
    private VertexCollapseChange _vertexCollapsePreview;
    private BridgeEdgesChange _bridgeEdgesPreview;
    private FaceDetachBatch[] _faceDetachPreview;
    private EdgeCutPreview _edgeCutPreview;
    private bool _disposed;

    public EditorPreviewService(
        Node3D previewParent,
        EditorMeshOperations operations,
        Action scenePreviewChanged
    )
    {
        ArgumentNullException.ThrowIfNull(previewParent);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(scenePreviewChanged);

        _previewParent = previewParent;
        _operations = operations;
        _scenePreviewChanged = scenePreviewChanged;
    }

    public void Apply(EditorPreviewRequest request)
    {
        if (request == null)
        {
            return;
        }
        if (request is not EditorPreviewRequest.EdgeCut)
            _edgeCutPreview?.Clear();

        switch (request)
        {
            case EditorPreviewRequest.Clear:
                Clear();
                break;
            case EditorPreviewRequest.Primitive primitive:
                ClearTranslationPreview();
                ClearFaceExtrusionPreview();
                ClearEdgeExtrusionPreview();
                ClearFaceInsetPreview();
                ClearEdgeBevelPreview();
                ClearVertexBevelPreview();
                ClearFillHolePreview();
                ClearFaceCollapsePreview();
                ClearVertexCollapsePreview();
                ClearBridgeEdgesPreview();
                ClearFaceDetachPreview();
                ShowPrimitivePreview(primitive.Settings, primitive.Bounds);
                break;
            case EditorPreviewRequest.TranslateSelection translation:
                ShowTranslationPreview(translation);
                break;
            case EditorPreviewRequest.ExtrudeFace extrusion:
                ShowFaceExtrusionPreview(extrusion);
                break;
            case EditorPreviewRequest.ExtrudeEdge extrusion:
                ShowEdgeExtrusionPreview(extrusion);
                break;
            case EditorPreviewRequest.InsetFace inset:
                ShowFaceInsetPreview(inset);
                break;
            case EditorPreviewRequest.BevelEdges bevel:
                ShowEdgeBevelPreview(bevel);
                break;
            case EditorPreviewRequest.BevelVertices bevel:
                ShowVertexBevelPreview(bevel);
                break;
            case EditorPreviewRequest.FillHole fillHole:
                ShowFillHolePreview(fillHole);
                break;
            case EditorPreviewRequest.CollapseFace collapseFace:
                ShowFaceCollapsePreview(collapseFace);
                break;
            case EditorPreviewRequest.CollapseVertices collapseVertices:
                ShowVertexCollapsePreview(collapseVertices);
                break;
            case EditorPreviewRequest.BridgeEdges bridgeEdges:
                ShowBridgeEdgesPreview(bridgeEdges);
                break;
            case EditorPreviewRequest.DetachFaces detachFaces:
                ShowFaceDetachPreview(detachFaces);
                break;
            case EditorPreviewRequest.EdgeCut edgeCut:
                ShowEdgeCutPreview(edgeCut);
                break;
        }
    }

    public void Clear()
    {
        _primitivePreview?.Clear();
        ClearTranslationPreview();
        ClearFaceExtrusionPreview();
        ClearEdgeExtrusionPreview();
        ClearFaceInsetPreview();
        ClearEdgeBevelPreview();
        ClearVertexBevelPreview();
        ClearFillHolePreview();
        ClearFaceCollapsePreview();
        ClearVertexCollapsePreview();
        ClearBridgeEdgesPreview();
        ClearFaceDetachPreview();
        _edgeCutPreview?.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Clear();
        _primitivePreview?.QueueFree();
        _primitivePreview = null;
        _edgeCutPreview?.QueueFree();
        _edgeCutPreview = null;
    }

    private void ShowPrimitivePreview(PrimitiveCreationSettings settings, PrimitiveBounds bounds)
    {
        EnsurePrimitivePreview();
        _primitivePreview.UpdatePreview(settings, bounds);
    }

    private void EnsurePrimitivePreview()
    {
        if (_primitivePreview != null)
        {
            return;
        }

        _primitivePreview = new PrimitiveCreationPreview { Name = "PrimitiveCreationPreview" };
        _previewParent.AddChild(_primitivePreview);
    }

    private void ShowEdgeCutPreview(EditorPreviewRequest.EdgeCut edgeCut)
    {
        Clear();
        if (_edgeCutPreview == null)
        {
            _edgeCutPreview = new EdgeCutPreview { Name = "EdgeCutPreview" };
            _previewParent.AddChild(_edgeCutPreview);
        }
        _edgeCutPreview.UpdatePreview(
            edgeCut.MeshTransform,
            edgeCut.Start,
            edgeCut.End,
            edgeCut.HasValidTarget
        );
    }

    private void ShowTranslationPreview(EditorPreviewRequest.TranslateSelection translation)
    {
        _primitivePreview?.Clear();
        ClearFaceExtrusionPreview(refresh: false);
        ClearEdgeExtrusionPreview(refresh: false);
        ClearFaceInsetPreview(refresh: false);
        ClearEdgeBevelPreview(refresh: false);
        ClearVertexBevelPreview(refresh: false);
        ClearFillHolePreview(refresh: false);
        ClearFaceCollapsePreview(refresh: false);
        ClearVertexCollapsePreview(refresh: false);
        ClearBridgeEdgesPreview(refresh: false);
        ClearFaceDetachPreview(refresh: false);
        ClearTranslationPreview(refresh: false);

        if (translation.Selection.IsEmpty || translation.Delta.IsZeroApprox())
        {
            _scenePreviewChanged();
            return;
        }

        _operations.TranslateSelection(translation.Selection, translation.Delta);
        _translationPreview = translation;
        _scenePreviewChanged();
    }

    private void ShowFaceExtrusionPreview(EditorPreviewRequest.ExtrudeFace extrusion)
    {
        _primitivePreview?.Clear();
        ClearTranslationPreview(refresh: false);
        ClearEdgeExtrusionPreview(refresh: false);
        ClearFaceInsetPreview(refresh: false);
        ClearEdgeBevelPreview(refresh: false);
        ClearVertexBevelPreview(refresh: false);
        ClearFillHolePreview(refresh: false);
        ClearFaceCollapsePreview(refresh: false);
        ClearVertexCollapsePreview(refresh: false);
        ClearBridgeEdgesPreview(refresh: false);
        ClearFaceDetachPreview(refresh: false);
        ClearFaceExtrusionPreview(refresh: false);

        if (extrusion.Delta.IsZeroApprox())
        {
            _scenePreviewChanged();
            return;
        }

        _faceExtrusionPreview = _operations.ExtrudeFace(extrusion.Face, extrusion.Delta);
        _scenePreviewChanged();
    }

    private void ShowEdgeExtrusionPreview(EditorPreviewRequest.ExtrudeEdge extrusion)
    {
        _primitivePreview?.Clear();
        ClearTranslationPreview(refresh: false);
        ClearFaceExtrusionPreview(refresh: false);
        ClearEdgeExtrusionPreview(refresh: false);
        ClearFaceInsetPreview(refresh: false);
        ClearEdgeBevelPreview(refresh: false);
        ClearVertexBevelPreview(refresh: false);
        ClearFillHolePreview(refresh: false);
        ClearFaceCollapsePreview(refresh: false);
        ClearVertexCollapsePreview(refresh: false);
        ClearBridgeEdgesPreview(refresh: false);
        ClearFaceDetachPreview(refresh: false);

        if (extrusion.Delta.IsZeroApprox())
        {
            _scenePreviewChanged();
            return;
        }

        _edgeExtrusionPreview = _operations.ExtrudeEdge(extrusion.Edge, extrusion.Delta);
        _scenePreviewChanged();
    }

    private void ShowFaceInsetPreview(EditorPreviewRequest.InsetFace inset)
    {
        _primitivePreview?.Clear();
        ClearTranslationPreview(refresh: false);
        ClearFaceExtrusionPreview(refresh: false);
        ClearEdgeExtrusionPreview(refresh: false);
        ClearFaceInsetPreview(refresh: false);
        ClearEdgeBevelPreview(refresh: false);
        ClearVertexBevelPreview(refresh: false);
        ClearFillHolePreview(refresh: false);
        ClearFaceCollapsePreview(refresh: false);
        ClearVertexCollapsePreview(refresh: false);
        ClearBridgeEdgesPreview(refresh: false);
        ClearFaceDetachPreview(refresh: false);

        if (!(inset.Depth > 0f))
        {
            _scenePreviewChanged();
            return;
        }

        _faceInsetPreview = _operations.InsetFace(inset.Face, inset.Depth);
        _scenePreviewChanged();
    }

    private void ShowEdgeBevelPreview(EditorPreviewRequest.BevelEdges bevel)
    {
        _primitivePreview?.Clear();
        ClearTranslationPreview(refresh: false);
        ClearFaceExtrusionPreview(refresh: false);
        ClearEdgeExtrusionPreview(refresh: false);
        ClearFaceInsetPreview(refresh: false);
        ClearEdgeBevelPreview(refresh: false);
        ClearVertexBevelPreview(refresh: false);
        ClearFillHolePreview(refresh: false);
        ClearFaceCollapsePreview(refresh: false);
        ClearVertexCollapsePreview(refresh: false);
        ClearBridgeEdgesPreview(refresh: false);
        ClearFaceDetachPreview(refresh: false);

        if (!(bevel.Width > 0f))
        {
            _scenePreviewChanged();
            return;
        }

        _edgeBevelPreview = _operations.BevelEdges(bevel.Selection.Targets, bevel.Width);
        _scenePreviewChanged();
    }

    private void ShowVertexBevelPreview(EditorPreviewRequest.BevelVertices bevel)
    {
        _primitivePreview?.Clear();
        ClearTranslationPreview(refresh: false);
        ClearFaceExtrusionPreview(refresh: false);
        ClearEdgeExtrusionPreview(refresh: false);
        ClearFaceInsetPreview(refresh: false);
        ClearEdgeBevelPreview(refresh: false);
        ClearVertexBevelPreview(refresh: false);
        ClearFillHolePreview(refresh: false);
        ClearFaceCollapsePreview(refresh: false);
        ClearVertexCollapsePreview(refresh: false);
        ClearBridgeEdgesPreview(refresh: false);
        ClearFaceDetachPreview(refresh: false);

        if (!(bevel.Width > 0f))
        {
            _scenePreviewChanged();
            return;
        }

        _vertexBevelPreview = _operations.BevelVertices(bevel.Selection.Targets, bevel.Width);
        _scenePreviewChanged();
    }

    private void ShowFillHolePreview(EditorPreviewRequest.FillHole fillHole)
    {
        _primitivePreview?.Clear();
        ClearTranslationPreview(refresh: false);
        ClearFaceExtrusionPreview(refresh: false);
        ClearEdgeExtrusionPreview(refresh: false);
        ClearFaceInsetPreview(refresh: false);
        ClearEdgeBevelPreview(refresh: false);
        ClearVertexBevelPreview(refresh: false);
        ClearFillHolePreview(refresh: false);
        ClearFaceCollapsePreview(refresh: false);
        ClearVertexCollapsePreview(refresh: false);
        ClearBridgeEdgesPreview(refresh: false);
        ClearFaceDetachPreview(refresh: false);

        _fillHolePreview = _operations.FillHole(fillHole.Edge);
        _scenePreviewChanged();
    }

    private void ShowFaceCollapsePreview(EditorPreviewRequest.CollapseFace collapseFace)
    {
        _primitivePreview?.Clear();
        ClearTranslationPreview(refresh: false);
        ClearFaceExtrusionPreview(refresh: false);
        ClearEdgeExtrusionPreview(refresh: false);
        ClearFaceInsetPreview(refresh: false);
        ClearEdgeBevelPreview(refresh: false);
        ClearVertexBevelPreview(refresh: false);
        ClearFillHolePreview(refresh: false);
        ClearFaceCollapsePreview(refresh: false);
        ClearVertexCollapsePreview(refresh: false);
        ClearBridgeEdgesPreview(refresh: false);
        ClearFaceDetachPreview(refresh: false);

        _faceCollapsePreview = _operations.CollapseFace(collapseFace.Face);
        _scenePreviewChanged();
    }

    private void ShowVertexCollapsePreview(EditorPreviewRequest.CollapseVertices collapse)
    {
        _primitivePreview?.Clear();
        ClearTranslationPreview(refresh: false);
        ClearFaceExtrusionPreview(refresh: false);
        ClearEdgeExtrusionPreview(refresh: false);
        ClearFaceInsetPreview(refresh: false);
        ClearEdgeBevelPreview(refresh: false);
        ClearVertexBevelPreview(refresh: false);
        ClearFillHolePreview(refresh: false);
        ClearFaceCollapsePreview(refresh: false);
        ClearVertexCollapsePreview(refresh: false);
        ClearBridgeEdgesPreview(refresh: false);
        ClearFaceDetachPreview(refresh: false);

        _vertexCollapsePreview = _operations.CollapseVertices(
            collapse.Selection.Targets,
            collapse.TwoVertexTarget
        );
        _scenePreviewChanged();
    }

    private void ShowBridgeEdgesPreview(EditorPreviewRequest.BridgeEdges bridge)
    {
        _primitivePreview?.Clear();
        ClearTranslationPreview(refresh: false);
        ClearFaceExtrusionPreview(refresh: false);
        ClearEdgeExtrusionPreview(refresh: false);
        ClearFaceInsetPreview(refresh: false);
        ClearEdgeBevelPreview(refresh: false);
        ClearVertexBevelPreview(refresh: false);
        ClearFillHolePreview(refresh: false);
        ClearFaceCollapsePreview(refresh: false);
        ClearVertexCollapsePreview(refresh: false);
        ClearBridgeEdgesPreview(refresh: false);
        ClearFaceDetachPreview(refresh: false);

        _bridgeEdgesPreview = _operations.BridgeEdges(
            bridge.First,
            bridge.Second,
            bridge.Segments,
            bridge.ArchAngleDegrees
        );
        _scenePreviewChanged();
    }

    private void ShowFaceDetachPreview(EditorPreviewRequest.DetachFaces detach)
    {
        _primitivePreview?.Clear();
        ClearTranslationPreview(refresh: false);
        ClearFaceExtrusionPreview(refresh: false);
        ClearEdgeExtrusionPreview(refresh: false);
        ClearFaceInsetPreview(refresh: false);
        ClearEdgeBevelPreview(refresh: false);
        ClearVertexBevelPreview(refresh: false);
        ClearFillHolePreview(refresh: false);
        ClearFaceCollapsePreview(refresh: false);
        ClearVertexCollapsePreview(refresh: false);
        ClearBridgeEdgesPreview(refresh: false);
        ClearFaceDetachPreview(refresh: false);

        _faceDetachPreview = _operations.DetachFaces(detach.Selection.Targets);
        _scenePreviewChanged();
    }

    private void ClearTranslationPreview(bool refresh = true)
    {
        if (_translationPreview == null)
        {
            return;
        }

        _operations.TranslateSelection(_translationPreview.Selection, -_translationPreview.Delta);
        _translationPreview = null;
        if (refresh)
        {
            _scenePreviewChanged();
        }
    }

    private void ClearFaceExtrusionPreview(bool refresh = true)
    {
        if (_faceExtrusionPreview == null)
        {
            return;
        }

        _operations.ApplyFaceExtrusionBefore(_faceExtrusionPreview);
        _faceExtrusionPreview.Dispose();
        _faceExtrusionPreview = null;
        if (refresh)
        {
            _scenePreviewChanged();
        }
    }

    private void ClearEdgeExtrusionPreview(bool refresh = true)
    {
        if (_edgeExtrusionPreview == null)
            return;

        _operations.ApplyEdgeExtrusionBefore(_edgeExtrusionPreview);
        _edgeExtrusionPreview.Dispose();
        _edgeExtrusionPreview = null;
        if (refresh)
            _scenePreviewChanged();
    }

    private void ClearFaceInsetPreview(bool refresh = true)
    {
        if (_faceInsetPreview == null)
            return;

        _operations.ApplyFaceInsetBefore(_faceInsetPreview);
        _faceInsetPreview.Dispose();
        _faceInsetPreview = null;
        if (refresh)
            _scenePreviewChanged();
    }

    private void ClearEdgeBevelPreview(bool refresh = true)
    {
        if (_edgeBevelPreview == null)
            return;

        _operations.ApplyEdgeBevelBefore(_edgeBevelPreview);
        foreach (EdgeBevelBatch batch in _edgeBevelPreview)
            batch.Dispose();
        _edgeBevelPreview = null;
        if (refresh)
            _scenePreviewChanged();
    }

    private void ClearVertexBevelPreview(bool refresh = true)
    {
        if (_vertexBevelPreview == null)
            return;

        _operations.ApplyVertexBevelBefore(_vertexBevelPreview);
        foreach (VertexBevelBatch batch in _vertexBevelPreview)
            batch.Dispose();
        _vertexBevelPreview = null;
        if (refresh)
            _scenePreviewChanged();
    }

    private void ClearFillHolePreview(bool refresh = true)
    {
        if (_fillHolePreview == null)
            return;

        _operations.ApplyFillHoleBefore(_fillHolePreview);
        _fillHolePreview.Dispose();
        _fillHolePreview = null;
        if (refresh)
            _scenePreviewChanged();
    }

    private void ClearFaceCollapsePreview(bool refresh = true)
    {
        if (_faceCollapsePreview == null)
            return;

        _operations.ApplyFaceCollapseBefore(_faceCollapsePreview);
        _faceCollapsePreview.Dispose();
        _faceCollapsePreview = null;
        if (refresh)
            _scenePreviewChanged();
    }

    private void ClearVertexCollapsePreview(bool refresh = true)
    {
        if (_vertexCollapsePreview == null)
            return;

        _operations.ApplyVertexCollapseBefore(_vertexCollapsePreview);
        _vertexCollapsePreview.Dispose();
        _vertexCollapsePreview = null;
        if (refresh)
            _scenePreviewChanged();
    }

    private void ClearBridgeEdgesPreview(bool refresh = true)
    {
        if (_bridgeEdgesPreview == null)
            return;

        _operations.ApplyBridgeEdgesBefore(_bridgeEdgesPreview);
        _bridgeEdgesPreview.Dispose();
        _bridgeEdgesPreview = null;
        if (refresh)
            _scenePreviewChanged();
    }

    private void ClearFaceDetachPreview(bool refresh = true)
    {
        if (_faceDetachPreview == null)
            return;

        _operations.ApplyFaceDetachBefore(_faceDetachPreview);
        foreach (FaceDetachBatch batch in _faceDetachPreview)
            batch.Dispose();
        _faceDetachPreview = null;
        if (refresh)
            _scenePreviewChanged();
    }
}

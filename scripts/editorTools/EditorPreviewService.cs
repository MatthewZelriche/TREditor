using System;
using Godot;

// TODO: Not sure how I feel about storing one type of each preview in this service.
public sealed class EditorPreviewService : IDisposable
{
    private readonly Node3D _previewParent;
    private readonly EditorSceneService _scene;
    private readonly Action _scenePreviewChanged;

    private PrimitiveCreationPreview _primitivePreview;
    private EditorPreviewRequest.TranslateSelection _translationPreview;
    private FaceExtrusionChange _faceExtrusionPreview;
    private FaceInsetChange _faceInsetPreview;
    private EdgeBevelBatch[] _edgeBevelPreview;
    private VertexBevelBatch[] _vertexBevelPreview;
    private FillHoleChange _fillHolePreview;
    private FaceCollapseChange _faceCollapsePreview;
    private VertexCollapseChange _vertexCollapsePreview;
    private BridgeEdgesChange _bridgeEdgesPreview;
    private bool _disposed;

    public EditorPreviewService(
        Node3D previewParent,
        EditorSceneService scene,
        Action scenePreviewChanged
    )
    {
        ArgumentNullException.ThrowIfNull(previewParent);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(scenePreviewChanged);

        _previewParent = previewParent;
        _scene = scene;
        _scenePreviewChanged = scenePreviewChanged;
    }

    public void Apply(EditorPreviewRequest request)
    {
        if (request == null)
        {
            return;
        }

        switch (request)
        {
            case EditorPreviewRequest.Clear:
                Clear();
                break;
            case EditorPreviewRequest.Primitive primitive:
                ClearTranslationPreview();
                ClearFaceExtrusionPreview();
                ClearFaceInsetPreview();
                ClearEdgeBevelPreview();
                ClearVertexBevelPreview();
                ClearFillHolePreview();
                ClearFaceCollapsePreview();
                ClearVertexCollapsePreview();
                ClearBridgeEdgesPreview();
                ShowPrimitivePreview(primitive.Settings, primitive.Bounds);
                break;
            case EditorPreviewRequest.TranslateSelection translation:
                ShowTranslationPreview(translation);
                break;
            case EditorPreviewRequest.ExtrudeFace extrusion:
                ShowFaceExtrusionPreview(extrusion);
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
        }
    }

    public void Clear()
    {
        _primitivePreview?.Clear();
        ClearTranslationPreview();
        ClearFaceExtrusionPreview();
        ClearFaceInsetPreview();
        ClearEdgeBevelPreview();
        ClearVertexBevelPreview();
        ClearFillHolePreview();
        ClearFaceCollapsePreview();
        ClearVertexCollapsePreview();
        ClearBridgeEdgesPreview();
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

    private void ShowTranslationPreview(EditorPreviewRequest.TranslateSelection translation)
    {
        _primitivePreview?.Clear();
        ClearFaceExtrusionPreview(refresh: false);
        ClearFaceInsetPreview(refresh: false);
        ClearEdgeBevelPreview(refresh: false);
        ClearVertexBevelPreview(refresh: false);
        ClearFillHolePreview(refresh: false);
        ClearFaceCollapsePreview(refresh: false);
        ClearVertexCollapsePreview(refresh: false);
        ClearBridgeEdgesPreview(refresh: false);
        ClearTranslationPreview(refresh: false);

        if (translation.Selection.IsEmpty || translation.Delta.IsZeroApprox())
        {
            _scenePreviewChanged();
            return;
        }

        _scene.TranslateSelection(translation.Selection, translation.Delta);
        _translationPreview = translation;
        _scenePreviewChanged();
    }

    private void ShowFaceExtrusionPreview(EditorPreviewRequest.ExtrudeFace extrusion)
    {
        _primitivePreview?.Clear();
        ClearTranslationPreview(refresh: false);
        ClearFaceInsetPreview(refresh: false);
        ClearEdgeBevelPreview(refresh: false);
        ClearVertexBevelPreview(refresh: false);
        ClearFillHolePreview(refresh: false);
        ClearFaceCollapsePreview(refresh: false);
        ClearVertexCollapsePreview(refresh: false);
        ClearBridgeEdgesPreview(refresh: false);
        ClearFaceExtrusionPreview(refresh: false);

        if (extrusion.Delta.IsZeroApprox())
        {
            _scenePreviewChanged();
            return;
        }

        _faceExtrusionPreview = _scene.ExtrudeFace(extrusion.Face, extrusion.Delta);
        _scenePreviewChanged();
    }

    private void ShowFaceInsetPreview(EditorPreviewRequest.InsetFace inset)
    {
        _primitivePreview?.Clear();
        ClearTranslationPreview(refresh: false);
        ClearFaceExtrusionPreview(refresh: false);
        ClearFaceInsetPreview(refresh: false);
        ClearEdgeBevelPreview(refresh: false);
        ClearVertexBevelPreview(refresh: false);
        ClearFillHolePreview(refresh: false);
        ClearFaceCollapsePreview(refresh: false);
        ClearVertexCollapsePreview(refresh: false);
        ClearBridgeEdgesPreview(refresh: false);

        if (!(inset.Depth > 0f))
        {
            _scenePreviewChanged();
            return;
        }

        _faceInsetPreview = _scene.InsetFace(inset.Face, inset.Depth);
        _scenePreviewChanged();
    }

    private void ShowEdgeBevelPreview(EditorPreviewRequest.BevelEdges bevel)
    {
        _primitivePreview?.Clear();
        ClearTranslationPreview(refresh: false);
        ClearFaceExtrusionPreview(refresh: false);
        ClearFaceInsetPreview(refresh: false);
        ClearEdgeBevelPreview(refresh: false);
        ClearVertexBevelPreview(refresh: false);
        ClearFillHolePreview(refresh: false);
        ClearFaceCollapsePreview(refresh: false);
        ClearVertexCollapsePreview(refresh: false);
        ClearBridgeEdgesPreview(refresh: false);

        if (!(bevel.Width > 0f))
        {
            _scenePreviewChanged();
            return;
        }

        _edgeBevelPreview = _scene.BevelEdges(bevel.Selection.Targets, bevel.Width);
        _scenePreviewChanged();
    }

    private void ShowVertexBevelPreview(EditorPreviewRequest.BevelVertices bevel)
    {
        _primitivePreview?.Clear();
        ClearTranslationPreview(refresh: false);
        ClearFaceExtrusionPreview(refresh: false);
        ClearFaceInsetPreview(refresh: false);
        ClearEdgeBevelPreview(refresh: false);
        ClearVertexBevelPreview(refresh: false);
        ClearFillHolePreview(refresh: false);
        ClearFaceCollapsePreview(refresh: false);
        ClearVertexCollapsePreview(refresh: false);
        ClearBridgeEdgesPreview(refresh: false);

        if (!(bevel.Width > 0f))
        {
            _scenePreviewChanged();
            return;
        }

        _vertexBevelPreview = _scene.BevelVertices(bevel.Selection.Targets, bevel.Width);
        _scenePreviewChanged();
    }

    private void ShowFillHolePreview(EditorPreviewRequest.FillHole fillHole)
    {
        _primitivePreview?.Clear();
        ClearTranslationPreview(refresh: false);
        ClearFaceExtrusionPreview(refresh: false);
        ClearFaceInsetPreview(refresh: false);
        ClearEdgeBevelPreview(refresh: false);
        ClearVertexBevelPreview(refresh: false);
        ClearFillHolePreview(refresh: false);
        ClearFaceCollapsePreview(refresh: false);
        ClearVertexCollapsePreview(refresh: false);
        ClearBridgeEdgesPreview(refresh: false);

        _fillHolePreview = _scene.FillHole(fillHole.Edge);
        _scenePreviewChanged();
    }

    private void ShowFaceCollapsePreview(EditorPreviewRequest.CollapseFace collapseFace)
    {
        _primitivePreview?.Clear();
        ClearTranslationPreview(refresh: false);
        ClearFaceExtrusionPreview(refresh: false);
        ClearFaceInsetPreview(refresh: false);
        ClearEdgeBevelPreview(refresh: false);
        ClearVertexBevelPreview(refresh: false);
        ClearFillHolePreview(refresh: false);
        ClearFaceCollapsePreview(refresh: false);
        ClearVertexCollapsePreview(refresh: false);
        ClearBridgeEdgesPreview(refresh: false);

        _faceCollapsePreview = _scene.CollapseFace(collapseFace.Face);
        _scenePreviewChanged();
    }

    private void ShowVertexCollapsePreview(EditorPreviewRequest.CollapseVertices collapse)
    {
        _primitivePreview?.Clear();
        ClearTranslationPreview(refresh: false);
        ClearFaceExtrusionPreview(refresh: false);
        ClearFaceInsetPreview(refresh: false);
        ClearEdgeBevelPreview(refresh: false);
        ClearVertexBevelPreview(refresh: false);
        ClearFillHolePreview(refresh: false);
        ClearFaceCollapsePreview(refresh: false);
        ClearVertexCollapsePreview(refresh: false);
        ClearBridgeEdgesPreview(refresh: false);

        _vertexCollapsePreview = _scene.CollapseVertices(
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
        ClearFaceInsetPreview(refresh: false);
        ClearEdgeBevelPreview(refresh: false);
        ClearVertexBevelPreview(refresh: false);
        ClearFillHolePreview(refresh: false);
        ClearFaceCollapsePreview(refresh: false);
        ClearVertexCollapsePreview(refresh: false);
        ClearBridgeEdgesPreview(refresh: false);

        _bridgeEdgesPreview = _scene.BridgeEdges(
            bridge.First,
            bridge.Second,
            bridge.Segments,
            bridge.ArchAngleDegrees
        );
        _scenePreviewChanged();
    }

    private void ClearTranslationPreview(bool refresh = true)
    {
        if (_translationPreview == null)
        {
            return;
        }

        _scene.TranslateSelection(_translationPreview.Selection, -_translationPreview.Delta);
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

        _scene.ApplyFaceExtrusionBefore(_faceExtrusionPreview);
        _faceExtrusionPreview.Dispose();
        _faceExtrusionPreview = null;
        if (refresh)
        {
            _scenePreviewChanged();
        }
    }

    private void ClearFaceInsetPreview(bool refresh = true)
    {
        if (_faceInsetPreview == null)
            return;

        _scene.ApplyFaceInsetBefore(_faceInsetPreview);
        _faceInsetPreview.Dispose();
        _faceInsetPreview = null;
        if (refresh)
            _scenePreviewChanged();
    }

    private void ClearEdgeBevelPreview(bool refresh = true)
    {
        if (_edgeBevelPreview == null)
            return;

        _scene.ApplyEdgeBevelBefore(_edgeBevelPreview);
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

        _scene.ApplyVertexBevelBefore(_vertexBevelPreview);
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

        _scene.ApplyFillHoleBefore(_fillHolePreview);
        _fillHolePreview.Dispose();
        _fillHolePreview = null;
        if (refresh)
            _scenePreviewChanged();
    }

    private void ClearFaceCollapsePreview(bool refresh = true)
    {
        if (_faceCollapsePreview == null)
            return;

        _scene.ApplyFaceCollapseBefore(_faceCollapsePreview);
        _faceCollapsePreview.Dispose();
        _faceCollapsePreview = null;
        if (refresh)
            _scenePreviewChanged();
    }

    private void ClearVertexCollapsePreview(bool refresh = true)
    {
        if (_vertexCollapsePreview == null)
            return;

        _scene.ApplyVertexCollapseBefore(_vertexCollapsePreview);
        _vertexCollapsePreview.Dispose();
        _vertexCollapsePreview = null;
        if (refresh)
            _scenePreviewChanged();
    }

    private void ClearBridgeEdgesPreview(bool refresh = true)
    {
        if (_bridgeEdgesPreview == null)
            return;

        _scene.ApplyBridgeEdgesBefore(_bridgeEdgesPreview);
        _bridgeEdgesPreview.Dispose();
        _bridgeEdgesPreview = null;
        if (refresh)
            _scenePreviewChanged();
    }
}

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
                ShowPrimitivePreview(primitive.Settings, primitive.Bounds);
                break;
            case EditorPreviewRequest.TranslateSelection translation:
                ShowTranslationPreview(translation);
                break;
        }
    }

    public void Clear()
    {
        _primitivePreview?.Clear();
        ClearTranslationPreview();
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
}

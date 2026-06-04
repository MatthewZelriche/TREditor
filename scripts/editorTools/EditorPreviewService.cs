using System;
using Godot;

// TODO: Not sure how I feel about storing one type of each preview in this service.
public sealed class EditorPreviewService : IDisposable
{
    private readonly Node3D _previewParent;

    private PrimitiveCreationPreview _primitivePreview;
    private bool _disposed;

    public EditorPreviewService(Node3D previewParent)
    {
        ArgumentNullException.ThrowIfNull(previewParent);

        _previewParent = previewParent;
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
                ShowPrimitivePreview(primitive.Settings, primitive.Bounds);
                break;
        }
    }

    public void Clear()
    {
        _primitivePreview?.Clear();
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
}

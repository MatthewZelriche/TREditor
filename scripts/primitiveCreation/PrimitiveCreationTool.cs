using System;
using Godot;
using TREditorSharp;

public sealed class PrimitiveCreationTool : IEditorTool
{
    private const float HeightPerScreenPixel = 0.02f;

    private enum CreationState
    {
        WaitingForFootprint,
        DrawingFootprint,
        RaisingHeight,
    }

    private readonly EditorToolContext _context;
    private readonly PrimitiveCreationSettings _settings;

    private CreationState _state = CreationState.WaitingForFootprint;
    private Vector3 _firstFootprintPoint;
    private Vector3 _secondFootprintPoint;
    private float _baseY;
    private float _heightReferenceScreenY;
    private float _currentHeight = PrimitiveBounds.DefaultMinimumExtent;
    private PrimitiveCreationPreview _preview;

    public PrimitiveCreationTool(PrimitiveCreationSettings settings, EditorToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _settings = settings;
        _context = context;
    }

    public void Enter()
    {
        ClearPreview();

        _state = CreationState.WaitingForFootprint;
        _currentHeight = PrimitiveBounds.DefaultMinimumExtent;
    }

    public void Exit()
    {
        ClearPreview();
        _preview?.QueueFree();
        _preview = null;
    }

    public EditorToolResult Cancel()
    {
        ClearPreview();
        return EditorToolResult.Cancelled();
    }

    public EditorToolResult HandleMouseButton(ViewportMouseButtonEvent input)
    {
        if (input.Button != MouseButton.Left || !input.Pressed)
        {
            return EditorToolResult.Continue;
        }

        if (_state == CreationState.WaitingForFootprint)
        {
            StartDrawing(input);
        }
        else if (_state == CreationState.DrawingFootprint)
        {
            StartRaisingHeight(input);
        }
        else if (_state == CreationState.RaisingHeight)
        {
            return EditorToolResult.Complete(CreatePrimitiveCommand());
        }

        return EditorToolResult.Continue;
    }

    public EditorToolResult HandleMouseMotion(ViewportMouseMotionEvent input)
    {
        if (_state == CreationState.DrawingFootprint)
        {
            UpdateFootprint(input);
        }
        else if (_state == CreationState.RaisingHeight)
        {
            UpdateHeight(input.ViewportPosition.Y);
        }

        return EditorToolResult.Continue;
    }

    private void StartDrawing(ViewportMouseButtonEvent input)
    {
        if (!TryPickCreationPoint(input.RayOrigin, input.RayDirection, out Vector3 point))
        {
            return;
        }

        _firstFootprintPoint = point;
        _secondFootprintPoint = point;
        _baseY = point.Y;
        _state = CreationState.DrawingFootprint;
        UpdatePreview(GetCurrentBounds());
    }

    private void UpdateFootprint(ViewportMouseMotionEvent input)
    {
        if (!TryPickCreationPoint(input.RayOrigin, input.RayDirection, out Vector3 point))
        {
            return;
        }

        _secondFootprintPoint = point;
        UpdatePreview(GetCurrentBounds());
    }

    private void StartRaisingHeight(ViewportMouseButtonEvent input)
    {
        _heightReferenceScreenY = input.ViewportPosition.Y;
        _currentHeight = SnapHeight(PrimitiveBounds.DefaultMinimumExtent);
        _state = CreationState.RaisingHeight;
        UpdatePreview(GetCurrentBounds());
    }

    private void UpdateHeight(float currentScreenY)
    {
        float upwardPixels = _heightReferenceScreenY - currentScreenY;
        float height = Mathf.Max(
            PrimitiveBounds.DefaultMinimumExtent,
            upwardPixels * HeightPerScreenPixel
        );
        _currentHeight = SnapHeight(height);
        UpdatePreview(GetCurrentBounds());
    }

    private EditorCommand CreatePrimitiveCommand()
    {
        SpatialMesh mesh = PrimitiveMeshFactory.Build(_settings, GetCurrentBounds());
        return new CreateMeshCommand(
            _context.WorldRoot,
            mesh,
            GetPrimitiveDisplayName(_settings.Kind)
        );
    }

    private PrimitiveBounds GetCurrentBounds()
    {
        return PrimitiveBounds.FromXzAndY(
            _firstFootprintPoint,
            _secondFootprintPoint,
            _baseY,
            _baseY + _currentHeight
        );
    }

    private float SnapHeight(float height)
    {
        float snapSize = _context.GetGridSnapSize();
        if (snapSize <= 0.0f)
        {
            return height;
        }

        Vector3 snappedTop = GridSnap.Snap(new Vector3(0.0f, _baseY + height, 0.0f), snapSize);
        return Mathf.Max(snapSize, snappedTop.Y - _baseY);
    }

    private bool TryPickCreationPoint(Vector3 rayOrigin, Vector3 rayDirection, out Vector3 point)
    {
        if (_context.RayPicking.TryPick(rayOrigin, rayDirection, out RayPickHit hit))
        {
            point = GridSnap.Snap(hit.Position, _context.GetGridSnapSize());
            return true;
        }

        if (_context.RayPicking.TryPickGrid(rayOrigin, rayDirection, out point))
        {
            point = GridSnap.Snap(point, _context.GetGridSnapSize());
            return true;
        }

        return false;
    }

    private void UpdatePreview(PrimitiveBounds bounds)
    {
        EnsurePreview();
        _preview.UpdatePreview(_settings, bounds);
    }

    private void ClearPreview()
    {
        _preview?.Clear();
    }

    private void EnsurePreview()
    {
        if (_preview != null)
        {
            return;
        }

        _preview = new PrimitiveCreationPreview { Name = "PrimitiveCreationPreview" };
        _context.WorldRoot.AddChild(_preview);
    }

    private static string GetPrimitiveDisplayName(PrimitiveKind kind)
    {
        return kind switch
        {
            PrimitiveKind.Box => "Box",
            PrimitiveKind.Cylinder => "Cylinder",
            _ => "Primitive",
        };
    }
}

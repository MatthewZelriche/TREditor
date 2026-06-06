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
    private readonly Func<PrimitiveCreationSettings> _getSettings;

    private CreationState _state = CreationState.WaitingForFootprint;
    private PrimitiveCreationSettings _settings;
    private Vector3 _firstFootprintPoint;
    private Vector3 _secondFootprintPoint;
    private float _baseY;
    private float _heightReferenceScreenY;
    private float _currentHeight = PrimitiveBounds.DefaultMinimumExtent;

    public PrimitiveCreationTool(
        Func<PrimitiveCreationSettings> getSettings,
        EditorToolContext context
    )
    {
        ArgumentNullException.ThrowIfNull(getSettings);
        ArgumentNullException.ThrowIfNull(context);

        _getSettings = getSettings;
        _context = context;
    }

    public void Enter() => Reset();

    public void Exit() => Reset();

    public EditorToolResult Cancel()
    {
        Reset();
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
            return TryStartDrawing(input)
                ? ContinueWithCurrentPreview()
                : EditorToolResult.Continue;
        }
        else if (_state == CreationState.DrawingFootprint)
        {
            StartRaisingHeight(input);
            return ContinueWithCurrentPreview();
        }
        else if (_state == CreationState.RaisingHeight)
        {
            EditorCommand command = CreatePrimitiveCommand();
            Reset();
            return new EditorToolResult(
                EditorToolStatus.Continue,
                command,
                new EditorPreviewRequest.Clear()
            );
        }

        return EditorToolResult.Continue;
    }

    public EditorToolResult HandleMouseMotion(ViewportMouseMotionEvent input)
    {
        if (_state == CreationState.DrawingFootprint)
        {
            return TryUpdateFootprint(input)
                ? ContinueWithCurrentPreview()
                : EditorToolResult.Continue;
        }
        else if (_state == CreationState.RaisingHeight)
        {
            UpdateHeight(input.ViewportPosition.Y);
            return ContinueWithCurrentPreview();
        }

        return EditorToolResult.Continue;
    }

    private bool TryStartDrawing(ViewportMouseButtonEvent input)
    {
        if (!TryPickCreationPoint(input.RayOrigin, input.RayDirection, out Vector3 point))
        {
            return false;
        }

        _firstFootprintPoint = point;
        _secondFootprintPoint = point;
        _baseY = point.Y;
        _settings = _getSettings();
        _state = CreationState.DrawingFootprint;
        return true;
    }

    private bool TryUpdateFootprint(ViewportMouseMotionEvent input)
    {
        if (!TryPickCreationPoint(input.RayOrigin, input.RayDirection, out Vector3 point))
        {
            return false;
        }

        _secondFootprintPoint = point;
        return true;
    }

    private void StartRaisingHeight(ViewportMouseButtonEvent input)
    {
        _heightReferenceScreenY = input.ViewportPosition.Y;
        _currentHeight = SnapHeight(PrimitiveBounds.DefaultMinimumExtent);
        _state = CreationState.RaisingHeight;
    }

    private void UpdateHeight(float currentScreenY)
    {
        float upwardPixels = _heightReferenceScreenY - currentScreenY;
        float height = Mathf.Max(
            PrimitiveBounds.DefaultMinimumExtent,
            upwardPixels * HeightPerScreenPixel
        );
        _currentHeight = SnapHeight(height);
    }

    private EditorCommand CreatePrimitiveCommand()
    {
        SpatialMesh mesh = PrimitiveMeshFactory.Build(_settings, GetCurrentBounds());
        return new CreateMeshCommand(
            EditorObjectId.New(),
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
        if (_context.ScenePicking.TryPick(rayOrigin, rayDirection, out RayPickHit hit))
        {
            point = GridSnap.Snap(hit.Position, _context.GetGridSnapSize());
            return true;
        }

        if (_context.ScenePicking.TryPickGrid(rayOrigin, rayDirection, out point))
        {
            point = GridSnap.Snap(point, _context.GetGridSnapSize());
            return true;
        }

        return false;
    }

    private EditorToolResult ContinueWithCurrentPreview() =>
        EditorToolResult.ContinueWithPreview(
            new EditorPreviewRequest.Primitive(_settings, GetCurrentBounds())
        );

    private void Reset()
    {
        _state = CreationState.WaitingForFootprint;
        _currentHeight = PrimitiveBounds.DefaultMinimumExtent;
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

using System;
using Godot;
using TREditorSharp;

public sealed class PrimitiveCreationTool : IDisposable
{
    private const float HeightPerScreenPixel = 0.02f;

    private enum CreationState
    {
        // "Inactive" in the sense that the tool is selected in the background but not
        // currently participating in the creation process. Eg After the user commits a
        // primitive, but before they select another tool.
        // We can probably refactorm this once we have a better tool selection architecture.
        Inactive,
        WaitingForFootprint,
        DrawingFootprint,
        RaisingHeight,
    }

    private readonly EditorSession _session;

    private PrimitiveCreationSettings _settings;
    private CreationState _state = CreationState.Inactive;
    private Vector3 _firstFootprintPoint;
    private Vector3 _secondFootprintPoint;
    private float _baseY;
    private float _heightReferenceScreenY;
    private float _currentHeight = PrimitiveBounds.DefaultMinimumExtent;
    private PrimitiveCreationPreview _preview;
    private bool _disposed;

    // TODO: Not sure how I feel about passing the entire session object in here.
    // Perhaps something like passing only the picking service and then returning a command object
    // that the session can then execute?
    public PrimitiveCreationTool(EditorSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;

        if (ViewportInputEvents.Instance == null)
        {
            GD.PushWarning("PrimitiveCreationTool could not find ViewportInputEvents.");
            return;
        }

        ViewportInputEvents.Instance.ViewportMouseButton += OnViewportMouseButton;
        ViewportInputEvents.Instance.ViewportMouseMotion += OnViewportMouseMotion;
    }

    public void Begin(PrimitiveCreationSettings settings)
    {
        ClearPreview();

        _settings = settings;
        _state = CreationState.WaitingForFootprint;
        _currentHeight = PrimitiveBounds.DefaultMinimumExtent;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ClearPreview();
        _preview?.QueueFree();
        _preview = null;

        if (ViewportInputEvents.Instance != null)
        {
            ViewportInputEvents.Instance.ViewportMouseButton -= OnViewportMouseButton;
            ViewportInputEvents.Instance.ViewportMouseMotion -= OnViewportMouseMotion;
        }
    }

    private void OnViewportMouseButton(ViewportMouseButtonEvent input)
    {
        if (_state == CreationState.Inactive || input.Button != MouseButton.Left)
        {
            return;
        }

        if (_state == CreationState.WaitingForFootprint && input.Pressed)
        {
            StartDrawing(input);
        }
        else if (_state == CreationState.DrawingFootprint && !input.Pressed)
        {
            StartRaisingHeight(input);
        }
        else if (_state == CreationState.RaisingHeight && input.Pressed)
        {
            CommitPrimitive();
        }
    }

    private void OnViewportMouseMotion(ViewportMouseMotionEvent input)
    {
        if (_state == CreationState.Inactive)
        {
            return;
        }

        if (_state == CreationState.DrawingFootprint)
        {
            UpdateFootprint(input);
        }
        else if (_state == CreationState.RaisingHeight)
        {
            UpdateHeight(input.ViewportPosition.Y);
        }
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
        _currentHeight = PrimitiveBounds.DefaultMinimumExtent;
        _state = CreationState.RaisingHeight;
        UpdatePreview(GetCurrentBounds());
    }

    private void UpdateHeight(float currentScreenY)
    {
        float upwardPixels = _heightReferenceScreenY - currentScreenY;
        _currentHeight = Mathf.Max(
            PrimitiveBounds.DefaultMinimumExtent,
            upwardPixels * HeightPerScreenPixel
        );
        UpdatePreview(GetCurrentBounds());
    }

    private void CommitPrimitive()
    {
        SpatialMesh mesh = PrimitiveMeshFactory.Build(_settings, GetCurrentBounds());
        _session.Commands.Execute(
            new CreateMeshCommand(_session, mesh, GetPrimitiveDisplayName(_settings.Kind))
        );

        _state = CreationState.Inactive;
        ClearPreview();
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

    private bool TryPickCreationPoint(Vector3 rayOrigin, Vector3 rayDirection, out Vector3 point)
    {
        // TODO: Try mesh collider picking first, then fall back to the grid so primitive
        // creation can start on existing mesh surfaces.
        return _session.RayPicking.TryPickGrid(rayOrigin, rayDirection, out point);
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
        _session.AddChild(_preview);
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

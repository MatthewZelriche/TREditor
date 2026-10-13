using System;
using System.Collections.Generic;
using Godot;

public enum BezierCurveHandleKind
{
    Anchor,
    Incoming,
    Outgoing,
}

public enum BezierCurveHandleMode
{
    Free,
    Aligned,
    Mirrored,
}

public readonly record struct BezierCurveHandle(int PointIndex, BezierCurveHandleKind Kind);

public readonly record struct BezierCurvePoint(
    Vector3 Position,
    Vector3 Incoming,
    Vector3 Outgoing,
    float Tilt
);

// Immutable Curve3D values for drag cancel / undo consumers. The gizmo stays unaware of document history.
public sealed class BezierCurveSnapshot
{
    private readonly BezierCurvePoint[] _points;

    private BezierCurveSnapshot(Curve3D curve)
    {
        _points = new BezierCurvePoint[curve.PointCount];
        for (int index = 0; index < _points.Length; index++)
        {
            _points[index] = new BezierCurvePoint(
                curve.GetPointPosition(index),
                curve.GetPointIn(index),
                curve.GetPointOut(index),
                curve.GetPointTilt(index)
            );
        }

        BakeInterval = curve.BakeInterval;
        Closed = curve.Closed;
        UpVectorEnabled = curve.UpVectorEnabled;
    }

    public int PointCount => _points.Length;

    public float BakeInterval { get; }

    public bool Closed { get; }

    public bool UpVectorEnabled { get; }

    public BezierCurvePoint this[int index] => _points[index];

    public static BezierCurveSnapshot Capture(Curve3D curve)
    {
        ArgumentNullException.ThrowIfNull(curve);
        return new BezierCurveSnapshot(curve);
    }

    public void ApplyTo(Curve3D curve)
    {
        ArgumentNullException.ThrowIfNull(curve);
        curve.ClearPoints();
        foreach (BezierCurvePoint point in _points)
        {
            curve.AddPoint(point.Position, point.Incoming, point.Outgoing);
            curve.SetPointTilt(curve.PointCount - 1, point.Tilt);
        }

        curve.BakeInterval = BakeInterval;
        curve.Closed = Closed;
        curve.UpVectorEnabled = UpVectorEnabled;
    }
}

// Viewport-independent Curve3D editor. Owns presentation/interaction only; the consumer owns the curve
// and decides when a finished drag becomes an undoable command.
public sealed class BezierCurveGizmo : IDisposable
{
    public const string HandleShaderPath = "res://src/shaders/bezier_curve_handle.gdshader";

    private const float PickRadiusPixels = 13.0f;
    private const float FineMovementScale = 0.1f;

    private static readonly Color CurveColor = new(1.0f, 0.68f, 0.12f);
    private static readonly Color GuideColor = new(0.28f, 0.62f, 0.95f, 0.8f);
    private static readonly Color AnchorColor = new(1.0f, 0.52f, 0.08f);
    private static readonly Color TangentColor = new(0.2f, 0.65f, 1.0f);
    private static readonly Color LockedColor = new(0.45f, 0.47f, 0.52f);
    private static readonly Color HoverColor = new(0.55f, 0.9f, 1.0f);
    private static readonly Color SelectedColor = new(1.0f, 0.9f, 0.18f);

    private readonly Dictionary<Camera3D, Node3D> _hosts = [];
    private readonly Dictionary<int, BezierCurveHandleMode> _handleModes = [];
    private readonly HashSet<BezierCurveHandle> _lockedHandles = [];
    private readonly ImmediateMesh _curveMesh = new();
    private readonly ImmediateMesh _guideMesh = new();
    private readonly QuadMesh _handleQuad = new() { Size = Vector2.One };
    private readonly MultiMesh _anchors = CreateHandleMesh();
    private readonly MultiMesh _tangents = CreateHandleMesh();
    private readonly MultiMesh _locked = CreateHandleMesh();
    private readonly MultiMesh _selected = CreateHandleMesh();
    private readonly MultiMesh _hovered = CreateHandleMesh();
    private readonly StandardMaterial3D _curveMaterial = CreateLineMaterial(CurveColor, 10);
    private readonly StandardMaterial3D _guideMaterial = CreateLineMaterial(GuideColor, 9);
    private readonly Shader _handleShader;
    private readonly ShaderMaterial _anchorMaterial;
    private readonly ShaderMaterial _tangentMaterial;
    private readonly ShaderMaterial _lockedMaterial;
    private readonly ShaderMaterial _selectedMaterial;
    private readonly ShaderMaterial _hoverMaterial;
    private Curve3D? _curve;
    private Transform3D _transform = Transform3D.Identity;
    private Camera3D? _interactionCamera;
    private BezierCurveSnapshot? _dragSnapshot;
    private Vector3 _dragStartIntersection;
    private Vector3 _dragStartWorldPosition;
    private Vector3 _dragPlaneNormal;
    private BezierCurveHandle? _selectedHandle;
    private BezierCurveHandle? _hoveredHandle;
    private int _knownPointCount;
    private bool _suppressCurveChanged;
    private bool _disposed;

    public BezierCurveGizmo()
    {
        _handleShader = GD.Load<Shader>(HandleShaderPath)
            ?? throw new InvalidOperationException($"Failed to load '{HandleShaderPath}'.");
        _anchorMaterial = CreateHandleMaterial(_handleShader, AnchorColor, 11.0f, false);
        _tangentMaterial = CreateHandleMaterial(_handleShader, TangentColor, 10.0f, true);
        _lockedMaterial = CreateHandleMaterial(_handleShader, LockedColor, 10.0f, false);
        _selectedMaterial = CreateHandleMaterial(_handleShader, SelectedColor, 15.0f, false);
        _hoverMaterial = CreateHandleMaterial(_handleShader, HoverColor, 14.0f, false);
        foreach (MultiMesh mesh in HandleMeshes())
            mesh.Mesh = _handleQuad;
    }

    public event Action<BezierCurveHandle?>? SelectionChanged;

    public event Action<BezierCurveHandle>? DragStarted;

    public event Action? PreviewChanged;

    public event Action<BezierCurveHandle, bool>? DragFinished;

    public event Action<int>? PointAdded;

    public event Action<int>? PointRemoved;

    public Curve3D? Curve => _curve;

    public Transform3D Transform => _transform;

    public bool IsVisible => _curve is not null;

    public bool IsInteracting => _interactionCamera is not null;

    public Camera3D? InteractionCamera => _interactionCamera;

    public BezierCurveHandle? SelectedHandle => _selectedHandle;

    public int ViewportCount => _hosts.Count;

    // After camera-plane movement, before Curve3D mutation. Bool is Ctrl/snap request; return constrained world position.
    public Func<BezierCurveHandle, Vector3, bool, Vector3>? PositionConstraint { get; set; }

    public void RegisterViewport(Camera3D camera, Node3D overlayRoot, uint layerMask)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(overlayRoot);
        if (_hosts.ContainsKey(camera))
            return;
        if (layerMask == 0u || (layerMask & (layerMask - 1u)) != 0u)
            throw new ArgumentException("A Bezier gizmo requires one render layer.", nameof(layerMask));

        Node3D host = CreateHost(layerMask);
        host.Transform = _transform;
        host.Visible = IsVisible;
        try
        {
            overlayRoot.AddChild(host);
            _hosts.Add(camera, host);
        }
        catch
        {
            ReleaseHost(host);
            throw;
        }
    }

    public void UnregisterViewport(Camera3D camera)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(camera);
        if (!_hosts.Remove(camera, out Node3D? host))
            return;

        if (ReferenceEquals(_interactionCamera, camera))
            Cancel();
        ReleaseHost(host);
    }

    public void Show(Curve3D curve, Transform3D transform)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(curve);
        ValidateTransform(transform);
        Cancel();

        if (!ReferenceEquals(_curve, curve))
        {
            DisconnectCurve();
            _curve = curve;
            _curve.Changed += OnCurveChanged;
            _handleModes.Clear();
            _lockedHandles.Clear();
            SetSelected(null);
        }

        _transform = transform;
        _knownPointCount = curve.PointCount;
        foreach (Node3D host in _hosts.Values)
        {
            host.Transform = transform;
            host.Visible = true;
        }

        RefreshVisuals();
    }

    public void SetTransform(Transform3D transform)
    {
        ThrowIfDisposed();
        ValidateTransform(transform);
        Cancel();
        _transform = transform;
        foreach (Node3D host in _hosts.Values)
            host.Transform = transform;
    }

    public void Hide()
    {
        ThrowIfDisposed();
        Cancel();
        DisconnectCurve();
        _handleModes.Clear();
        _lockedHandles.Clear();
        SetSelected(null);
        SetHovered(null);
        foreach (Node3D host in _hosts.Values)
            host.Visible = false;
        ClearVisuals();
    }

    // Locked handles can be selected but do not begin a drag. Returns true if a handle consumed the press.
    public bool BeginInteraction(Camera3D camera, Vector2 screenPosition)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(camera);
        Cancel();
        if (_curve is null || !_hosts.ContainsKey(camera))
            return false;
        if (!TryPickHandle(camera, screenPosition, out BezierCurveHandle handle))
        {
            SetSelected(null);
            return false;
        }

        SetSelected(handle);
        if (_lockedHandles.Contains(handle))
            return true;

        Vector3 worldPosition = GetWorldPosition(handle);
        _dragPlaneNormal = -camera.GlobalBasis.Z.Normalized();
        if (
            _dragPlaneNormal.IsZeroApprox()
            || !GeometryUtils.TryIntersectPlane(camera, screenPosition, worldPosition, _dragPlaneNormal, out _dragStartIntersection)
        )
        {
            return true;
        }

        _dragStartWorldPosition = worldPosition;
        _dragSnapshot = BezierCurveSnapshot.Capture(_curve);
        _interactionCamera = camera;
        DragStarted?.Invoke(handle);
        return true;
    }

    public bool UpdateInteraction(
        Camera3D camera,
        Vector2 screenPosition,
        bool fineMovement = false,
        bool snapping = false
    )
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(camera);
        if (
            !ReferenceEquals(_interactionCamera, camera)
            || _selectedHandle is not BezierCurveHandle handle
            || _curve is null
            || !GeometryUtils.TryIntersectPlane(camera, screenPosition, _dragStartWorldPosition, _dragPlaneNormal, out Vector3 intersection)
        )
        {
            return false;
        }

        Vector3 delta = intersection - _dragStartIntersection;
        if (fineMovement)
            delta *= FineMovementScale;
        Vector3 proposed = _dragStartWorldPosition + delta;
        if (PositionConstraint is { } constraint)
            proposed = constraint(handle, proposed, snapping);
        if (!proposed.IsFinite())
            return false;

        ApplyWorldPosition(handle, proposed);
        return true;
    }

    // Ends the preview drag; the consumer decides what DragFinished means for history.
    public void EndInteraction()
    {
        ThrowIfDisposed();
        if (!TryFinishInteraction(out BezierCurveHandle handle))
            return;
        _dragSnapshot = null;
        DragFinished?.Invoke(handle, false);
    }

    public void Cancel()
    {
        ThrowIfDisposed();
        if (!TryFinishInteraction(out BezierCurveHandle handle))
            return;

        if (_curve is not null && _dragSnapshot is { } snapshot)
        {
            MutateCurve(() => snapshot.ApplyTo(_curve));
            RefreshVisuals();
            PreviewChanged?.Invoke();
        }

        _dragSnapshot = null;
        DragFinished?.Invoke(handle, true);
    }

    public bool Select(BezierCurveHandle? handle)
    {
        ThrowIfDisposed();
        if (handle is BezierCurveHandle value && !IsValidHandle(value))
            return false;
        SetSelected(handle);
        return true;
    }

    public void UpdateHover(Camera3D camera, Vector2 screenPosition)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(camera);
        SetHovered(
            _curve is not null
                && _hosts.ContainsKey(camera)
                && TryPickHandle(camera, screenPosition, out BezierCurveHandle handle)
                ? handle
                : null
        );
    }

    public int AddPoint(Vector3 worldPosition, int index = -1)
    {
        ThrowIfDisposed();
        Curve3D curve = RequireCurve();
        if (!worldPosition.IsFinite())
            throw new ArgumentException("A curve point must be finite.", nameof(worldPosition));
        if (index < -1 || index > curve.PointCount)
            throw new ArgumentOutOfRangeException(nameof(index));

        Cancel();
        int inserted = index < 0 ? curve.PointCount : index;
        curve.AddPoint(_transform.AffineInverse() * worldPosition, index: index);
        ResetPointMetadata();
        SetSelected(new BezierCurveHandle(inserted, BezierCurveHandleKind.Anchor));
        PointAdded?.Invoke(inserted);
        return inserted;
    }

    public bool RemovePoint(int index)
    {
        ThrowIfDisposed();
        Curve3D curve = RequireCurve();
        if (index < 0 || index >= curve.PointCount)
            return false;
        if (_lockedHandles.Contains(new BezierCurveHandle(index, BezierCurveHandleKind.Anchor)))
            return false;

        Cancel();
        curve.RemovePoint(index);
        ResetPointMetadata();
        SetSelected(null);
        PointRemoved?.Invoke(index);
        return true;
    }

    public void SetHandleLocked(BezierCurveHandle handle, bool locked)
    {
        ThrowIfDisposed();
        if (!IsValidHandle(handle))
            throw new ArgumentOutOfRangeException(nameof(handle));
        Cancel();
        if (locked)
            _lockedHandles.Add(handle);
        else
            _lockedHandles.Remove(handle);
        RefreshVisuals();
    }

    public bool IsHandleLocked(BezierCurveHandle handle) => _lockedHandles.Contains(handle);

    public void SetHandleMode(int pointIndex, BezierCurveHandleMode mode)
    {
        ThrowIfDisposed();
        RequirePointIndex(pointIndex);
        _handleModes[pointIndex] = mode;
    }

    public BezierCurveHandleMode GetHandleMode(int pointIndex)
    {
        ThrowIfDisposed();
        RequirePointIndex(pointIndex);
        return _handleModes.GetValueOrDefault(pointIndex, BezierCurveHandleMode.Free);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        if (IsInteracting)
            Cancel();
        _disposed = true;
        DisconnectCurve();
        foreach (Node3D host in _hosts.Values)
            ReleaseHost(host);
        _hosts.Clear();
        ClearVisuals();
        foreach (MultiMesh mesh in HandleMeshes())
        {
            mesh.Mesh = null;
            mesh.Dispose();
        }
        _curveMesh.Dispose();
        _guideMesh.Dispose();
        _handleQuad.Dispose();
        _curveMaterial.Dispose();
        _guideMaterial.Dispose();
        _anchorMaterial.Dispose();
        _tangentMaterial.Dispose();
        _lockedMaterial.Dispose();
        _selectedMaterial.Dispose();
        _hoverMaterial.Dispose();
        _handleShader.Dispose();
        SelectionChanged = null;
        DragStarted = null;
        PreviewChanged = null;
        DragFinished = null;
        PointAdded = null;
        PointRemoved = null;
    }

    private bool TryFinishInteraction(out BezierCurveHandle handle)
    {
        if (_interactionCamera is null || _selectedHandle is not BezierCurveHandle selected)
        {
            handle = default;
            return false;
        }

        _interactionCamera = null;
        handle = selected;
        return true;
    }

    private void ApplyWorldPosition(BezierCurveHandle handle, Vector3 worldPosition)
    {
        Curve3D curve = RequireCurve();
        Vector3 local = _transform.AffineInverse() * worldPosition;
        MutateCurve(() =>
        {
            switch (handle.Kind)
            {
                case BezierCurveHandleKind.Anchor:
                    curve.SetPointPosition(handle.PointIndex, local);
                    break;
                case BezierCurveHandleKind.Incoming:
                case BezierCurveHandleKind.Outgoing:
                    ApplyTangent(handle, local - curve.GetPointPosition(handle.PointIndex));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(handle));
            }
        });
        RefreshVisuals();
        PreviewChanged?.Invoke();
    }

    private void ApplyTangent(BezierCurveHandle handle, Vector3 offset)
    {
        Curve3D curve = RequireCurve();
        bool incoming = handle.Kind == BezierCurveHandleKind.Incoming;
        SetTangent(curve, handle.PointIndex, incoming, offset);

        BezierCurveHandleMode mode = GetHandleMode(handle.PointIndex);
        if (mode == BezierCurveHandleMode.Free || offset.IsZeroApprox())
            return;

        Vector3 opposite = GetTangent(curve, handle.PointIndex, !incoming);
        float length = mode == BezierCurveHandleMode.Mirrored ? offset.Length() : opposite.Length();
        SetTangent(curve, handle.PointIndex, !incoming, -offset.Normalized() * length);
    }

    private static void SetTangent(Curve3D curve, int pointIndex, bool incoming, Vector3 offset)
    {
        if (incoming)
            curve.SetPointIn(pointIndex, offset);
        else
            curve.SetPointOut(pointIndex, offset);
    }

    private static Vector3 GetTangent(Curve3D curve, int pointIndex, bool incoming) =>
        incoming ? curve.GetPointIn(pointIndex) : curve.GetPointOut(pointIndex);

    private void MutateCurve(Action mutation)
    {
        _suppressCurveChanged = true;
        try
        {
            mutation();
        }
        finally
        {
            _suppressCurveChanged = false;
        }
    }

    private void OnCurveChanged()
    {
        if (_suppressCurveChanged || _curve is null)
            return;
        if (_knownPointCount != _curve.PointCount)
            ResetPointMetadata();
        RefreshVisuals();
        PreviewChanged?.Invoke();
    }

    private void RefreshVisuals()
    {
        if (_curve is null)
        {
            ClearVisuals();
            return;
        }

        _knownPointCount = _curve.PointCount;
        RebuildCurveMesh();

        var anchors = new List<Transform3D>(_curve.PointCount);
        var tangents = new List<Transform3D>(_curve.PointCount * 2);
        var locked = new List<Transform3D>();
        var guides = new List<Vector3>();
        foreach (BezierCurveHandle handle in EnumerateVisibleHandles())
        {
            Vector3 position = GetLocalPosition(handle);
            AddMarker(handle, position, handle.Kind == BezierCurveHandleKind.Anchor ? anchors : tangents, locked);
            if (handle.Kind == BezierCurveHandleKind.Anchor)
                continue;
            guides.Add(_curve.GetPointPosition(handle.PointIndex));
            guides.Add(position);
        }

        SetInstances(_anchors, anchors);
        SetInstances(_tangents, tangents);
        SetInstances(_locked, locked);
        SetSingleMarker(_selected, _selectedHandle);
        SetSingleMarker(_hovered, _hoveredHandle == _selectedHandle ? null : _hoveredHandle);

        _guideMesh.ClearSurfaces();
        if (guides.Count == 0)
            return;
        _guideMesh.SurfaceBegin(Mesh.PrimitiveType.Lines, _guideMaterial);
        foreach (Vector3 point in guides)
            _guideMesh.SurfaceAddVertex(point);
        _guideMesh.SurfaceEnd();
    }

    private void RebuildCurveMesh()
    {
        _curveMesh.ClearSurfaces();
        Vector3[] points = _curve!.Tessellate();
        if (points.Length < 2)
            return;
        _curveMesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip, _curveMaterial);
        foreach (Vector3 point in points)
            _curveMesh.SurfaceAddVertex(point);
        _curveMesh.SurfaceEnd();
    }

    private void AddMarker(
        BezierCurveHandle handle,
        Vector3 position,
        List<Transform3D> ordinary,
        List<Transform3D> locked
    ) =>
        (_lockedHandles.Contains(handle) ? locked : ordinary).Add(
            new Transform3D(Basis.Identity, position)
        );

    private void SetSingleMarker(MultiMesh mesh, BezierCurveHandle? handle)
    {
        if (handle is not BezierCurveHandle value || !IsValidHandle(value))
        {
            mesh.InstanceCount = 0;
            return;
        }

        mesh.InstanceCount = 1;
        mesh.SetInstanceTransform(0, new Transform3D(Basis.Identity, GetLocalPosition(value)));
    }

    private static void SetInstances(MultiMesh mesh, IReadOnlyList<Transform3D> transforms)
    {
        mesh.InstanceCount = 0;
        mesh.InstanceCount = transforms.Count;
        for (int index = 0; index < transforms.Count; index++)
            mesh.SetInstanceTransform(index, transforms[index]);
    }

    private bool TryPickHandle(
        Camera3D camera,
        Vector2 screenPosition,
        out BezierCurveHandle picked
    )
    {
        picked = default;
        float closest = PickRadiusPixels * PickRadiusPixels;
        bool found = false;
        foreach (BezierCurveHandle handle in EnumerateVisibleHandles())
        {
            Vector3 world = GetWorldPosition(handle);
            if (camera.IsPositionBehind(world))
                continue;
            float distance = camera.UnprojectPosition(world).DistanceSquaredTo(screenPosition);
            if (distance > closest)
                continue;
            closest = distance;
            picked = handle;
            found = true;
        }
        return found;
    }

    private IEnumerable<BezierCurveHandle> EnumerateVisibleHandles()
    {
        Curve3D curve = RequireCurve();
        for (int index = 0; index < curve.PointCount; index++)
        {
            yield return new BezierCurveHandle(index, BezierCurveHandleKind.Anchor);
            if (index > 0)
                yield return new BezierCurveHandle(index, BezierCurveHandleKind.Incoming);
            if (index + 1 < curve.PointCount)
                yield return new BezierCurveHandle(index, BezierCurveHandleKind.Outgoing);
        }
    }

    private bool IsValidHandle(BezierCurveHandle handle)
    {
        if (_curve is null || handle.PointIndex < 0 || handle.PointIndex >= _curve.PointCount)
            return false;
        return handle.Kind switch
        {
            BezierCurveHandleKind.Anchor => true,
            BezierCurveHandleKind.Incoming => handle.PointIndex > 0,
            BezierCurveHandleKind.Outgoing => handle.PointIndex + 1 < _curve.PointCount,
            _ => false,
        };
    }

    private Vector3 GetLocalPosition(BezierCurveHandle handle)
    {
        Curve3D curve = RequireCurve();
        Vector3 anchor = curve.GetPointPosition(handle.PointIndex);
        return handle.Kind switch
        {
            BezierCurveHandleKind.Anchor => anchor,
            BezierCurveHandleKind.Incoming or BezierCurveHandleKind.Outgoing =>
                anchor + GetDisplayTangent(handle.PointIndex, handle.Kind),
            _ => throw new ArgumentOutOfRangeException(nameof(handle)),
        };
    }

    // Curve3D collapses straight-segment tangents to zero; stacked controls can't be picked. Show a
    // non-authoritative one-third chord until the first drag writes the displayed offset.
    private Vector3 GetDisplayTangent(int pointIndex, BezierCurveHandleKind kind)
    {
        Curve3D curve = RequireCurve();
        Vector3 tangent = GetTangent(curve, pointIndex, kind == BezierCurveHandleKind.Incoming);
        if (!tangent.IsZeroApprox())
            return tangent;

        int neighbor = kind == BezierCurveHandleKind.Incoming ? pointIndex - 1 : pointIndex + 1;
        Vector3 chord = curve.GetPointPosition(neighbor) - curve.GetPointPosition(pointIndex);
        return chord.IsZeroApprox() ? Vector3.Right : chord / 3.0f;
    }

    private Vector3 GetWorldPosition(BezierCurveHandle handle) => _transform * GetLocalPosition(handle);

    private void SetSelected(BezierCurveHandle? handle)
    {
        if (_selectedHandle == handle)
            return;
        _selectedHandle = handle;
        SetSingleMarker(_selected, handle);
        SelectionChanged?.Invoke(handle);
    }

    private void SetHovered(BezierCurveHandle? handle)
    {
        if (_hoveredHandle == handle)
            return;
        _hoveredHandle = handle;
        SetSingleMarker(_hovered, handle == _selectedHandle ? null : handle);
    }

    private void ResetPointMetadata()
    {
        _handleModes.Clear();
        _lockedHandles.Clear();
        _knownPointCount = _curve?.PointCount ?? 0;
        if (_selectedHandle is BezierCurveHandle selected && !IsValidHandle(selected))
            SetSelected(null);
        if (_hoveredHandle is BezierCurveHandle hovered && !IsValidHandle(hovered))
            SetHovered(null);
    }

    private void DisconnectCurve()
    {
        if (_curve is not null)
            _curve.Changed -= OnCurveChanged;
        _curve = null;
        _knownPointCount = 0;
    }

    private void ClearVisuals()
    {
        _curveMesh.ClearSurfaces();
        _guideMesh.ClearSurfaces();
        foreach (MultiMesh mesh in HandleMeshes())
            mesh.InstanceCount = 0;
    }

    private Node3D CreateHost(uint layerMask)
    {
        var host = new Node3D { Name = "BezierCurveGizmo", Visible = false };
        host.AddChild(CreateMeshInstance("Curve", _curveMesh, layerMask));
        host.AddChild(CreateMeshInstance("Guides", _guideMesh, layerMask));
        host.AddChild(CreateMultiMeshInstance("Anchors", _anchors, _anchorMaterial, layerMask));
        host.AddChild(CreateMultiMeshInstance("Tangents", _tangents, _tangentMaterial, layerMask));
        host.AddChild(CreateMultiMeshInstance("Locked", _locked, _lockedMaterial, layerMask));
        host.AddChild(CreateMultiMeshInstance("Selected", _selected, _selectedMaterial, layerMask));
        host.AddChild(CreateMultiMeshInstance("Hovered", _hovered, _hoverMaterial, layerMask));
        return host;
    }

    // Detach shared resources before Free — RenderingServer may keep instances a frame past disposal.
    private static void ReleaseHost(Node3D host)
    {
        if (!GodotObject.IsInstanceValid(host))
            return;
        foreach (Node child in host.GetChildren())
        {
            if (child is MultiMeshInstance3D multiMeshInstance)
            {
                multiMeshInstance.MaterialOverride = null;
                multiMeshInstance.Multimesh = null;
            }
            else if (child is MeshInstance3D meshInstance)
            {
                meshInstance.MaterialOverride = null;
                meshInstance.Mesh = null;
            }
        }

        if (host.GetParent() is Node parent)
            parent.RemoveChild(host);
        host.Free();
    }

    private static MeshInstance3D CreateMeshInstance(string name, Mesh mesh, uint layerMask)
    {
        var instance = new MeshInstance3D { Name = name, Mesh = mesh };
        ConfigureOverlay(instance, layerMask);
        return instance;
    }

    private static MultiMeshInstance3D CreateMultiMeshInstance(
        string name,
        MultiMesh mesh,
        Material material,
        uint layerMask
    )
    {
        var instance = new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = mesh,
            MaterialOverride = material,
        };
        ConfigureOverlay(instance, layerMask);
        return instance;
    }

    private static void ConfigureOverlay(GeometryInstance3D instance, uint layerMask)
    {
        instance.Layers = layerMask;
        instance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        instance.IgnoreOcclusionCulling = true;
    }

    private IEnumerable<MultiMesh> HandleMeshes()
    {
        yield return _anchors;
        yield return _tangents;
        yield return _locked;
        yield return _selected;
        yield return _hovered;
    }

    private static MultiMesh CreateHandleMesh() =>
        new() { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D };

    private static StandardMaterial3D CreateLineMaterial(Color color, int renderPriority) =>
        new()
        {
            AlbedoColor = color,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            NoDepthTest = true,
            DisableFog = true,
            RenderPriority = renderPriority,
        };

    private static ShaderMaterial CreateHandleMaterial(
        Shader shader,
        Color color,
        float size,
        bool diamond
    )
    {
        var material = new ShaderMaterial
        {
            Shader = shader,
            RenderPriority = (int)Material.RenderPriorityMax,
        };
        material.SetShaderParameter("handle_color", color);
        material.SetShaderParameter("handle_size_pixels", size);
        material.SetShaderParameter("diamond", diamond);
        return material;
    }

    private static void ValidateTransform(Transform3D transform)
    {
        float determinant = transform.Basis.Determinant();
        if (
            !transform.Origin.IsFinite()
            || !transform.Basis.X.IsFinite()
            || !transform.Basis.Y.IsFinite()
            || !transform.Basis.Z.IsFinite()
            || !float.IsFinite(determinant)
            || Mathf.IsZeroApprox(determinant)
        )
        {
            throw new ArgumentException("A Bezier gizmo transform must be finite and invertible.", nameof(transform));
        }
    }

    private void RequirePointIndex(int pointIndex)
    {
        if (_curve is null || pointIndex < 0 || pointIndex >= _curve.PointCount)
            throw new ArgumentOutOfRangeException(nameof(pointIndex));
    }

    private Curve3D RequireCurve() =>
        _curve ?? throw new InvalidOperationException("The Bezier gizmo is not showing a curve.");

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

using System;
using System.Collections.Generic;
using Gizmo3DPlugin;
using Godot;

public sealed class SelectionTranslationGizmoController : IDisposable
{
    private readonly EditorSceneService _scene;
    private readonly SelectionService _selection;
    private readonly List<GizmoRegistration> _registrations = [];

    private bool _active;
    private bool _disposed;
    private bool _dragging;
    private GizmoRegistration _dragRegistration;
    private SelectionSnapshot _dragSelection = SelectionSnapshot.Empty;
    private Vector3 _dragCenter;
    private Vector3 _dragDelta;

    public SelectionTranslationGizmoController(EditorSceneService scene, SelectionService selection)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(selection);

        _scene = scene;
        _selection = selection;
        _selection.SelectionChanged += OnSelectionChanged;
    }

    public event Action<EditorCommand> CommandSubmitted;
    public event Action<EditorPreviewRequest> PreviewSubmitted;

    public void SetActive(bool active)
    {
        if (_active == active)
        {
            return;
        }

        _active = active;
        if (!_active)
        {
            CancelDrag();
        }

        UpdateGizmos();
    }

    public void Refresh()
    {
        if (!_dragging)
        {
            UpdateGizmos();
        }
    }

    public void Register(Gizmo3D gizmo, Node3D target)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(gizmo);
        ArgumentNullException.ThrowIfNull(target);

        GizmoRegistration registration = new(gizmo, target);
        registration.TransformBegin = mode => OnTransformBegin(registration, mode);
        registration.TransformChanged = (mode, value) =>
            OnTransformChanged(registration, mode, value);
        registration.TransformEnd = mode => OnTransformEnd(registration, mode);

        ConfigureGizmo(registration);
        gizmo.TransformBegin += registration.TransformBegin;
        gizmo.TransformChanged += registration.TransformChanged;
        gizmo.TransformEnd += registration.TransformEnd;
        _registrations.Add(registration);
        UpdateGizmos();
    }

    public void Unregister(Gizmo3D gizmo)
    {
        if (gizmo == null)
        {
            return;
        }

        GizmoRegistration registration = _registrations.Find(item => item.Gizmo == gizmo);
        if (registration == null)
        {
            return;
        }

        if (_dragRegistration == registration)
        {
            CancelDrag();
        }

        registration.Gizmo.TransformBegin -= registration.TransformBegin;
        registration.Gizmo.TransformChanged -= registration.TransformChanged;
        registration.Gizmo.TransformEnd -= registration.TransformEnd;
        _registrations.Remove(registration);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelDrag();
        _selection.SelectionChanged -= OnSelectionChanged;

        foreach (GizmoRegistration registration in _registrations.ToArray())
        {
            Unregister(registration.Gizmo);
        }
    }

    private void OnSelectionChanged()
    {
        if (!_dragging)
        {
            UpdateGizmos();
        }
    }

    private void OnTransformBegin(GizmoRegistration registration, int mode)
    {
        if (
            !_active
            || (Gizmo3D.TransformMode)mode != Gizmo3D.TransformMode.Translate
            || !_scene.TryGetSelectionCenter(_selection.Current, out _dragCenter)
        )
        {
            return;
        }

        _dragging = true;
        _dragRegistration = registration;
        _dragSelection = _selection.Current;
        _dragDelta = Vector3.Zero;
    }

    private void OnTransformChanged(GizmoRegistration registration, int mode, Vector3 value)
    {
        if (
            !_dragging
            || _dragRegistration != registration
            || (Gizmo3D.TransformMode)mode != Gizmo3D.TransformMode.Translate
        )
        {
            return;
        }

        _dragDelta = registration.Target.GlobalPosition - _dragCenter;
        PreviewSubmitted?.Invoke(
            new EditorPreviewRequest.TranslateSelection(_dragSelection, _dragDelta)
        );
        UpdateGizmoTargets(_dragCenter + _dragDelta, registration);
    }

    private void OnTransformEnd(GizmoRegistration registration, int mode)
    {
        if (!_dragging || _dragRegistration != registration)
        {
            return;
        }

        Vector3 finalDelta = _dragDelta;
        SelectionSnapshot finalSelection = _dragSelection;

        ClearDragState();
        PreviewSubmitted?.Invoke(new EditorPreviewRequest.Clear());

        TranslateSelectionCommand command = TranslateSelectionCommand.CreateIfChanged(
            finalSelection,
            finalDelta
        );
        if (command != null)
        {
            CommandSubmitted?.Invoke(command);
        }

        UpdateGizmos();
    }

    private void CancelDrag()
    {
        if (!_dragging)
        {
            return;
        }

        ClearDragState();
        PreviewSubmitted?.Invoke(new EditorPreviewRequest.Clear());
    }

    private void ClearDragState()
    {
        _dragging = false;
        _dragRegistration = null;
        _dragSelection = SelectionSnapshot.Empty;
        _dragCenter = Vector3.Zero;
        _dragDelta = Vector3.Zero;
    }

    private void UpdateGizmos()
    {
        if (!_active || !_scene.TryGetSelectionCenter(_selection.Current, out Vector3 center))
        {
            HideGizmos();
            return;
        }

        UpdateGizmoTargets(center, except: null);
    }

    private void UpdateGizmoTargets(Vector3 center, GizmoRegistration except)
    {
        foreach (GizmoRegistration registration in _registrations.ToArray())
        {
            if (!IsValid(registration.Gizmo) || !IsValid(registration.Target))
            {
                _registrations.Remove(registration);
                continue;
            }

            if (registration == except)
            {
                continue;
            }

            registration.Target.GlobalPosition = center;
            if (!registration.Gizmo.IsSelected(registration.Target))
            {
                registration.Gizmo.Select(registration.Target);
            }

            registration.Gizmo.Visible = _active;
        }
    }

    private void HideGizmos()
    {
        foreach (GizmoRegistration registration in _registrations.ToArray())
        {
            if (!IsValid(registration.Gizmo))
            {
                _registrations.Remove(registration);
                continue;
            }

            registration.Gizmo.ClearSelection();
            registration.Gizmo.Visible = false;
        }
    }

    private static void ConfigureGizmo(GizmoRegistration registration)
    {
        registration.Gizmo.Mode = Gizmo3D.ToolMode.Move;
        registration.Gizmo.UseLocalSpace = false;
        registration.Gizmo.ShowSelectionBox = false;
        registration.Gizmo.ShowRotationLine = false;
        registration.Gizmo.ShowRotationArc = false;
        registration.Gizmo.Visible = false;
    }

    private static bool IsValid(GodotObject instance) =>
        instance != null && GodotObject.IsInstanceValid(instance);

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class GizmoRegistration
    {
        public GizmoRegistration(Gizmo3D gizmo, Node3D target)
        {
            Gizmo = gizmo;
            Target = target;
        }

        public Gizmo3D Gizmo { get; }
        public Node3D Target { get; }
        public Gizmo3D.TransformBeginEventHandler TransformBegin { get; set; }
        public Gizmo3D.TransformChangedEventHandler TransformChanged { get; set; }
        public Gizmo3D.TransformEndEventHandler TransformEnd { get; set; }
    }
}

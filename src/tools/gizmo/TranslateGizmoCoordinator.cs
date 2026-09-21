using System;
using System.Collections.Generic;
using Godot;

// The editor's owner of every viewport-local translate gizmo. Panes register their camera and overlay
// root as they come and go; tools drive interaction through this type and never touch a Gizmo3D node.
//
// All hosts move one shared, non-rendering proxy node. The gizmo is only ever allowed to mutate that
// proxy, so a drag can never reach a document object, a render mesh, or any other authoritative node.
public sealed class TranslateGizmoCoordinator : ISceneService
{
    private readonly Dictionary<Camera3D, HostRegistration> _hosts = [];
    private readonly GizmoLayerPool _layers = new();
    private readonly Node3D _proxy;
    private readonly SelectionState _selection;
    private readonly PreviewState _previews;
    private readonly SnapSettings _snap;
    private Document? _document;
    private Camera3D? _interactionCamera;
    private bool _attached;
    private bool _enabled = true;
    private bool _disposed;

    public TranslateGizmoCoordinator(
        Node3D proxyParent,
        SelectionState selection,
        PreviewState previews,
        SnapSettings snap
    )
    {
        ArgumentNullException.ThrowIfNull(proxyParent);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(previews);
        ArgumentNullException.ThrowIfNull(snap);

        _selection = selection;
        _previews = previews;
        _snap = snap;
        _proxy = new Node3D { Name = "TranslateGizmoProxy" };
        try
        {
            proxyParent.AddChild(_proxy);
            _selection.Changed += Synchronize;
        }
        catch
        {
            _proxy.Free();
            throw;
        }
    }

    public int ViewportCount => _hosts.Count;

    // True between a gizmo-hit mouse down and the matching mouse up or cancellation.
    public bool IsInteracting => _interactionCamera is not null;

    // The camera whose host owns the active interaction. At most one host may own one at a time.
    public Camera3D? InteractionCamera => _interactionCamera;

    // Whether a gizmo is currently shown for the shared proxy.
    public bool IsVisible => _attached;

    // The proxy's absolute transform, which is what a drag produces and what the tool previews.
    public Transform3D ProxyTransform => _proxy.GlobalTransform;

    // Returns the private render layer allocated to this viewport. Other viewport-local overlays
    // may share it and remain isolated by the camera cull mask configured here.
    public uint RegisterViewport(Camera3D camera, Node3D overlayRoot)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(overlayRoot);
        if (_hosts.TryGetValue(camera, out HostRegistration existingRegistration))
            return existingRegistration.LayerMask;

        uint layerMask = _layers.Allocate();
        uint previousCullMask = camera.CullMask;
        var host = new TranslateGizmoHost(_snap);
        host.SetRenderLayer(layerMask);
        try
        {
            overlayRoot.AddChild(host);
        }
        catch
        {
            _layers.Release(layerMask);
            host.Free();
            throw;
        }

        // The panes share one World3D, so this camera must drop every other host's layer and keep only
        // its own.
        camera.CullMask = (previousCullMask & ~GizmoLayerPool.ReservedMask) | layerMask;
        _hosts.Add(camera, new HostRegistration(host, layerMask, previousCullMask));
        if (_attached)
            host.Attach(_proxy);

        return layerMask;
    }

    // SceneServices owns document and preview lifecycle. Committed transforms arrive through
    // OnDocumentChanged; preview transforms arrive through OnPreviewTransformChanged. The
    // coordinator needs no parallel event subscriptions in Application.
    public void SetDocument(Document document)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(document);

        Hide();
        _document = document;
    }

    public void OnDocumentChanged(DocumentChangeSet changeSet)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(changeSet);

        DocumentObjectChange change = changeSet.Change;
        switch (change.Kind)
        {
            case DocumentChangeKind.Removed:
                if (_selection.Selected == change.ObjectId)
                    Hide();
                break;
            case DocumentChangeKind.TransformChanged:
                if (_selection.Selected == change.ObjectId)
                    Synchronize();
                break;
        }
    }

    public void OnPreviewTransformChanged(MeshObjectId id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_selection.Selected == id)
            Synchronize();
    }

    public void Flush() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void UnregisterViewport(Camera3D camera)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(camera);
        if (!_hosts.Remove(camera, out HostRegistration registration))
            return;

        // A viewport that goes away mid-drag ends the interaction without committing anything.
        if (ReferenceEquals(_interactionCamera, camera))
        {
            registration.Host.EndInteraction();
            _interactionCamera = null;
        }

        RestoreCullMask(camera, registration.PreviousCullMask);
        _layers.Release(registration.LayerMask);
        registration.Host.Detach();
        registration.Host.QueueFree();
    }

    // Whether the translate tool currently owns interaction. When false the gizmo stays hidden even
    // if a selection exists, so a create tool cannot leave a dead gizmo on screen.
    public void SetEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_enabled == enabled)
            return;

        _enabled = enabled;
        if (_enabled)
            Synchronize();
        else
            Hide();
    }

    // Places the shared proxy and shows a gizmo in every viewport.
    public void ShowAt(Transform3D transform)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_enabled)
            return;

        _proxy.GlobalTransform = transform;
        _attached = true;
        foreach (HostRegistration registration in _hosts.Values)
            registration.Host.Attach(_proxy);
    }

    // Hides every gizmo. Used when nothing is selected, when translate is not the active tool, and
    // during scene and application lifecycle transitions.
    public void Hide()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Cancel();
        _attached = false;
        foreach (HostRegistration registration in _hosts.Values)
            registration.Host.Detach();
    }

    // Reports whether the press landed on the gizmo belonging to the pressed camera. A miss leaves no
    // interaction behind, so the caller is free to treat the press as a scene click.
    public bool BeginInteraction(Camera3D camera, Vector2 screenPosition)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(camera);

        Cancel();
        if (!_attached || !_hosts.TryGetValue(camera, out HostRegistration registration))
            return false;

        if (!registration.Host.BeginInteraction(screenPosition))
            return false;

        _interactionCamera = camera;
        return true;
    }

    // Advances the active drag. Input from any other camera is ignored so a second pane cannot steal
    // an interaction that is already owned.
    public bool UpdateInteraction(Camera3D camera, Vector2 screenPosition)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(camera);
        if (!ReferenceEquals(_interactionCamera, camera))
            return false;

        _hosts[camera].Host.UpdateInteraction(screenPosition);
        return true;
    }

    // Ends the drag and leaves the proxy where it was dragged to. Committing is the tool's job.
    public void EndInteraction()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Cancel();
    }

    // Drops any active interaction without committing. The proxy keeps whatever transform the drag
    // produced; callers that need the original back place it with ShowAt.
    public void Cancel()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_interactionCamera is null)
            return;

        if (_hosts.TryGetValue(_interactionCamera, out HostRegistration registration))
            registration.Host.EndInteraction();

        _interactionCamera = null;
    }

    // Cancels an abandoned drag and restores the proxy from preview or committed state. Completing a
    // drag uses Cancel directly so its final proxy transform remains available to the tool for commit.
    public void CancelAndSynchronize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Cancel();
        Synchronize();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _selection.Changed -= Synchronize;
        _document = null;
        _interactionCamera = null;
        _attached = false;
        foreach ((Camera3D camera, HostRegistration registration) in _hosts)
        {
            RestoreCullMask(camera, registration.PreviousCullMask);
            registration.Host.Detach();
            registration.Host.QueueFree();
        }

        _hosts.Clear();
        _proxy.QueueFree();
    }

    // Places the shared proxy on whatever the selected object currently looks like, preferring an
    // active preview over the committed transform. A drag already owns the proxy, so it is left alone.
    private void Synchronize()
    {
        if (_disposed || IsInteracting || !_enabled)
            return;

        if (
            _document is Document document
            && _selection.Selected is MeshObjectId id
            && TryGetDisplayedTransform(document, id, out Transform3D transform)
        )
        {
            ShowAt(transform);
            return;
        }

        Hide();
    }

    private bool TryGetDisplayedTransform(Document document, MeshObjectId id, out Transform3D transform)
    {
        if (_previews.TryGetTransform(id, out transform))
            return true;

        return document.TryGetMeshTransform(id, out transform);
    }

    // A pane torn down by Godot can free its camera before the coordinator hears about it, and there
    // is nothing left to restore in that case.
    private static void RestoreCullMask(Camera3D camera, uint cullMask)
    {
        if (GodotObject.IsInstanceValid(camera))
            camera.CullMask = cullMask;
    }

    private readonly record struct HostRegistration(
        TranslateGizmoHost Host,
        uint LayerMask,
        uint PreviousCullMask
    );
}

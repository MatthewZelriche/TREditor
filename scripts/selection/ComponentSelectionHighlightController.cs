using System;
using System.Collections.Generic;
using Godot;

public sealed class ComponentSelectionHighlightController : IDisposable
{
    private readonly EditorSceneService _scene;
    private readonly SelectionService _selection;
    private readonly Dictionary<EditorObjectId, ComponentSelectionOverlay> _overlays = [];

    private SelectionTarget? _hover;
    private Vector3 _cameraOrigin;
    private ComponentHighlightMode _mode = ComponentHighlightMode.Edit;
    private bool _active;
    private bool _disposed;

    public ComponentSelectionHighlightController(
        EditorSceneService scene,
        SelectionService selection
    )
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(selection);

        _scene = scene;
        _selection = selection;
        _selection.SelectionChanged += Sync;
    }

    public void SetActive(bool active)
    {
        if (_active == active)
        {
            return;
        }

        _active = active;
        if (!active)
        {
            _hover = null;
        }

        Sync();
    }

    public void SetMode(ComponentHighlightMode mode)
    {
        if (_mode == mode)
        {
            return;
        }

        _mode = mode;
        if (_hover.HasValue && !_mode.AllowsHover(_hover.Value))
        {
            _hover = null;
        }
        Sync();
    }

    public void SetPointerState(Vector3 cameraOrigin, SelectionTarget? hover)
    {
        _cameraOrigin = cameraOrigin;
        if (hover.HasValue && !_mode.AllowsHover(hover.Value))
        {
            hover = null;
        }
        if (_hover == hover)
        {
            Sync();
            return;
        }

        _hover = hover;
        Sync();
    }

    public void Refresh()
    {
        Sync();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _selection.SelectionChanged -= Sync;
        ClearOverlays();
    }

    private void Sync()
    {
        if (!_active)
        {
            ClearOverlays();
            return;
        }

        Dictionary<EditorObjectId, List<SelectionTarget>> selectedByObject =
            BuildSelectedByObject();
        HashSet<EditorObjectId> liveObjectIds = [];

        foreach ((EditorObjectId objectId, TRMeshGD meshNode) in _scene.EnumerateMeshObjects())
        {
            if (meshNode.GetParent() == null)
            {
                continue;
            }

            liveObjectIds.Add(objectId);
            selectedByObject.TryGetValue(objectId, out List<SelectionTarget> selected);

            ComponentSelectionOverlay overlay = GetOrCreateOverlay(objectId, meshNode);
            overlay.Rebuild(
                meshNode,
                selected ?? [],
                GetHoverForObject(objectId),
                _cameraOrigin,
                _mode
            );
        }

        RemoveUnusedOverlays(liveObjectIds);
    }

    private Dictionary<EditorObjectId, List<SelectionTarget>> BuildSelectedByObject()
    {
        Dictionary<EditorObjectId, List<SelectionTarget>> selectedByObject = [];
        foreach (SelectionTarget target in _selection.Current.Targets)
        {
            if (!_mode.AllowsSelected(target))
            {
                continue;
            }

            if (!selectedByObject.TryGetValue(target.ObjectId, out List<SelectionTarget> targets))
            {
                targets = [];
                selectedByObject.Add(target.ObjectId, targets);
            }

            targets.Add(target);
        }

        return selectedByObject;
    }

    private SelectionTarget? GetHoverForObject(EditorObjectId objectId)
    {
        if (!_hover.HasValue || _hover.Value.ObjectId != objectId)
        {
            return null;
        }

        return _mode.AllowsHover(_hover.Value) ? _hover.Value : null;
    }

    private ComponentSelectionOverlay GetOrCreateOverlay(EditorObjectId objectId, TRMeshGD meshNode)
    {
        if (_overlays.TryGetValue(objectId, out ComponentSelectionOverlay overlay))
        {
            return overlay;
        }

        overlay = new ComponentSelectionOverlay { Name = "ComponentSelectionOverlay" };
        _overlays.Add(objectId, overlay);
        meshNode.AddChild(overlay);
        return overlay;
    }

    private void RemoveUnusedOverlays(HashSet<EditorObjectId> liveObjectIds)
    {
        List<EditorObjectId> unusedObjectIds = [];
        foreach (EditorObjectId objectId in _overlays.Keys)
        {
            if (!liveObjectIds.Contains(objectId))
            {
                unusedObjectIds.Add(objectId);
            }
        }

        foreach (EditorObjectId objectId in unusedObjectIds)
        {
            RemoveOverlay(objectId);
        }
    }

    private void ClearOverlays()
    {
        List<EditorObjectId> objectIds = [.. _overlays.Keys];
        foreach (EditorObjectId objectId in objectIds)
        {
            RemoveOverlay(objectId);
        }
    }

    private void RemoveOverlay(EditorObjectId objectId)
    {
        if (!_overlays.Remove(objectId, out ComponentSelectionOverlay overlay))
        {
            return;
        }

        overlay.GetParent()?.RemoveChild(overlay);
        overlay.QueueFree();
    }

}

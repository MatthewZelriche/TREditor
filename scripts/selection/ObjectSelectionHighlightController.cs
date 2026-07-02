using System;
using System.Collections.Generic;

public sealed class ObjectSelectionHighlightController : IDisposable
{
    private readonly EditorSceneService _scene;
    private readonly SelectionService _selection;
    private readonly HashSet<EditorObjectId> _highlighted = [];

    private bool _active;
    private bool _disposed;

    public ObjectSelectionHighlightController(EditorSceneService scene, SelectionService selection)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(selection);

        _scene = scene;
        _selection = selection;
        _selection.SelectionChanged += OnSelectionChanged;
    }

    public void SetActive(bool active)
    {
        if (_active == active)
        {
            return;
        }

        _active = active;
        Sync();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _selection.SelectionChanged -= OnSelectionChanged;
        Clear();
    }

    private void OnSelectionChanged(SelectionSnapshot _) => Sync();

    private void Sync()
    {
        if (!_active)
        {
            Clear();
            return;
        }

        HashSet<EditorObjectId> selectedObjectIds = GetSelectedObjectIds();
        List<EditorObjectId> highlightedObjectIds = [.. _highlighted];
        foreach (EditorObjectId objectId in highlightedObjectIds)
        {
            if (selectedObjectIds.Contains(objectId))
            {
                continue;
            }

            SetObjectHighlighted(objectId, false);
            _highlighted.Remove(objectId);
        }

        foreach (EditorObjectId objectId in selectedObjectIds)
        {
            if (!_highlighted.Add(objectId))
            {
                continue;
            }

            SetObjectHighlighted(objectId, true);
        }
    }

    private void Clear()
    {
        foreach (EditorObjectId objectId in _highlighted)
        {
            SetObjectHighlighted(objectId, false);
        }

        _highlighted.Clear();
    }

    private HashSet<EditorObjectId> GetSelectedObjectIds()
    {
        HashSet<EditorObjectId> selectedObjectIds = [];
        foreach (SelectionTarget target in _selection.Current.Targets)
        {
            if (target.Kind == ScenePickElementKind.Object)
            {
                selectedObjectIds.Add(target.ObjectId);
            }
        }

        return selectedObjectIds;
    }

    private void SetObjectHighlighted(EditorObjectId objectId, bool highlighted)
    {
        foreach ((EditorObjectId candidateId, TRMeshGD meshNode) in _scene.EnumerateMeshObjects())
        {
            if (candidateId != objectId)
            {
                continue;
            }

            meshNode.SetObjectSelected(highlighted);
            return;
        }
    }
}

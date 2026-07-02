using System;

/// <summary>
/// Coordinates authoritative model changes with view synchronization. Model insertion and view
/// attachment succeed together or roll back without disposing caller-owned meshes.
/// </summary>
public sealed class EditorObjectLifecycle
{
    private readonly EditorSceneModel _model;
    private readonly IEditorSceneView _view;

    public EditorObjectLifecycle(EditorSceneModel model, IEditorSceneView view)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(view);

        _model = model;
        _view = view;
    }

    public EditorSceneModel Model => _model;

    public bool Add(EditorObjectModel obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        if (_model.Contains(obj.Id))
            return false;

        if (!_model.Add(obj))
            return false;

        if (_view.Attach(obj))
            return true;

        _model.Remove(obj.Id);
        return false;
    }

    public EditorObjectModel Remove(EditorObjectId id)
    {
        _view.Destroy(id);
        return _model.Remove(id);
    }

    public void Clear()
    {
        _view.Clear();
        _model.Clear();
    }
}

using System;
using System.Collections.Generic;

public sealed class SceneServices
{
    private readonly IReadOnlyList<ISceneService> _services;
    private Scene? _scene;

    public SceneServices(params ISceneService[] services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var serviceCopy = new ISceneService[services.Length];
        for (int index = 0; index < services.Length; index++)
        {
            serviceCopy[index] = services[index]
                ?? throw new ArgumentException("The service list cannot contain null entries.", nameof(services));
        }

        _services = serviceCopy;
    }

    public bool IsAttached => _scene is not null;

    public void Attach(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (_scene is not null)
            throw new InvalidOperationException("Scene services are already attached to a scene.");

        _scene = scene;
        scene.ObjectAdded += OnObjectAdded;
        scene.ObjectRemoved += OnObjectRemoved;
        scene.ObjectGeometryChanged += OnObjectGeometryChanged;
        scene.ObjectTransformChanged += OnObjectTransformChanged;

        int attachedServiceCount = 0;
        try
        {
            for (; attachedServiceCount < _services.Count; attachedServiceCount++)
                _services[attachedServiceCount].Attach(scene);
        }
        catch
        {
            Unsubscribe(scene);
            _scene = null;

            for (int index = attachedServiceCount - 1; index >= 0; index--)
                _services[index].Detach();

            throw;
        }
    }

    public void Detach()
    {
        Scene? scene = _scene;
        if (scene is null)
            return;

        Unsubscribe(scene);
        _scene = null;

        foreach (ISceneService service in _services)
            service.Detach();
    }

    public void Flush()
    {
        if (_scene is null)
            return;

        foreach (ISceneService service in _services)
            service.Flush();
    }

    private void OnObjectAdded(Scene.MeshObject meshObject)
    {
        foreach (ISceneService service in _services)
            service.OnObjectAdded(meshObject);
    }

    private void OnObjectRemoved(Scene.MeshObject meshObject)
    {
        foreach (ISceneService service in _services)
            service.OnObjectRemoved(meshObject);
    }

    private void OnObjectGeometryChanged(MeshObjectId id)
    {
        foreach (ISceneService service in _services)
            service.OnObjectGeometryChanged(id);
    }

    private void OnObjectTransformChanged(MeshObjectId id)
    {
        foreach (ISceneService service in _services)
            service.OnObjectTransformChanged(id);
    }

    private void Unsubscribe(Scene scene)
    {
        scene.ObjectAdded -= OnObjectAdded;
        scene.ObjectRemoved -= OnObjectRemoved;
        scene.ObjectGeometryChanged -= OnObjectGeometryChanged;
        scene.ObjectTransformChanged -= OnObjectTransformChanged;
    }
}

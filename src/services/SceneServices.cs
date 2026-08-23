using System;
using System.Collections.Generic;

public sealed class SceneServices : IDisposable
{
    private readonly IReadOnlyList<ISceneService> _services;
    private Scene _scene;
    private bool _disposed;

    public SceneServices(Scene scene, params ISceneService[] services)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(services);

        var serviceCopy = (ISceneService[])services.Clone();
        for (int index = 0; index < serviceCopy.Length; index++)
        {
            _ = serviceCopy[index]
                ?? throw new ArgumentException(
                    "The service list cannot contain null entries.",
                    nameof(services)
                );
        }

        _scene = scene;
        _services = serviceCopy;

        try
        {
            foreach (ISceneService service in _services)
                service.SetScene(scene);

            Subscribe(scene);
            ReplayObjects(scene, _services.Count);
        }
        catch (Exception initializationException)
        {
            DisposeAfterFailure(initializationException);
            throw;
        }
    }

    public Scene CurrentScene => _scene;

    public void SetScene(Scene scene)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(scene);
        if (ReferenceEquals(_scene, scene))
            return;

        // A partial transition cannot be recovered reliably across arbitrary services.
        // Any failure permanently disposes the complete collection.
        Unsubscribe(_scene);

        try
        {
            foreach (ISceneService service in _services)
                service.SetScene(scene);

            _scene = scene;
            Subscribe(scene);
            ReplayObjects(scene, _services.Count);
        }
        catch (Exception transitionException)
        {
            DisposeAfterFailure(transitionException);
            throw;
        }
    }

    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        foreach (ISceneService service in _services)
            service.Flush();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Unsubscribe(_scene);

        List<Exception>? disposalExceptions = null;
        foreach (ISceneService service in _services)
        {
            try
            {
                service.Dispose();
            }
            catch (Exception exception)
            {
                disposalExceptions ??= [];
                disposalExceptions.Add(exception);
            }
        }

        if (disposalExceptions is not null)
        {
            throw new AggregateException(
                "One or more scene services could not be disposed.",
                disposalExceptions
            );
        }
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

    private void ReplayObjects(Scene scene, int serviceCount)
    {
        foreach (Scene.MeshObject meshObject in scene.Objects.Values)
        {
            for (int index = 0; index < serviceCount; index++)
                _services[index].OnObjectAdded(meshObject);
        }
    }

    private void Subscribe(Scene scene)
    {
        scene.ObjectAdded += OnObjectAdded;
        scene.ObjectRemoved += OnObjectRemoved;
        scene.ObjectGeometryChanged += OnObjectGeometryChanged;
        scene.ObjectTransformChanged += OnObjectTransformChanged;
    }

    private void Unsubscribe(Scene scene)
    {
        scene.ObjectAdded -= OnObjectAdded;
        scene.ObjectRemoved -= OnObjectRemoved;
        scene.ObjectGeometryChanged -= OnObjectGeometryChanged;
        scene.ObjectTransformChanged -= OnObjectTransformChanged;
    }

    private void DisposeAfterFailure(Exception originalException)
    {
        try
        {
            Dispose();
        }
        catch (Exception disposalException)
        {
            throw new AggregateException(
                "Scene services failed and could not be completely disposed.",
                originalException,
                disposalException
            );
        }
    }
}

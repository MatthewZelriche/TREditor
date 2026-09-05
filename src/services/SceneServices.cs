using System;
using System.Collections.Generic;

public sealed class SceneServices : IDisposable
{
    private readonly IReadOnlyList<ISceneService> _services;
    private readonly PreviewState _previews;
    private Scene _scene;
    private Exception? _fatalException;
    private bool _disposed;

    // Preview state is named explicitly because it is a second source of transform notifications,
    // not just another subscriber. It is also driven as an ordinary service, first in the list, so
    // it discards previews before the services that render them react.
    public SceneServices(Scene scene, PreviewState previews, params ISceneService[] services)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(previews);
        ArgumentNullException.ThrowIfNull(services);

        var serviceCopy = new ISceneService[services.Length + 1];
        serviceCopy[0] = previews;
        for (int index = 0; index < services.Length; index++)
        {
            serviceCopy[index + 1] = services[index]
                ?? throw new ArgumentException(
                    "The service list cannot contain null entries.",
                    nameof(services)
                );
        }

        _scene = scene;
        _previews = previews;
        _services = serviceCopy;
        _previews.TransformChanged += OnObjectTransformChanged;

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

    // A service callback can fail after the document mutation that triggered it has already
    // completed. Such a failure must not escape back through the document and make the command
    // appear to have failed. The collection instead becomes permanently faulted and reports the
    // first failure to its owner.
    public event Action<string, Exception>? FatalError;

    public bool IsFaulted => _fatalException is not null;

    public void SetScene(Scene scene)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(scene);
        if (IsFaulted)
            return;

        if (ReferenceEquals(_scene, scene))
            return;

        // Once transition begins, the new scene is the authoritative document even if one of its
        // derived services cannot follow it. A failure is fatal for the application, but it does
        // not escape this boundary or roll the document transition back.
        Unsubscribe(_scene);
        _scene = scene;
        if (
            !RunServiceOperation("replacing the scene", () =>
            {
                foreach (ISceneService service in _services)
                    service.SetScene(scene);
            })
        )
        {
            return;
        }

        Subscribe(scene);
        RunServiceOperation("replaying the scene", () => ReplayObjects(scene, _services.Count));
    }

    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        RunServiceOperation("flushing scene services", () =>
        {
            foreach (ISceneService service in _services)
                service.Flush();
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        FatalError = null;
        _previews.TransformChanged -= OnObjectTransformChanged;
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
        RunServiceOperation("adding a scene object", () =>
        {
            foreach (ISceneService service in _services)
                service.OnObjectAdded(meshObject);
        });
    }

    private void OnObjectRemoved(Scene.MeshObject meshObject)
    {
        RunServiceOperation("removing a scene object", () =>
        {
            foreach (ISceneService service in _services)
                service.OnObjectRemoved(meshObject);
        });
    }

    private void OnObjectGeometryChanged(MeshObjectId id)
    {
        RunServiceOperation("updating scene geometry", () =>
        {
            foreach (ISceneService service in _services)
                service.OnObjectGeometryChanged(id);
        });
    }

    private void OnObjectTransformChanged(MeshObjectId id)
    {
        RunServiceOperation("updating a scene transform", () =>
        {
            foreach (ISceneService service in _services)
                service.OnObjectTransformChanged(id);
        });
    }

    private bool RunServiceOperation(string operation, Action action)
    {
        if (IsFaulted)
            return false;

        try
        {
            action();
            return true;
        }
        catch (Exception exception)
        {
            _fatalException = exception;
            FatalError?.Invoke(operation, exception);
            return false;
        }
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

using System;
using System.Collections.Generic;

public sealed class SceneServices : IDisposable
{
    private readonly IReadOnlyList<ISceneService> _services;
    private readonly PreviewState _previews;
    private Document _document;
    private Exception? _fatalException;
    private bool _disposed;

    // Preview state is named explicitly because it is a second source of transform notifications,
    // not just another subscriber. It is also driven as an ordinary service, first in the list, so
    // it discards previews before the services that render them react.
    public SceneServices(Document document, PreviewState previews, params ISceneService[] services)
    {
        ArgumentNullException.ThrowIfNull(document);
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

        _document = document;
        _previews = previews;
        _services = serviceCopy;
        _previews.TransformChanged += OnObjectTransformChanged;

        try
        {
            foreach (ISceneService service in _services)
                service.SetDocument(document);

            Subscribe(document);
            ReplayObjects(document, _services.Count);
        }
        catch (Exception initializationException)
        {
            DisposeAfterFailure(initializationException);
            throw;
        }
    }

    public Document CurrentDocument => _document;

    // A service callback can fail after the document mutation that triggered it has already
    // completed. Such a failure must not escape back through the document and make the command
    // appear to have failed. The collection instead becomes permanently faulted and reports the
    // first failure to its owner.
    public event Action<string, Exception>? FatalError;

    public bool IsFaulted => _fatalException is not null;

    public void SetDocument(Document document)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(document);
        if (IsFaulted)
            return;

        if (ReferenceEquals(_document, document))
            return;

        // Once transition begins, the new document is authoritative even if one of its derived
        // services cannot follow it. A failure is fatal for the application, but it does not escape
        // this boundary or roll the document transition back.
        Unsubscribe(_document);
        _document = document;
        if (
            !RunServiceOperation("replacing the document", () =>
            {
                foreach (ISceneService service in _services)
                    service.SetDocument(document);
            })
        )
        {
            return;
        }

        Subscribe(document);
        RunServiceOperation("replaying the document", () => ReplayObjects(document, _services.Count));
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
        Unsubscribe(_document);

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

    private void OnObjectAdded(MeshObject meshObject)
    {
        RunServiceOperation("adding a document object", () =>
        {
            foreach (ISceneService service in _services)
                service.OnObjectAdded(meshObject);
        });
    }

    private void OnObjectRemoved(MeshObject meshObject)
    {
        RunServiceOperation("removing a document object", () =>
        {
            foreach (ISceneService service in _services)
                service.OnObjectRemoved(meshObject);
        });
    }

    private void OnObjectGeometryChanged(MeshObjectId id)
    {
        RunServiceOperation("updating document geometry", () =>
        {
            foreach (ISceneService service in _services)
                service.OnObjectGeometryChanged(id);
        });
    }

    private void OnObjectTransformChanged(MeshObjectId id)
    {
        RunServiceOperation("updating a document transform", () =>
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

    private void ReplayObjects(Document document, int serviceCount)
    {
        foreach (MeshObject meshObject in document.GetObjects())
        {
            for (int index = 0; index < serviceCount; index++)
                _services[index].OnObjectAdded(meshObject);
        }
    }

    private void Subscribe(Document document)
    {
        document.ObjectAdded += OnObjectAdded;
        document.ObjectRemoved += OnObjectRemoved;
        document.ObjectGeometryChanged += OnObjectGeometryChanged;
        document.ObjectTransformChanged += OnObjectTransformChanged;
    }

    private void Unsubscribe(Document document)
    {
        document.ObjectAdded -= OnObjectAdded;
        document.ObjectRemoved -= OnObjectRemoved;
        document.ObjectGeometryChanged -= OnObjectGeometryChanged;
        document.ObjectTransformChanged -= OnObjectTransformChanged;
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

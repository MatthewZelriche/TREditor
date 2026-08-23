using System;
using Godot;

public sealed class Application : IDisposable
{
    private readonly SceneServices _services;
    private Scene? _currentScene;
    private string? _currentFilePath;
    private bool _disposed;

    public Application(Node3D mapRoot)
    {
        ArgumentNullException.ThrowIfNull(mapRoot);

        _services = new SceneServices();
        NewScene();
    }

    public Scene? CurrentScene => _currentScene;

    public string? CurrentFilePath => _currentFilePath;

    public void NewScene()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ReplaceScene(new Scene(), null);
    }

    public void OpenScene(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        DetachAndDisposeCurrentScene();
        Scene loadedScene = SceneSerializer.Load(path);
        AttachScene(loadedScene, path);
    }

    public void SaveScene()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Scene scene = _currentScene
            ?? throw new InvalidOperationException("There is no scene to save.");
        string path = _currentFilePath
            ?? throw new InvalidOperationException(
                "The current scene has no file path. Use SaveSceneAs first."
            );

        SceneSerializer.Save(scene, path);
    }

    public void SaveSceneAs(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Scene scene = _currentScene
            ?? throw new InvalidOperationException("There is no scene to save.");

        SceneSerializer.Save(scene, path);
        _currentFilePath = path;
    }

    public void Update(double delta)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _ = delta;
        _services.Flush();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        DetachAndDisposeCurrentScene();
    }

    private void ReplaceScene(Scene scene, string? path)
    {
        try
        {
            DetachAndDisposeCurrentScene();
        }
        catch
        {
            scene.Dispose();
            throw;
        }

        AttachScene(scene, path);
    }

    private void AttachScene(Scene scene, string? path)
    {
        try
        {
            _services.Attach(scene);
        }
        catch
        {
            scene.Dispose();
            throw;
        }

        _currentScene = scene;
        _currentFilePath = path;
    }

    private void DetachAndDisposeCurrentScene()
    {
        try
        {
            _services.Detach();
        }
        finally
        {
            _currentScene?.Dispose();
            _currentScene = null;
            _currentFilePath = null;
        }
    }
}

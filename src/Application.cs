using System;
using Godot;
using TRMesh.Mesh;
using NumericsVector3 = System.Numerics.Vector3;

public sealed class Application : IDisposable
{
    private readonly SceneServices _services;
    private Scene? _currentScene;
    private string? _currentFilePath;
    private bool _disposed;

    public Application(Node3D mapRoot)
    {
        ArgumentNullException.ThrowIfNull(mapRoot);
        var initialScene = new Scene();
        RenderService? renderService = null;
        PickingService? pickingService = null;
        try
        {
            renderService = new RenderService(initialScene, mapRoot);
            pickingService = new PickingService(initialScene, mapRoot);
            _services = new SceneServices(
                initialScene,
                renderService,
                pickingService
            );
        }
        catch
        {
            pickingService?.Dispose();
            renderService?.Dispose();
            initialScene.Dispose();
            throw;
        }

        _currentScene = initialScene;
        SetUpSceneForDebug();
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

        Scene loadedScene = SceneSerializer.Load(path);
        ReplaceScene(loadedScene, path);
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
        try
        {
            _services.Dispose();
        }
        finally
        {
            _currentScene?.Dispose();
            _currentScene = null;
            _currentFilePath = null;
        }
    }

    private void ReplaceScene(Scene scene, string? path)
    {
        Scene? previousScene = _currentScene;
        try
        {
            _services.SetScene(scene);
        }
        catch
        {
            scene.Dispose();
            throw;
        }

        _currentScene = scene;
        _currentFilePath = path;
        previousScene?.Dispose();
    }

    // TODO: Delete this when Primitive Creation is hooked up
    private void SetUpSceneForDebug()
    {
        Scene scene = _currentScene
            ?? throw new InvalidOperationException("A scene must exist before adding debug geometry.");
        var cubeTransform = new Transform3D(
            Basis.Identity,
            new Vector3(-1.5f, 0.0f, 0.0f)
        );
        var cylinderTransform = new Transform3D(
            Basis.Identity,
            new Vector3(1.5f, 0.0f, 0.0f)
        );

        scene.Add(
            "Debug Cube",
            cubeTransform,
            new GDMesh(
                SpatialMeshBuilder.CreateAabb(
                    new NumericsVector3(-1.0f, -1.0f, -1.0f),
                    new NumericsVector3(2.0f, 2.0f, 2.0f)
                )
            )
        );
        scene.Add(
            "Debug Cylinder",
            cylinderTransform,
            new GDMesh(SpatialMeshBuilder.CreateCylinder(2.5f, 1.75f, 16))
        );
    }
}

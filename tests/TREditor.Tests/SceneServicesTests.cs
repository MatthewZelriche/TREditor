using Godot;

namespace TREditor.Tests;

public sealed class SceneServicesTests
{
    [Fact]
    public void FansOutLifecycleAndSceneEventsInRegistrationOrder()
    {
        using var scene = new Scene();
        var calls = new List<string>();
        var first = new RecordingSceneService("first", calls);
        var second = new RecordingSceneService("second", calls);
        using var services = new SceneServices(scene, new PreviewState(), first, second);

        Assert.Same(scene, first.Scene);
        Assert.Same(scene, second.Scene);
        Scene.MeshObject meshObject = scene.Add("Object", Transform3D.Identity, null!);
        scene.SetTransform(
            meshObject.Id,
            new Transform3D(Basis.Identity, new Vector3(1, 2, 3))
        );
        scene.MarkGeometryChanged(meshObject.Id);
        services.Flush();
        scene.Remove(meshObject.Id);
        services.Dispose();

        Assert.Equal(
            [
                "first:set-scene",
                "second:set-scene",
                $"first:add:{meshObject.Id.Value}",
                $"second:add:{meshObject.Id.Value}",
                $"first:transform:{meshObject.Id.Value}",
                $"second:transform:{meshObject.Id.Value}",
                $"first:geometry:{meshObject.Id.Value}",
                $"second:geometry:{meshObject.Id.Value}",
                "first:flush",
                "second:flush",
                $"first:remove:{meshObject.Id.Value}",
                $"second:remove:{meshObject.Id.Value}",
                "first:dispose",
                "second:dispose",
            ],
            calls
        );
    }

    [Fact]
    public void DisposeUnsubscribesFromTheScene()
    {
        using var scene = new Scene();
        var calls = new List<string>();
        var service = new RecordingSceneService("service", calls);
        var services = new SceneServices(scene, new PreviewState(), service);
        services.Dispose();
        calls.Clear();

        Scene.MeshObject meshObject = scene.Add("Object", Transform3D.Identity, null!);
        scene.SetTransform(meshObject.Id, Transform3D.Identity);
        scene.MarkGeometryChanged(meshObject.Id);
        scene.Remove(meshObject.Id);

        Assert.Empty(calls);
    }

    [Fact]
    public void ConstructionReplaysExistingObjectsInRegistrationOrder()
    {
        using var scene = new Scene();
        Scene.MeshObject firstObject = scene.Add("First", Transform3D.Identity, null!);
        Scene.MeshObject secondObject = scene.Add("Second", Transform3D.Identity, null!);
        var calls = new List<string>();
        var firstService = new RecordingSceneService("first", calls);
        var secondService = new RecordingSceneService("second", calls);
        using var services = new SceneServices(
            scene,
            new PreviewState(),
            firstService,
            secondService
        );

        Assert.Equal(
            [
                "first:set-scene",
                "second:set-scene",
                $"first:add:{firstObject.Id.Value}",
                $"second:add:{firstObject.Id.Value}",
                $"first:add:{secondObject.Id.Value}",
                $"second:add:{secondObject.Id.Value}",
            ],
            calls
        );
    }

    [Fact]
    public void SetSceneUnsubscribesFromTheOldSceneAndReplaysTheNewScene()
    {
        using var oldScene = new Scene();
        using var newScene = new Scene();
        Scene.MeshObject existingObject = newScene.Add("Existing", Transform3D.Identity, null!);
        var calls = new List<string>();
        var service = new RecordingSceneService("service", calls);
        using var services = new SceneServices(oldScene, new PreviewState(), service);

        calls.Clear();
        services.SetScene(newScene);
        oldScene.Add("Old", Transform3D.Identity, null!);
        Scene.MeshObject addedObject = newScene.Add("New", Transform3D.Identity, null!);

        Assert.Equal(
            [
                "service:set-scene",
                $"service:add:{existingObject.Id.Value}",
                $"service:add:{addedObject.Id.Value}",
            ],
            calls
        );
        Assert.Same(newScene, service.Scene);
        Assert.Same(newScene, services.CurrentScene);
    }

    [Fact]
    public void SetSceneFailurePermanentlyDisposesTheServiceCollection()
    {
        using var oldScene = new Scene();
        using var newScene = new Scene();
        var calls = new List<string>();
        var first = new RecordingSceneService("first", calls);
        var failing = new RecordingSceneService("failing", calls);
        var services = new SceneServices(oldScene, new PreviewState(), first, failing);
        calls.Clear();
        failing.ThrowOnSetScene = true;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => services.SetScene(newScene)
        );

        Assert.Equal("SetScene failed.", exception.Message);
        Assert.Equal(
            [
                "first:set-scene",
                "failing:set-scene",
                "first:dispose",
                "failing:dispose",
            ],
            calls
        );

        calls.Clear();
        oldScene.Add("Old", Transform3D.Identity, null!);
        newScene.Add("New", Transform3D.Identity, null!);
        Assert.Empty(calls);
        Assert.Throws<ObjectDisposedException>(() => services.Flush());
        Assert.Throws<ObjectDisposedException>(() => services.SetScene(oldScene));
    }

    private sealed class RecordingSceneService(
        string name,
        List<string> calls
    ) : ISceneService
    {
        public Scene? Scene { get; private set; }

        public bool ThrowOnSetScene { get; set; }

        public void SetScene(Scene scene)
        {
            calls.Add($"{name}:set-scene");
            if (ThrowOnSetScene)
                throw new InvalidOperationException("SetScene failed.");

            Scene = scene;
        }

        public void Dispose()
        {
            calls.Add($"{name}:dispose");
            Scene = null;
        }

        public void OnObjectAdded(Scene.MeshObject meshObject) =>
            calls.Add($"{name}:add:{meshObject.Id.Value}");

        public void OnObjectRemoved(Scene.MeshObject meshObject) =>
            calls.Add($"{name}:remove:{meshObject.Id.Value}");

        public void OnObjectGeometryChanged(MeshObjectId id) =>
            calls.Add($"{name}:geometry:{id.Value}");

        public void OnObjectTransformChanged(MeshObjectId id) =>
            calls.Add($"{name}:transform:{id.Value}");

        public void Flush() => calls.Add($"{name}:flush");
    }
}

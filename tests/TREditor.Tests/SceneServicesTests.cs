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
        var services = new SceneServices(first, second);

        services.Attach(scene);
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
        services.Detach();

        Assert.Equal(
            [
                "first:attach",
                "second:attach",
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
                "first:detach",
                "second:detach",
            ],
            calls
        );
        Assert.False(services.IsAttached);
    }

    [Fact]
    public void DetachUnsubscribesFromTheScene()
    {
        using var scene = new Scene();
        var calls = new List<string>();
        var service = new RecordingSceneService("service", calls);
        var services = new SceneServices(service);
        services.Attach(scene);
        services.Detach();
        calls.Clear();

        Scene.MeshObject meshObject = scene.Add("Object", Transform3D.Identity, null!);
        scene.SetTransform(meshObject.Id, Transform3D.Identity);
        scene.MarkGeometryChanged(meshObject.Id);
        scene.Remove(meshObject.Id);
        services.Flush();

        Assert.Empty(calls);
    }

    [Fact]
    public void CanAttachToANewSceneAfterDetach()
    {
        using var oldScene = new Scene();
        using var newScene = new Scene();
        var calls = new List<string>();
        var service = new RecordingSceneService("service", calls);
        var services = new SceneServices(service);
        services.Attach(oldScene);
        services.Detach();
        services.Attach(newScene);
        calls.Clear();

        oldScene.Add("Old", Transform3D.Identity, null!);
        newScene.Add("New", Transform3D.Identity, null!);

        Assert.Equal(["service:add:0"], calls);
        Assert.Same(newScene, service.Scene);

        services.Detach();
    }

    private sealed class RecordingSceneService(
        string name,
        List<string> calls
    ) : ISceneService
    {
        public Scene? Scene { get; private set; }

        public void Attach(Scene scene)
        {
            Scene = scene;
            calls.Add($"{name}:attach");
        }

        public void Detach()
        {
            calls.Add($"{name}:detach");
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

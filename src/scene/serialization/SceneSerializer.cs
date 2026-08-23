using System;
using System.IO;
using Godot;

public sealed class SceneSerializer
{
    public static void Save(Scene scene, string path)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var resource = new SceneResource();
        foreach (Scene.MeshObject meshObject in scene.Objects.Values)
        {
            if (meshObject.Mesh is null)
            {
                throw new InvalidOperationException(
                    $"Scene object {meshObject.Id} ('{meshObject.Name}') has no mesh to serialize."
                );
            }

            resource.Objects.Add(
                new SceneObjectResource
                {
                    Name = meshObject.Name,
                    Transform = meshObject.Transform,
                    Mesh = meshObject.Mesh,
                }
            );
        }

        Error error = ResourceSaver.Save(resource, path);
        if (error != Error.Ok)
            throw new IOException($"Could not save scene to '{path}': {error}.");
    }

    public static Scene Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        SceneResource resource = ResourceLoader.Load<SceneResource>(
            path,
            cacheMode: ResourceLoader.CacheMode.Ignore
        ) ?? throw new FileNotFoundException($"Could not load a scene resource from '{path}'.", path);

        if (resource.FormatVersion != SceneResource.CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"Unsupported scene format version {resource.FormatVersion}; "
                    + $"expected {SceneResource.CurrentFormatVersion}."
            );
        }

        var scene = new Scene();
        try
        {
            foreach (SceneObjectResource sceneObject in resource.Objects)
            {
                if (sceneObject is null)
                    throw new InvalidDataException("The scene resource contains a null object entry.");

                if (sceneObject.Mesh is null)
                {
                    throw new InvalidDataException(
                        $"Scene object '{sceneObject.Name}' does not contain a mesh."
                    );
                }

                scene.Add(sceneObject.Name, sceneObject.Transform, sceneObject.Mesh);
            }

            return scene;
        }
        catch
        {
            scene.Dispose();
            throw;
        }
    }
}

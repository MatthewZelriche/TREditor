using System;
using System.IO;
using Godot;

public sealed class SceneSerializer
{
    public static void Save(Document document, string path)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var resource = new SceneResource();
        foreach (MeshObject meshObject in document.GetObjects())
        {
            if (meshObject.Mesh is null)
            {
                throw new InvalidOperationException(
                    $"Document object {meshObject.Id} ('{meshObject.Name}') has no mesh to serialize."
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

    public static Document Load(string path)
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

        var document = new Document();
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

                document.Add(sceneObject.Name, sceneObject.Transform, sceneObject.Mesh);
            }

            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }
}

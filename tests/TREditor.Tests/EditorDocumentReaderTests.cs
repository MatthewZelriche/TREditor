using System.Collections;
using System.Linq;
using System.Reflection;
using Godot;

namespace TREditor.Tests;

public class EditorDocumentReaderTests
{
    [Fact]
    public void ReadTransformsAreCopiesThatDoNotFollowLaterSceneChanges()
    {
        using var scene = new Scene();
        IEditorDocumentReader reader = scene;
        var start = new Transform3D(Basis.Identity, new Vector3(1, 2, 3));
        Scene.MeshObject added = scene.Add("Cube", start, null!);

        Assert.True(reader.TryGetMeshTransform(added.Id, out Transform3D transform));
        Assert.True(scene.SetTransform(added.Id, new Transform3D(Basis.Identity, new Vector3(9, 9, 9))));

        Assert.Equal(start, transform);
    }

    [Fact]
    public void UnknownIdReturnsFalseAndADefaultTransform()
    {
        using var scene = new Scene();
        IEditorDocumentReader reader = scene;

        Assert.False(reader.TryGetMeshTransform(new MeshObjectId(42), out Transform3D transform));
        Assert.Equal(default, transform);
    }

    [Fact]
    public void ObjectIdsAreACopyThatDoesNotFollowLaterSceneChanges()
    {
        using var scene = new Scene();
        IEditorDocumentReader reader = scene;
        Scene.MeshObject first = scene.Add("First", Transform3D.Identity, null!);

        var ids = reader.GetObjectIds();
        scene.Add("Second", Transform3D.Identity, null!);

        Assert.Equal([first.Id], ids);
        Assert.Equal(2, reader.ObjectCount);
    }

    [Fact]
    public void ReadInterfaceExposesNoMutableModelOrMeshTypes()
    {
        MethodInfo[] methods = typeof(IEditorDocumentReader).GetMethods();
        var types = methods
            .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType)
                .Append(method.ReturnType))
            .ToList();

        foreach (System.Type type in types)
        {
            System.Type candidate = type.IsByRef ? type.GetElementType()! : type;
            Assert.False(IsForbidden(candidate), $"{candidate} must not cross the read boundary.");
            foreach (System.Type argument in candidate.GetGenericArguments())
                Assert.False(IsForbidden(argument), $"{argument} must not cross the read boundary.");
        }
    }

    private static bool IsForbidden(System.Type type) =>
        type == typeof(Scene)
        || type == typeof(Scene.MeshObject)
        || type == typeof(GDMesh)
        || type.FullName == "TRMesh.Mesh.SpatialMesh"
        || (typeof(IEnumerable).IsAssignableFrom(type)
            && type.GetGenericArguments().Any(argument =>
                argument == typeof(Scene.MeshObject) || argument == typeof(GDMesh)));
}

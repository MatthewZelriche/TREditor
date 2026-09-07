using System.Collections;
using System.Linq;
using System.Reflection;
using Godot;

namespace TREditor.Tests;

public class EditorDocumentReaderTests
{
    [Fact]
    public void ReadTransformsAreCopiesThatDoNotFollowLaterDocumentChanges()
    {
        using var document = new Document();
        IEditorDocumentReader reader = document;
        var start = new Transform3D(Basis.Identity, new Vector3(1, 2, 3));
        MeshObject added = document.Add("Cube", start, null!);

        Assert.True(reader.TryGetMeshTransform(added.Id, out Transform3D transform));
        Assert.True(
            document.ReplaceObject(
                added,
                added with { Transform = new Transform3D(Basis.Identity, new Vector3(9, 9, 9)) }
            )
        );

        Assert.Equal(start, transform);
    }

    [Fact]
    public void TryGetObjectReturnsTheCurrentRecord()
    {
        using var document = new Document();
        IEditorDocumentReader reader = document;
        MeshObject added = document.Add("Cube", Transform3D.Identity, null!);
        MeshObject replacement = added with
        {
            Transform = new Transform3D(Basis.Identity, new Vector3(2, 3, 4)),
        };
        Assert.True(document.ReplaceObject(added, replacement));

        Assert.True(reader.TryGetObject(added.Id, out MeshObject current));
        Assert.Same(replacement, current);
        Assert.False(reader.TryGetObject(new MeshObjectId(99), out _));
    }

    [Fact]
    public void UnknownIdReturnsFalseAndADefaultTransform()
    {
        using var document = new Document();
        IEditorDocumentReader reader = document;

        Assert.False(reader.TryGetMeshTransform(new MeshObjectId(42), out Transform3D transform));
        Assert.Equal(default, transform);
    }

    [Fact]
    public void ObjectIdsAreACopyThatDoesNotFollowLaterDocumentChanges()
    {
        using var document = new Document();
        IEditorDocumentReader reader = document;
        MeshObject first = document.Add("First", Transform3D.Identity, null!);

        var ids = reader.GetObjectIds();
        document.Add("Second", Transform3D.Identity, null!);

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
        type == typeof(Document)
        || type == typeof(GDMesh)
        || type.FullName == "TRMesh.Mesh.SpatialMesh"
        || (typeof(IEnumerable).IsAssignableFrom(type)
            && type.GetGenericArguments().Any(argument => argument == typeof(GDMesh)));
}

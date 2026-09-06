using System.Reflection;
using Godot;

namespace TREditor.Tests;

public class DocumentTests
{
    [Fact]
    public void AddAllocatesMonotonicIdsWithoutReuse()
    {
        using var document = new Document();

        MeshObject first = document.Add("First", Transform3D.Identity, null!);
        MeshObject second = document.Add("Second", Transform3D.Identity, null!);
        Assert.True(document.Remove(first.Id));
        MeshObject third = document.Add("Third", Transform3D.Identity, null!);

        Assert.Equal(new MeshObjectId(0), first.Id);
        Assert.Equal(new MeshObjectId(1), second.Id);
        Assert.Equal(new MeshObjectId(2), third.Id);
        Assert.False(document.TryGetObject(first.Id, out _));
        Assert.True(document.TryGetObject(second.Id, out MeshObject currentSecond));
        Assert.Same(second, currentSecond);
        Assert.True(document.TryGetObject(third.Id, out MeshObject currentThird));
        Assert.Same(third, currentThird);
    }

    [Fact]
    public void MutationsRaiseOneEventWithTheExpectedPayload()
    {
        using var document = new Document();
        int addedCount = 0;
        int removedCount = 0;
        int geometryChangedCount = 0;
        int transformChangedCount = 0;
        MeshObject? added = null;
        MeshObject? removed = null;
        MeshObjectId geometryChanged = default;
        MeshObjectId transformChanged = default;

        document.ObjectAdded += meshObject =>
        {
            addedCount++;
            added = meshObject;
        };
        document.ObjectRemoved += meshObject =>
        {
            removedCount++;
            removed = meshObject;
        };
        document.ObjectGeometryChanged += id =>
        {
            geometryChangedCount++;
            geometryChanged = id;
        };
        document.ObjectTransformChanged += id =>
        {
            transformChangedCount++;
            transformChanged = id;
        };

        MeshObject meshObject = document.Add("Object", Transform3D.Identity, null!);
        var transform = new Transform3D(Basis.Identity, new Vector3(1, 2, 3));
        Assert.True(document.SetTransform(meshObject.Id, transform));
        Assert.True(document.MarkGeometryChanged(meshObject.Id));
        Assert.True(document.Remove(meshObject.Id));

        Assert.Equal(1, addedCount);
        Assert.Same(meshObject, added);
        Assert.Equal(1, transformChangedCount);
        Assert.Equal(meshObject.Id, transformChanged);
        Assert.Equal(Transform3D.Identity, meshObject.Transform);
        Assert.Equal(1, geometryChangedCount);
        Assert.Equal(meshObject.Id, geometryChanged);
        Assert.Equal(1, removedCount);
        Assert.NotNull(removed);
        Assert.Equal(meshObject.Id, removed!.Id);
        Assert.Equal(transform, removed.Transform);
        Assert.NotSame(meshObject, removed);
    }

    [Fact]
    public void SetTransformDoesNotRaiseGeometryChanged()
    {
        using var document = new Document();
        MeshObject meshObject = document.Add("Object", Transform3D.Identity, null!);
        int geometryChangedCount = 0;
        int transformChangedCount = 0;
        document.ObjectGeometryChanged += _ => geometryChangedCount++;
        document.ObjectTransformChanged += _ => transformChangedCount++;

        Assert.True(
            document.SetTransform(
                meshObject.Id,
                new Transform3D(Basis.Identity, new Vector3(4, 5, 6))
            )
        );

        Assert.Equal(0, geometryChangedCount);
        Assert.Equal(1, transformChangedCount);
    }

    [Fact]
    public void RemoveUnknownIdIsNoOp()
    {
        using var document = new Document();
        int removedCount = 0;
        document.ObjectRemoved += _ => removedCount++;

        bool removed = document.Remove(new MeshObjectId(42));

        Assert.False(removed);
        Assert.Equal(0, removedCount);
        Assert.Equal(0, document.ObjectCount);
        Assert.Empty(document.GetObjects());
    }

    [Fact]
    public void PreviouslyReturnedObjectDoesNotChangeWhenReplacementIsInstalled()
    {
        using var document = new Document();
        MeshObject before = document.Add("Object", Transform3D.Identity, null!);
        var updated = new Transform3D(Basis.Identity, new Vector3(7, 8, 9));

        Assert.True(document.SetTransform(before.Id, updated));

        Assert.Equal(Transform3D.Identity, before.Transform);
        Assert.True(document.TryGetObject(before.Id, out MeshObject after));
        Assert.Equal(updated, after.Transform);
        Assert.NotSame(before, after);
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.Name, after.Name);
        Assert.Same(before.Mesh, after.Mesh);
    }

    [Fact]
    public void ObjectCollectionSnapshotsDoNotFollowLaterDocumentChanges()
    {
        using var document = new Document();
        MeshObject first = document.Add("First", Transform3D.Identity, null!);

        MeshObject[] snapshot = document.GetObjects();
        document.Add("Second", Transform3D.Identity, null!);
        Assert.True(
            document.SetTransform(
                first.Id,
                new Transform3D(Basis.Identity, new Vector3(1, 0, 0))
            )
        );

        Assert.Single(snapshot);
        Assert.Same(first, snapshot[0]);
        Assert.Equal(2, document.ObjectCount);
        Assert.Equal(Transform3D.Identity, snapshot[0].Transform);
    }

    [Fact]
    public void MeshObjectPropertiesAreInitOnlyAndWithDoesNotMutatePriorInstances()
    {
        string[] expected = ["Id", "Name", "Transform", "Mesh"];
        foreach (string propertyName in expected)
        {
            PropertyInfo property = typeof(MeshObject).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            )!;
            MethodInfo setter = property.GetSetMethod(nonPublic: true)!;
            Assert.Contains(
                setter.ReturnParameter.GetRequiredCustomModifiers(),
                modifier => modifier.Name == "IsExternalInit"
            );
        }

        var original = new MeshObject(
            new MeshObjectId(1),
            "Name",
            Transform3D.Identity,
            null!
        );
        MeshObject modified = original with
        {
            Name = "Other",
            Transform = new Transform3D(Basis.Identity, new Vector3(1, 2, 3)),
        };

        Assert.Equal("Name", original.Name);
        Assert.Equal(Transform3D.Identity, original.Transform);
        Assert.Equal("Other", modified.Name);
        Assert.NotSame(original, modified);
    }

    [Fact]
    public void GetObjectsDoesNotExposeTheLiveCollection()
    {
        using var document = new Document();
        document.Add("First", Transform3D.Identity, null!);

        MeshObject[] firstSnapshot = document.GetObjects();
        MeshObject[] secondSnapshot = document.GetObjects();

        Assert.NotSame(firstSnapshot, secondSnapshot);
        Assert.Equal(firstSnapshot, secondSnapshot);
    }

    [Fact]
    public void DisposeClearsObjectsAndRejectsFurtherMutations()
    {
        var document = new Document();
        document.Add("Object", Transform3D.Identity, null!);

        document.Dispose();
        document.Dispose();

        Assert.Equal(0, document.ObjectCount);
        Assert.Throws<ObjectDisposedException>(
            () => document.Add("Another", Transform3D.Identity, null!)
        );
        Assert.Throws<ObjectDisposedException>(() => document.GetObjects());
    }
}

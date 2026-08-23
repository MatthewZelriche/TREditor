using Godot;

namespace TREditor.Tests;

public class SceneTests
{
    [Fact]
    public void AddAllocatesMonotonicIdsWithoutReuse()
    {
        using var scene = new Scene();

        Scene.MeshObject first = scene.Add("First", Transform3D.Identity, null!);
        Scene.MeshObject second = scene.Add("Second", Transform3D.Identity, null!);
        Assert.True(scene.Remove(first.Id));
        Scene.MeshObject third = scene.Add("Third", Transform3D.Identity, null!);

        Assert.Equal(new MeshObjectId(0), first.Id);
        Assert.Equal(new MeshObjectId(1), second.Id);
        Assert.Equal(new MeshObjectId(2), third.Id);
        Assert.False(scene.Objects.ContainsKey(first.Id));
        Assert.Same(second, scene.Objects[second.Id]);
        Assert.Same(third, scene.Objects[third.Id]);
    }

    [Fact]
    public void MutationsRaiseOneEventWithTheExpectedPayload()
    {
        using var scene = new Scene();
        int addedCount = 0;
        int removedCount = 0;
        int geometryChangedCount = 0;
        int transformChangedCount = 0;
        Scene.MeshObject? added = null;
        Scene.MeshObject? removed = null;
        MeshObjectId geometryChanged = default;
        MeshObjectId transformChanged = default;

        scene.ObjectAdded += meshObject =>
        {
            addedCount++;
            added = meshObject;
        };
        scene.ObjectRemoved += meshObject =>
        {
            removedCount++;
            removed = meshObject;
        };
        scene.ObjectGeometryChanged += id =>
        {
            geometryChangedCount++;
            geometryChanged = id;
        };
        scene.ObjectTransformChanged += id =>
        {
            transformChangedCount++;
            transformChanged = id;
        };

        Scene.MeshObject meshObject = scene.Add("Object", Transform3D.Identity, null!);
        var transform = new Transform3D(Basis.Identity, new Vector3(1, 2, 3));
        Assert.True(scene.SetTransform(meshObject.Id, transform));
        Assert.True(scene.MarkGeometryChanged(meshObject.Id));
        Assert.True(scene.Remove(meshObject.Id));

        Assert.Equal(1, addedCount);
        Assert.Same(meshObject, added);
        Assert.Equal(1, transformChangedCount);
        Assert.Equal(meshObject.Id, transformChanged);
        Assert.Equal(transform, meshObject.Transform);
        Assert.Equal(1, geometryChangedCount);
        Assert.Equal(meshObject.Id, geometryChanged);
        Assert.Equal(1, removedCount);
        Assert.Same(meshObject, removed);
    }

    [Fact]
    public void SetTransformDoesNotRaiseGeometryChanged()
    {
        using var scene = new Scene();
        Scene.MeshObject meshObject = scene.Add("Object", Transform3D.Identity, null!);
        int geometryChangedCount = 0;
        int transformChangedCount = 0;
        scene.ObjectGeometryChanged += _ => geometryChangedCount++;
        scene.ObjectTransformChanged += _ => transformChangedCount++;

        Assert.True(
            scene.SetTransform(
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
        using var scene = new Scene();
        int removedCount = 0;
        scene.ObjectRemoved += _ => removedCount++;

        bool removed = scene.Remove(new MeshObjectId(42));

        Assert.False(removed);
        Assert.Equal(0, removedCount);
        Assert.Empty(scene.Objects);
    }

    [Fact]
    public void MeshObjectRejectsTransformChangesWithoutSceneGuard()
    {
        using var scene = new Scene();
        Scene.MeshObject meshObject = scene.Add("Object", Transform3D.Identity, null!);
        var transform = new Transform3D(Basis.Identity, new Vector3(7, 8, 9));

        Assert.Throws<InvalidOperationException>(
            () => meshObject.SetTransform(transform, new object())
        );
        Assert.Equal(Transform3D.Identity, meshObject.Transform);
    }

    [Fact]
    public void DisposeClearsObjectsAndRejectsFurtherMutations()
    {
        var scene = new Scene();
        scene.Add("Object", Transform3D.Identity, null!);

        scene.Dispose();
        scene.Dispose();

        Assert.Empty(scene.Objects);
        Assert.Throws<ObjectDisposedException>(
            () => scene.Add("Another", Transform3D.Identity, null!)
        );
    }
}

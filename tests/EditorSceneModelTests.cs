using Godot;
using TREditorSharp;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor2026.Tests;

public sealed class EditorSceneModelTests
{
    [Fact]
    public void Add_ReturnsTrueThenFalseOnDuplicateId_DuplicateDoesNotDisposeEitherMesh()
    {
        EditorObjectId id = EditorObjectId.New();
        SpatialMesh firstMesh = new();
        SpatialMesh secondMesh = new();
        using EditorSceneModel model = new();
        EditorObjectModel first = CreateObject(id, "First", firstMesh);
        using EditorObjectModel duplicate = CreateObject(id, "Second", secondMesh);

        Assert.True(model.Add(first));
        Assert.False(model.Add(duplicate));
        Assert.Single(model.Objects);
        Assert.Same(first, model.Objects.First());

        AssertMeshAlive(firstMesh);
        AssertMeshAlive(secondMesh);
    }

    [Fact]
    public void Remove_ReturnsOwnedObjectAndRemovesFromMap_SecondRemoveReturnsNull()
    {
        EditorObjectId id = EditorObjectId.New();
        SpatialMesh mesh = new();
        using EditorSceneModel model = new();
        EditorObjectModel obj = CreateObject(id, "Box", mesh);
        model.Add(obj);

        EditorObjectModel removed = model.Remove(id);

        Assert.Same(obj, removed);
        Assert.Empty(model.Objects);
        Assert.False(model.Contains(id));
        Assert.Null(model.Remove(id));
        AssertMeshAlive(mesh);
    }

    [Fact]
    public void Dispose_DisposesAllOwnedMeshesExactlyOnce()
    {
        SpatialMesh firstMesh = new();
        SpatialMesh secondMesh = new();
        EditorSceneModel model = new();
        model.Add(CreateObject(EditorObjectId.New(), "First", firstMesh));
        model.Add(CreateObject(EditorObjectId.New(), "Second", secondMesh));

        model.Dispose();

        AssertMeshDisposed(firstMesh);
        AssertMeshDisposed(secondMesh);

        model.Dispose();
        AssertMeshDisposed(firstMesh);
        AssertMeshDisposed(secondMesh);
    }

    [Fact]
    public void Constructor_RejectsEmptyId()
    {
        SpatialMesh mesh = new();

        Assert.Throws<ArgumentException>(
            () =>
                new EditorObjectModel(
                    new EditorObjectId(Guid.Empty),
                    "Box",
                    Transform3D.Identity,
                    mesh
                )
        );

        AssertMeshAlive(mesh);
        mesh.Dispose();
    }

    [Fact]
    public void Constructor_RejectsNonFiniteTransform()
    {
        SpatialMesh mesh = new();
        Transform3D transform = new(Basis.Identity, new Vector3(float.NaN, 0f, 0f));

        Assert.Throws<ArgumentException>(
            () => new EditorObjectModel(EditorObjectId.New(), "Box", transform, mesh)
        );

        AssertMeshAlive(mesh);
        mesh.Dispose();
    }

    [Fact]
    public void SetLocalTransform_BumpsOnlyTransformRevision()
    {
        using EditorObjectModel obj = CreateObject();

        obj.SetLocalTransform(Transform3D.Identity);

        Assert.Equal(0ul, obj.GeometryRevision);
        Assert.Equal(0ul, obj.AppearanceRevision);
        Assert.Equal(1ul, obj.TransformRevision);
    }

    [Fact]
    public void MarkGeometryChanged_BumpsOnlyGeometryRevision()
    {
        using EditorObjectModel obj = CreateObject();

        obj.MarkGeometryChanged();

        Assert.Equal(1ul, obj.GeometryRevision);
        Assert.Equal(0ul, obj.AppearanceRevision);
        Assert.Equal(0ul, obj.TransformRevision);
    }

    [Fact]
    public void MarkAppearanceChanged_BumpsOnlyAppearanceRevision()
    {
        using EditorObjectModel obj = CreateObject();

        obj.MarkAppearanceChanged();

        Assert.Equal(0ul, obj.GeometryRevision);
        Assert.Equal(1ul, obj.AppearanceRevision);
        Assert.Equal(0ul, obj.TransformRevision);
    }

    [Fact]
    public void SetLocalTransform_RejectsNonFiniteTransform()
    {
        using EditorObjectModel obj = CreateObject();
        Transform3D transform = new(Basis.Identity, new Vector3(float.PositiveInfinity, 0f, 0f));

        Assert.Throws<ArgumentException>(() => obj.SetLocalTransform(transform));
        Assert.Equal(0ul, obj.TransformRevision);
    }

    [Fact]
    public void Clear_DisposesLiveObjectsButLeavesModelUsable()
    {
        SpatialMesh mesh = new();
        using EditorSceneModel model = new();
        model.Add(CreateObject(EditorObjectId.New(), "Box", mesh));

        model.Clear();

        AssertMeshDisposed(mesh);
        Assert.Equal(0, model.Count);
        Assert.True(model.Add(CreateObject(EditorObjectId.New(), "AfterClear", new SpatialMesh())));
    }

    [Fact]
    public void OperationsRejectUseAfterDispose()
    {
        SpatialMesh mesh = new();
        EditorSceneModel model = new();
        EditorObjectId id = EditorObjectId.New();
        model.Add(CreateObject(id, "Box", mesh));
        model.Dispose();

        Assert.Throws<ObjectDisposedException>(() => model.Add(CreateObject()));
        Assert.Throws<ObjectDisposedException>(() => model.Remove(id));
        Assert.Throws<ObjectDisposedException>(() => model.TryGet(id, out _));
        Assert.Throws<ObjectDisposedException>(() => model.Contains(id));
        Assert.Throws<ObjectDisposedException>(() => _ = model.Objects);
        Assert.Throws<ObjectDisposedException>(() => _ = model.Count);
        Assert.Throws<ObjectDisposedException>(() => model.Clear());
    }

    [Fact]
    public void ObjectModel_OperationsRejectUseAfterDispose()
    {
        EditorObjectModel obj = CreateObject();

        obj.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = obj.LocalTransform);
        Assert.Throws<ObjectDisposedException>(() => _ = obj.Mesh);
        Assert.Throws<ObjectDisposedException>(() => _ = obj.GeometryRevision);
        Assert.Throws<ObjectDisposedException>(() => obj.SetLocalTransform(Transform3D.Identity));
        Assert.Throws<ObjectDisposedException>(() => obj.MarkGeometryChanged());
        Assert.Throws<ObjectDisposedException>(() => obj.MarkAppearanceChanged());

        obj.Dispose();
    }

    [Fact]
    public void ObjectModel_DisposeIsIdempotent()
    {
        SpatialMesh mesh = new();
        EditorObjectModel obj = CreateObject(mesh: mesh);

        obj.Dispose();
        obj.Dispose();

        AssertMeshDisposed(mesh);
    }

    [Fact]
    public void Remove_DoesNotDisposeReturnedObject()
    {
        SpatialMesh mesh = new();
        using EditorSceneModel model = new();
        EditorObjectId id = EditorObjectId.New();
        EditorObjectModel obj = CreateObject(id, "Box", mesh);
        model.Add(obj);

        EditorObjectModel removed = model.Remove(id);

        Assert.Same(obj, removed);
        AssertMeshAlive(mesh);
    }

    private static EditorObjectModel CreateObject(
        EditorObjectId? id = null,
        string name = "Box",
        SpatialMesh? mesh = null
    )
    {
        return new EditorObjectModel(
            id ?? EditorObjectId.New(),
            name,
            Transform3D.Identity,
            mesh ?? new SpatialMesh()
        );
    }

    private static void AssertMeshAlive(SpatialMesh mesh) =>
        mesh.AddVertex(new NumericsVector3(0f, 0f, 0f));

    private static void AssertMeshDisposed(SpatialMesh mesh) =>
        Assert.Throws<ObjectDisposedException>(
            () => mesh.AddVertex(new NumericsVector3(0f, 0f, 0f))
        );
}

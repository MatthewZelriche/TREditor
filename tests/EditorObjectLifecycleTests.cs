using Godot;
using TREditorSharp;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor2026.Tests;

public sealed class EditorObjectLifecycleTests
{
    [Fact]
    public void Add_DuplicateIdLeavesOwnershipWithCaller()
    {
        EditorObjectId id = EditorObjectId.New();
        SpatialMesh firstMesh = new();
        SpatialMesh secondMesh = new();
        EditorSceneModel model = new();
        FakeEditorSceneView view = new();
        EditorObjectLifecycle lifecycle = new(model, view);
        EditorObjectModel first = CreateObject(id, "First", firstMesh);
        EditorObjectModel duplicate = CreateObject(id, "Second", secondMesh);

        Assert.True(lifecycle.Add(first));
        Assert.False(lifecycle.Add(duplicate));
        Assert.Single(model.Objects);
        Assert.Equal(1, view.AttachCount);

        AssertMeshAlive(firstMesh);
        AssertMeshAlive(secondMesh);

        model.Dispose();
        duplicate.Dispose();
    }

    [Fact]
    public void Add_ViewAttachmentFailureRollsBackWithoutDisposingCallerMesh()
    {
        SpatialMesh mesh = new();
        EditorSceneModel model = new();
        FakeEditorSceneView view = new() { AllowAttach = false };
        EditorObjectLifecycle lifecycle = new(model, view);
        EditorObjectModel obj = CreateObject(mesh: mesh);

        Assert.False(lifecycle.Add(obj));
        Assert.Empty(model.Objects);
        Assert.Equal(0, view.AttachCount);
        AssertMeshAlive(mesh);
    }

    [Fact]
    public void Remove_ExcludesObjectFromModel_RestoreCreatesFreshViewAttachment()
    {
        EditorObjectId id = EditorObjectId.New();
        EditorSceneModel model = new();
        FakeEditorSceneView view = new();
        EditorObjectLifecycle lifecycle = new(model, view);
        EditorObjectModel obj = CreateObject(id, mesh: new SpatialMesh());
        lifecycle.Add(obj);

        EditorObjectModel removed = lifecycle.Remove(id);

        Assert.Same(obj, removed);
        Assert.Empty(model.Objects);
        Assert.Equal(1, view.DestroyCount);
        Assert.Empty(view.LiveNodeIds);

        Assert.True(lifecycle.Add(obj));
        Assert.Single(model.Objects);
        Assert.Equal(2, view.AttachCount);
        Assert.Contains(id, view.LiveNodeIds);

        lifecycle.Clear();
    }

    [Fact]
    public void Clear_ViewClearsBeforeModel_ModelDisposesMeshes()
    {
        SpatialMesh mesh = new();
        EditorSceneModel model = new();
        FakeEditorSceneView view = new();
        EditorObjectLifecycle lifecycle = new(model, view);
        lifecycle.Add(CreateObject(mesh: mesh));

        lifecycle.Clear();

        Assert.Empty(view.LiveNodeIds);
        Assert.Equal(0, model.Count);
        AssertMeshDisposed(mesh);
    }

    [Fact]
    public void CreateMeshObject_DuplicateAddLeavesCallerMeshAlive()
    {
        EditorObjectId id = EditorObjectId.New();
        SpatialMesh mesh = new();
        EditorSceneModel model = new();
        FakeEditorSceneView view = new();
        EditorObjectLifecycle lifecycle = new(model, view);
        EditorSceneService scene = new(lifecycle, model, view);

        Assert.True(scene.CreateMeshObject(id, mesh, "Box"));
        Assert.False(scene.CreateMeshObject(id, new SpatialMesh(), "Duplicate"));
        AssertMeshAlive(mesh);

        scene.ClearAll();
    }

    [Fact]
    public void CreateMeshObject_ViewFailureReturnsMeshToCaller()
    {
        SpatialMesh mesh = new();
        EditorSceneModel model = new();
        FakeEditorSceneView view = new() { AllowAttach = false };
        EditorObjectLifecycle lifecycle = new(model, view);
        EditorSceneService scene = new(lifecycle, model, view);

        Assert.False(scene.CreateMeshObject(EditorObjectId.New(), mesh, "Box"));
        Assert.Empty(model.Objects);
        AssertMeshAlive(mesh);

        model.Dispose();
    }

    [Fact]
    public void DetachedObjectLifecycle_RemoveRestoreAndDestroy()
    {
        EditorObjectId id = EditorObjectId.New();
        SpatialMesh mesh = new();
        EditorSceneModel model = new();
        FakeEditorSceneView view = new();
        EditorObjectLifecycle lifecycle = new(model, view);
        EditorSceneService scene = new(lifecycle, model, view);
        scene.CreateMeshObject(id, mesh, "Box");

        Assert.True(scene.RemoveMeshObject(id));
        Assert.Empty(model.Objects);
        Assert.DoesNotContain(id, view.LiveNodeIds);

        Assert.True(scene.RestoreMeshObject(id));
        Assert.Single(model.Objects);
        Assert.Equal(2, view.AttachCount);

        Assert.True(scene.RemoveMeshObject(id));
        Assert.True(scene.DestroyMeshObject(id));
        AssertMeshDisposed(mesh);
    }

    [Fact]
    public void ClearAll_DisposesDetachedObjects()
    {
        EditorObjectId id = EditorObjectId.New();
        SpatialMesh mesh = new();
        EditorSceneModel model = new();
        FakeEditorSceneView view = new();
        EditorObjectLifecycle lifecycle = new(model, view);
        EditorSceneService scene = new(lifecycle, model, view);
        scene.CreateMeshObject(id, mesh, "Box");
        scene.RemoveMeshObject(id);

        scene.ClearAll();

        AssertMeshDisposed(mesh);
        Assert.Equal(0, model.Count);
    }

    private static EditorObjectModel CreateObject(
        EditorObjectId? id = null,
        string name = "Box",
        SpatialMesh? mesh = null
    ) => new(id ?? EditorObjectId.New(), name, Transform3D.Identity, mesh ?? new SpatialMesh());

    private static void AssertMeshAlive(SpatialMesh mesh) =>
        mesh.AddVertex(new NumericsVector3(0f, 0f, 0f));

    private static void AssertMeshDisposed(SpatialMesh mesh) =>
        Assert.Throws<ObjectDisposedException>(
            () => mesh.AddVertex(new NumericsVector3(0f, 0f, 0f))
        );

    private sealed class FakeEditorSceneView : IEditorSceneView
    {
        public bool AllowAttach { get; init; } = true;
        public int AttachCount { get; private set; }
        public int DestroyCount { get; private set; }
        public HashSet<EditorObjectId> LiveNodeIds { get; } = [];

        public bool Attach(EditorObjectModel obj)
        {
            if (!AllowAttach || !LiveNodeIds.Add(obj.Id))
                return false;

            AttachCount++;
            return true;
        }

        public void Destroy(EditorObjectId id)
        {
            if (!LiveNodeIds.Remove(id))
                return;

            DestroyCount++;
        }

        public void SyncTransform(EditorObjectModel obj) { }

        public void SyncGeometry(EditorObjectId id) { }

        public void SyncRender(EditorObjectId id) { }

        public bool TryGetNode(EditorObjectId id, out TRMeshGD node)
        {
            node = null!;
            return false;
        }

        public IEnumerable<KeyValuePair<EditorObjectId, TRMeshGD>> Nodes => [];

        public void Clear()
        {
            DestroyCount += LiveNodeIds.Count;
            LiveNodeIds.Clear();
        }
    }
}

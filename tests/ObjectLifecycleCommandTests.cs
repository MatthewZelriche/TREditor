using Godot;
using TREditorSharp;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor2026.Tests;

public sealed class ObjectLifecycleCommandTests
{
    [Fact]
    public void CreateCommand_UndoRedoRecreatesViewAttachment()
    {
        CommandHarness harness = new();
        EditorObjectId id = new(Guid.NewGuid());
        CreateMeshCommand create = new(id, new SpatialMesh(), "Box");

        harness.Service.Execute(create);
        Assert.Equal(1, harness.View.AttachCount);
        Assert.Single(harness.Model.Objects);

        harness.Service.Undo();
        Assert.Empty(harness.Model.Objects);
        Assert.Equal(1, harness.View.DestroyCount);

        harness.Service.Redo();
        Assert.Single(harness.Model.Objects);
        Assert.Equal(2, harness.View.AttachCount);
    }

    [Fact]
    public void CreateCommand_UndoneThenDiscardedDisposesOwnedObjectExactlyOnce()
    {
        CommandHarness harness = new();
        EditorObjectId id = new(Guid.NewGuid());
        SpatialMesh mesh = new();
        CreateMeshCommand create = new(id, mesh, "Box");
        harness.Service.Execute(create);
        harness.Service.Undo();

        harness.Service.Execute(new NoOpCommand());

        Assert.Equal(EditorCommandState.Disposed, create.State);
        AssertMeshDisposed(mesh);
    }

    [Fact]
    public void CreateCommand_AppliedThenDiscardedLeavesObjectInModel()
    {
        CommandHarness harness = new();
        EditorObjectId id = new(Guid.NewGuid());
        SpatialMesh mesh = new();
        CreateMeshCommand create = new(id, mesh, "Box");
        harness.Service.Execute(create);

        harness.Service.ClearHistory();

        Assert.Equal(EditorCommandState.Disposed, create.State);
        Assert.True(harness.Model.Contains(id));
        mesh.AddVertex(NumericsVector3.Zero);
    }

    [Fact]
    public void CreateCommand_FailedAddDisposesOwnedObject()
    {
        CommandHarness harness = new(allowAttach: false);
        SpatialMesh mesh = new();
        CreateMeshCommand create = new(new EditorObjectId(Guid.NewGuid()), mesh, "Box");

        harness.Service.Execute(create);

        Assert.Equal(EditorCommandState.Disposed, create.State);
        Assert.Empty(harness.Model.Objects);
        AssertMeshDisposed(mesh);
        Assert.False(harness.Service.CanUndo);
    }

    [Fact]
    public void DeleteCommand_PartialMultiDeleteKeepsOnlyRemovedObjects()
    {
        CommandHarness harness = new();
        EditorObjectId firstId = new(Guid.NewGuid());
        EditorObjectId missingId = new(Guid.NewGuid());
        EditorObjectId secondId = new(Guid.NewGuid());
        harness.Lifecycle.Add(
            new EditorObjectModel(firstId, "First", Transform3D.Identity, new SpatialMesh())
        );
        harness.Lifecycle.Add(
            new EditorObjectModel(secondId, "Second", Transform3D.Identity, new SpatialMesh())
        );
        SelectionSnapshot selection = SelectionSnapshot.From(
            [
                SelectionTarget.ForObject(firstId),
                SelectionTarget.ForObject(missingId),
                SelectionTarget.ForObject(secondId),
            ]
        );
        DeleteMeshCommand delete = DeleteMeshCommand.CreateIfAny(selection)!;

        harness.Service.Execute(delete);

        Assert.Empty(harness.Model.Objects);
        harness.Service.Undo();
        Assert.Equal(2, harness.Model.Count);
    }

    [Fact]
    public void DeleteCommand_AppliedThenDiscardedDisposesRemovedObjects()
    {
        CommandHarness harness = new();
        EditorObjectId id = new(Guid.NewGuid());
        SpatialMesh mesh = new();
        harness.Lifecycle.Add(new EditorObjectModel(id, "Box", Transform3D.Identity, mesh));
        DeleteMeshCommand delete = DeleteMeshCommand.CreateIfAny(
            SelectionSnapshot.From([SelectionTarget.ForObject(id)])
        )!;
        harness.Service.Execute(delete);

        harness.Service.ClearHistory();

        Assert.Equal(EditorCommandState.Disposed, delete.State);
        AssertMeshDisposed(mesh);
    }

    [Fact]
    public void DeleteCommand_UndoneThenDiscardedLeavesRestoredObjectsInModel()
    {
        CommandHarness harness = new();
        EditorObjectId id = new(Guid.NewGuid());
        SpatialMesh mesh = new();
        harness.Lifecycle.Add(new EditorObjectModel(id, "Box", Transform3D.Identity, mesh));
        DeleteMeshCommand delete = DeleteMeshCommand.CreateIfAny(
            SelectionSnapshot.From([SelectionTarget.ForObject(id)])
        )!;
        harness.Service.Execute(delete);
        harness.Service.Undo();

        harness.Service.Execute(new NoOpCommand());

        Assert.Equal(EditorCommandState.Disposed, delete.State);
        Assert.True(harness.Model.Contains(id));
        mesh.AddVertex(NumericsVector3.Zero);
    }

    [Fact]
    public void DeleteCommand_UndoRedoRemovesAndRestoresViewAttachments()
    {
        CommandHarness harness = new();
        EditorObjectId id = new(Guid.NewGuid());
        harness.Lifecycle.Add(
            new EditorObjectModel(id, "Box", Transform3D.Identity, new SpatialMesh())
        );
        DeleteMeshCommand delete = DeleteMeshCommand.CreateIfAny(
            SelectionSnapshot.From([SelectionTarget.ForObject(id)])
        )!;
        harness.Service.Execute(delete);
        Assert.Equal(1, harness.View.DestroyCount);

        harness.Service.Undo();
        Assert.Equal(2, harness.View.AttachCount);

        harness.Service.Redo();
        Assert.Equal(2, harness.View.DestroyCount);
    }

    private static void AssertMeshDisposed(SpatialMesh mesh) =>
        Assert.Throws<ObjectDisposedException>(
            () => mesh.AddVertex(new NumericsVector3(0f, 0f, 0f))
        );

    private sealed class NoOpCommand : EditorCommand
    {
        public override string Name => "No Op";

        protected override bool Do(EditorCommandContext context) => true;

        protected override void Undo(EditorCommandContext context) { }
    }

    private sealed class CommandHarness
    {
        public CommandHarness(bool allowAttach = true)
        {
            Model = new EditorSceneModel();
            View = new FakeEditorSceneView { AllowAttach = allowAttach };
            Lifecycle = new EditorObjectLifecycle(Model, View);
            EditorMeshOperations operations = new(Model, View);
            Service = new CommandService(
                new EditorCommandContext(Lifecycle, operations, _ => false)
            );
        }

        public EditorSceneModel Model { get; }
        public FakeEditorSceneView View { get; }
        public EditorObjectLifecycle Lifecycle { get; }
        public CommandService Service { get; }
    }

    private sealed class FakeEditorSceneView : IEditorSceneView
    {
        public bool AllowAttach { get; set; } = true;
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

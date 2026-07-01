using System.Numerics;
using TREditorSharp;

namespace TREditor2026.Tests;

public sealed class CommandServiceTests
{
    [Fact]
    public void ExecuteUndoRedo_TransitionsCommandState()
    {
        using CommandService service = CreateService();
        TrackingCommand command = new();

        service.Execute(command);
        Assert.Equal(EditorCommandState.Applied, command.State);

        service.Undo();
        Assert.Equal(EditorCommandState.Undone, command.State);

        service.Redo();
        Assert.Equal(EditorCommandState.Applied, command.State);
        Assert.Equal(2, command.DoCount);
        Assert.Equal(1, command.UndoCount);
    }

    [Fact]
    public void CommandState_RejectsUndoAndRedoFromWrongSide()
    {
        using TrackingCommand command = new();
        EditorCommandContext context = CreateContext();

        Assert.Throws<InvalidOperationException>(command.ExecuteUndo);
        Assert.True(command.ExecuteInitial(context));
        Assert.Throws<InvalidOperationException>(command.ExecuteRedo);
        command.ExecuteUndo();
        command.ExecuteRedo();
        Assert.Throws<InvalidOperationException>(command.ExecuteRedo);
    }

    [Fact]
    public void FailedCommand_IsDisposedWithoutChangingHistoryOrDiscardingRedo()
    {
        using CommandService service = CreateService();
        TrackingCommand existing = new();
        service.Execute(existing);
        service.Undo();
        int changedCount = 0;
        service.CommandHistoryChanged += () => changedCount++;
        TrackingCommand failed = new(initialResult: false);

        service.Execute(failed);

        Assert.Equal(EditorCommandState.Disposed, failed.State);
        Assert.Equal(EditorCommandState.New, failed.DiscardedState);
        Assert.True(service.CanRedo);
        Assert.False(service.CanUndo);
        Assert.Equal(0, changedCount);
    }

    [Fact]
    public void NewCommand_DiscardsEntireRedoBranchExactlyOnce()
    {
        using CommandService service = CreateService();
        TrackingCommand first = new();
        TrackingCommand second = new();
        service.Execute(first);
        service.Execute(second);
        service.Undo();
        service.Undo();

        service.Execute(new TrackingCommand());

        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.DisposeCount);
        Assert.Equal(EditorCommandState.Undone, first.DiscardedState);
        Assert.Equal(EditorCommandState.Undone, second.DiscardedState);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public void HistoryCapacity_EvictsOldestAppliedCommand()
    {
        using CommandService service = CreateService();
        TrackingCommand oldest = new();
        service.Execute(oldest);

        for (int index = 0; index < CommandService.HistoryCapacity; index++)
            service.Execute(new TrackingCommand());

        Assert.Equal(EditorCommandState.Disposed, oldest.State);
        Assert.Equal(EditorCommandState.Applied, oldest.DiscardedState);

        for (int index = 0; index < CommandService.HistoryCapacity; index++)
            service.Undo();
        Assert.False(service.CanUndo);
    }

    [Fact]
    public void SelectionCommand_CountsTowardHistoryCapacity()
    {
        using CommandService service = CreateService();
        TrackingCommand selection = new(affectsDocument: false);
        service.Execute(selection);

        for (int index = 0; index < CommandService.HistoryCapacity; index++)
            service.Execute(new TrackingCommand());

        Assert.False(selection.AffectsDocument);
        Assert.Equal(EditorCommandState.Disposed, selection.State);
    }

    [Fact]
    public void SelectionCommand_NoOpDoesNotEnterHistory()
    {
        SelectionSnapshot current = SelectionSnapshot.From(
            [SelectionTarget.ForObject(new EditorObjectId(Guid.NewGuid()))]
        );
        EditorCommandContext context = new(
            new FakeObjectLifecycle(),
            selection =>
            {
                if (selection == current)
                    return false;
                current = selection;
                return true;
            }
        );
        using CommandService service = new(context);
        SetSelectionCommand command = new(SelectionSnapshot.Empty, current);

        service.Execute(command);

        Assert.Equal(EditorCommandState.Disposed, command.State);
        Assert.False(service.CanUndo);
    }

    [Fact]
    public void ClearHistory_DisposesBothSidesAndOnlyEmitsWhenHistoryChanges()
    {
        using CommandService service = CreateService();
        TrackingCommand undone = new();
        TrackingCommand applied = new();
        service.Execute(undone);
        service.Undo();
        service.Execute(applied);
        int changedCount = 0;
        service.CommandHistoryChanged += () => changedCount++;

        service.ClearHistory();
        service.ClearHistory();
        service.Undo();
        service.Redo();

        Assert.Equal(1, undone.DisposeCount);
        Assert.Equal(1, applied.DisposeCount);
        Assert.Equal(1, changedCount);
        Assert.False(service.CanUndo);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public void Dispose_ReleasesResourcesExactlyOnce()
    {
        CommandService service = CreateService();
        TrackingCommand applied = new();
        TrackingCommand undone = new();
        service.Execute(applied);
        service.Execute(undone);
        service.Undo();

        service.Dispose();
        service.Dispose();

        Assert.Equal(1, applied.DisposeCount);
        Assert.Equal(1, undone.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => service.Execute(new TrackingCommand()));
        Assert.Throws<ObjectDisposedException>(service.Undo);
        Assert.Throws<ObjectDisposedException>(service.Redo);
    }

    [Fact]
    public void ThrowingCommand_IsDisposedAndDoesNotEnterHistory()
    {
        using CommandService service = CreateService();
        TrackingCommand command = new(throwOnInitial: true);

        Assert.Throws<InvalidOperationException>(() => service.Execute(command));

        Assert.Equal(EditorCommandState.Disposed, command.State);
        Assert.False(service.CanUndo);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public void CreateCommand_UndoneThenDiscardedDestroysDetachedObject()
    {
        FakeObjectLifecycle objects = new();
        using CommandService service = CreateService(objects);
        EditorObjectId id = new(Guid.NewGuid());
        using SpatialMesh mesh = new();
        CreateMeshCommand create = new(id, mesh, "Box");
        service.Execute(create);
        service.Undo();

        service.Execute(new TrackingCommand());

        Assert.Equal([id], objects.Destroyed);
        Assert.Equal(EditorCommandState.Disposed, create.State);
        Assert.Throws<ObjectDisposedException>(() => mesh.AddVertex(Vector3.Zero));
    }

    [Fact]
    public void CreateCommand_AppliedThenDiscardedLeavesLiveObjectOwnedByScene()
    {
        FakeObjectLifecycle objects = new();
        using CommandService service = CreateService(objects);
        EditorObjectId id = new(Guid.NewGuid());
        using SpatialMesh mesh = new();
        CreateMeshCommand create = new(id, mesh, "Box");
        service.Execute(create);

        service.ClearHistory();

        Assert.Empty(objects.Destroyed);
        Assert.Equal(EditorCommandState.Disposed, create.State);
        mesh.AddVertex(Vector3.Zero);
    }

    [Fact]
    public void CreateCommand_FailureDisposesUnclaimedMesh()
    {
        FakeObjectLifecycle objects = new() { AllowCreate = false };
        using CommandService service = CreateService(objects);
        SpatialMesh mesh = new();
        CreateMeshCommand create = new(new EditorObjectId(Guid.NewGuid()), mesh, "Box");

        service.Execute(create);

        Assert.Equal(EditorCommandState.Disposed, create.State);
        Assert.Throws<ObjectDisposedException>(() => mesh.AddVertex(Vector3.Zero));
        Assert.False(service.CanUndo);
    }

    [Fact]
    public void DeleteCommand_AppliedThenDiscardedDestroysDetachedObject()
    {
        FakeObjectLifecycle objects = new();
        EditorObjectId id = new(Guid.NewGuid());
        objects.Live.Add(id);
        SelectionSnapshot selection = SelectionSnapshot.From([SelectionTarget.ForObject(id)]);
        using CommandService service = CreateService(objects);
        DeleteMeshCommand command = DeleteMeshCommand.CreateIfAny(selection)!;
        service.Execute(command);

        service.ClearHistory();

        Assert.Equal([id], objects.Destroyed);
        Assert.Equal(EditorCommandState.Disposed, command.State);
    }

    [Fact]
    public void DeleteCommand_UndoneThenDiscardedLeavesRestoredObjectOwnedByScene()
    {
        FakeObjectLifecycle objects = new();
        EditorObjectId id = new(Guid.NewGuid());
        objects.Live.Add(id);
        SelectionSnapshot selection = SelectionSnapshot.From([SelectionTarget.ForObject(id)]);
        using CommandService service = CreateService(objects);
        DeleteMeshCommand command = DeleteMeshCommand.CreateIfAny(selection)!;
        service.Execute(command);
        service.Undo();

        service.Execute(new TrackingCommand());

        Assert.Empty(objects.Destroyed);
        Assert.Contains(id, objects.Live);
        Assert.Equal(EditorCommandState.Disposed, command.State);
    }

    private static CommandService CreateService(FakeObjectLifecycle? objects = null) =>
        new(CreateContext(objects));

    private static EditorCommandContext CreateContext(FakeObjectLifecycle? objects = null) =>
        new(objects ?? new FakeObjectLifecycle(), _ => false);

    private sealed class TrackingCommand : EditorCommand
    {
        private readonly bool _initialResult;
        private readonly bool _affectsDocument;
        private readonly bool _throwOnInitial;

        public TrackingCommand(
            bool initialResult = true,
            bool affectsDocument = true,
            bool throwOnInitial = false
        )
        {
            _initialResult = initialResult;
            _affectsDocument = affectsDocument;
            _throwOnInitial = throwOnInitial;
        }

        public override string Name => "Tracking Command";

        public override bool AffectsDocument => _affectsDocument;

        public int DisposeCount { get; private set; }
        public int DoCount { get; private set; }
        public int UndoCount { get; private set; }
        public EditorCommandState? DiscardedState { get; private set; }

        protected override bool Do(EditorCommandContext context)
        {
            DoCount++;
            if (_throwOnInitial && DoCount == 1)
                throw new InvalidOperationException("Expected test failure.");
            return DoCount > 1 || _initialResult;
        }

        protected override void Undo(EditorCommandContext context)
        {
            UndoCount++;
        }

        protected override void OnDispose(
            EditorCommandContext context,
            EditorCommandState discardedState
        )
        {
            DisposeCount++;
            DiscardedState = discardedState;
        }
    }

    private sealed class FakeObjectLifecycle : IEditorObjectLifecycle
    {
        public bool AllowCreate { get; init; } = true;
        public HashSet<EditorObjectId> Live { get; } = [];
        public HashSet<EditorObjectId> Detached { get; } = [];
        public List<EditorObjectId> Destroyed { get; } = [];
        private Dictionary<EditorObjectId, SpatialMesh> Meshes { get; } = [];

        public bool CreateMeshObject(EditorObjectId objectId, SpatialMesh mesh, string displayName)
        {
            if (!AllowCreate || !Live.Add(objectId))
                return false;

            Meshes.Add(objectId, mesh);
            return true;
        }

        public bool RemoveMeshObject(EditorObjectId objectId)
        {
            if (!Live.Remove(objectId))
                return false;

            Detached.Add(objectId);
            return true;
        }

        public bool RestoreMeshObject(EditorObjectId objectId)
        {
            if (!Detached.Remove(objectId))
                return false;

            Live.Add(objectId);
            return true;
        }

        public bool DestroyMeshObject(EditorObjectId objectId)
        {
            if (!Detached.Remove(objectId))
                return false;

            if (Meshes.Remove(objectId, out SpatialMesh? mesh))
                mesh.Dispose();
            Destroyed.Add(objectId);
            return true;
        }
    }
}

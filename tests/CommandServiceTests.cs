using System.Runtime.CompilerServices;

namespace TREditor2026.Tests;

public sealed class CommandServiceTests
{
    [Fact]
    public void ClearHistory_ReleasesResourcesExactlyOnceAndRemovesUndoRedo()
    {
        FakeCommandHistory history = new();
        using CommandService service = new(CreateContext(), history);
        TrackingCommand command = CreateCommand();
        service.Execute(command);
        service.Undo();

        service.ClearHistory();
        service.ClearHistory();
        service.Undo();
        service.Redo();

        Assert.Equal(1, command.ReleaseCount);
        Assert.Equal(1, command.DoCount);
        Assert.Equal(1, command.UndoCount);
        Assert.False(service.CanUndo);
        Assert.False(service.CanRedo);
        Assert.Equal(0, history.UndoCount);
        Assert.Equal(0, history.RedoCount);
    }

    [Fact]
    public void Dispose_ReleasesResourcesExactlyOnceAndClearsHistory()
    {
        FakeCommandHistory history = new();
        CommandService service = new(CreateContext(), history);
        TrackingCommand first = CreateCommand();
        TrackingCommand second = CreateCommand();
        service.Execute(first);
        service.Execute(second);

        service.Dispose();
        service.Dispose();

        Assert.Equal(1, first.ReleaseCount);
        Assert.Equal(1, second.ReleaseCount);
        Assert.Equal(1, history.DisposeCount);
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void ClearHistory_EmitsHistoryChanged()
    {
        FakeCommandHistory history = new();
        using CommandService service = new(CreateContext(), history);
        int changedCount = 0;
        service.CommandHistoryChanged += () => changedCount++;

        service.ClearHistory();

        Assert.Equal(1, changedCount);
    }

    private static TrackingCommand CreateCommand() =>
        (TrackingCommand)RuntimeHelpers.GetUninitializedObject(typeof(TrackingCommand));

    private static EditorCommandContext CreateContext() =>
        (EditorCommandContext)RuntimeHelpers.GetUninitializedObject(typeof(EditorCommandContext));

    private sealed partial class TrackingCommand : EditorCommand
    {
        public int ReleaseCount { get; private set; }
        public int DoCount { get; private set; }
        public int UndoCount { get; private set; }

        public override string Name => "Tracking Command";

        public override void Do(EditorCommandContext context)
        {
            DoCount++;
        }

        public override void Undo(EditorCommandContext context)
        {
            UndoCount++;
        }

        protected override void OnReleaseResources()
        {
            ReleaseCount++;
        }
    }

    private sealed class FakeCommandHistory : ICommandHistory
    {
        private readonly List<EditorCommand> _undo = [];
        private readonly List<EditorCommand> _redo = [];

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public int UndoCount => _undo.Count;
        public int RedoCount => _redo.Count;
        public int DisposeCount { get; private set; }

        public void Execute(EditorCommand command)
        {
            _undo.Add(command);
            _redo.Clear();
            command.ExecuteDo();
        }

        public void Undo()
        {
            EditorCommand command = _undo[^1];
            _undo.RemoveAt(_undo.Count - 1);
            _redo.Add(command);
            command.ExecuteUndo();
        }

        public void Redo()
        {
            EditorCommand command = _redo[^1];
            _redo.RemoveAt(_redo.Count - 1);
            _undo.Add(command);
            command.ExecuteDo();
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }

        public void Dispose()
        {
            DisposeCount++;
            Clear();
        }
    }
}

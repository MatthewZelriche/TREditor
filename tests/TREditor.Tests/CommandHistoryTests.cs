namespace TREditor.Tests;

public sealed class CommandHistoryTests : IDisposable
{
    private readonly Document _document = new();

    public void Dispose() => _document.Dispose();

    [Fact]
    public void ExecuteAppliesTheCommandAndUndoRedoReplayIt()
    {
        var document = new List<string>();
        using var history = CreateHistory();
        int changedCount = 0;
        history.Changed += () => changedCount++;

        history.Execute(new AppendCommand(document, "first"));
        history.Execute(new AppendCommand(document, "second"));

        Assert.Equal(["first", "second"], document);
        Assert.Equal(2, changedCount);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal("Append second", history.UndoDisplayName);
        Assert.Null(history.RedoDisplayName);

        Assert.True(history.Undo());

        Assert.Equal(["first"], document);
        Assert.Equal(3, changedCount);
        Assert.Equal("Append first", history.UndoDisplayName);
        Assert.Equal("Append second", history.RedoDisplayName);

        Assert.True(history.Redo());

        Assert.Equal(["first", "second"], document);
        Assert.Equal(4, changedCount);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void UndoAndRedoOnAnEmptyHistoryReturnFalseWithoutNotifying()
    {
        using var history = CreateHistory();
        int changedCount = 0;
        history.Changed += () => changedCount++;

        Assert.False(history.Undo());
        Assert.False(history.Redo());

        Assert.Equal(0, changedCount);
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Null(history.UndoDisplayName);
        Assert.Null(history.RedoDisplayName);
    }

    [Fact]
    public void ExecutingAfterUndoInvalidatesAndDisposesTheRedoStack()
    {
        var log = new List<string>();
        using var history = CreateHistory();
        var undone = new TestCommand("Undone", log);
        var replacement = new TestCommand("Replacement", log);

        history.Execute(undone);
        Assert.True(history.Undo());
        history.Execute(replacement);

        Assert.Equal(
            ["execute:Undone", "undo:Undone", "execute:Replacement", "dispose:Undone"],
            log
        );
        Assert.False(history.CanRedo);
        Assert.Equal("Replacement", history.UndoDisplayName);
    }

    [Fact]
    public void FailedExecuteLeavesTheHistoryUnchangedAndTheCommandUnowned()
    {
        using var history = CreateHistory();
        var applied = new TestCommand("Applied");
        var failing = new TestCommand("Failing") { ThrowOnExecute = true };
        history.Execute(applied);
        int changedCount = 0;
        history.Changed += () => changedCount++;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => history.Execute(failing)
        );

        Assert.Equal("Failing could not be executed.", exception.Message);
        Assert.Equal(0, changedCount);
        Assert.Equal(0, failing.DisposeCount);
        Assert.False(history.CanRedo);
        Assert.Equal("Applied", history.UndoDisplayName);
    }

    [Fact]
    public void FailedUndoKeepsTheCommandOnTheUndoStack()
    {
        using var history = CreateHistory();
        var failing = new TestCommand("Failing");
        history.Execute(failing);
        failing.ThrowOnUndo = true;
        int changedCount = 0;
        history.Changed += () => changedCount++;

        Assert.Throws<InvalidOperationException>(() => history.Undo());

        Assert.Equal(0, changedCount);
        Assert.Equal("Failing", history.UndoDisplayName);
        Assert.False(history.CanRedo);

        failing.ThrowOnUndo = false;

        Assert.True(history.Undo());
        Assert.Equal(1, failing.UndoCount);
        Assert.Equal("Failing", history.RedoDisplayName);
    }

    [Fact]
    public void FailedRedoKeepsTheCommandOnTheRedoStack()
    {
        using var history = CreateHistory();
        var failing = new TestCommand("Failing");
        history.Execute(failing);
        Assert.True(history.Undo());
        failing.ThrowOnExecute = true;
        int changedCount = 0;
        history.Changed += () => changedCount++;

        Assert.Throws<InvalidOperationException>(() => history.Redo());

        Assert.Equal(0, changedCount);
        Assert.False(history.CanUndo);
        Assert.Equal("Failing", history.RedoDisplayName);

        failing.ThrowOnExecute = false;

        Assert.True(history.Redo());
        Assert.Equal(2, failing.ExecuteCount);
        Assert.Equal("Failing", history.UndoDisplayName);
    }

    [Fact]
    public void ReachingCapacityEvictsAndDisposesTheOldestCommands()
    {
        var log = new List<string>();
        using var history = CreateHistory(2);

        history.Execute(new TestCommand("First", log));
        history.Execute(new TestCommand("Second", log));
        history.Execute(new TestCommand("Third", log));

        Assert.Equal(
            ["execute:First", "execute:Second", "execute:Third", "dispose:First"],
            log
        );

        Assert.True(history.Undo());
        Assert.True(history.Undo());

        Assert.False(history.Undo());
        Assert.Equal(["undo:Third", "undo:Second"], log.GetRange(4, log.Count - 4));
        Assert.Equal("Second", history.RedoDisplayName);
    }

    [Fact]
    public void CapacityMustBePositiveAndDefaultsToOneHundred()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CommandHistory(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CommandHistory(-1));

        using var history = CreateHistory();

        Assert.Equal(100, history.Capacity);
        Assert.Equal(CommandHistory.DefaultCapacity, history.Capacity);
    }

    [Fact]
    public void ClearDisposesEveryRetainedCommandAndNotifiesOnce()
    {
        var log = new List<string>();
        using var history = CreateHistory();
        history.Execute(new TestCommand("Undoable", log));
        history.Execute(new TestCommand("Redoable", log));
        Assert.True(history.Undo());
        int changedCount = 0;
        history.Changed += () => changedCount++;

        history.Clear();

        Assert.Equal(1, changedCount);
        Assert.Equal(["dispose:Undoable", "dispose:Redoable"], log.GetRange(3, log.Count - 3));
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void ClearingAnEmptyHistoryDoesNotNotify()
    {
        using var history = CreateHistory();
        int changedCount = 0;
        history.Changed += () => changedCount++;

        history.Clear();

        Assert.Equal(0, changedCount);
    }

    [Fact]
    public void DisposeReleasesRetainedCommandsAndBlocksFurtherUse()
    {
        var log = new List<string>();
        var history = CreateHistory();
        var undoable = new TestCommand("Undoable", log);
        var redoable = new TestCommand("Redoable", log);
        history.Execute(undoable);
        history.Execute(redoable);
        Assert.True(history.Undo());
        int changedCount = 0;
        history.Changed += () => changedCount++;

        history.Dispose();
        history.Dispose();

        Assert.Equal(0, changedCount);
        Assert.Equal(1, undoable.DisposeCount);
        Assert.Equal(1, redoable.DisposeCount);
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Throws<ObjectDisposedException>(() => history.Execute(new TestCommand("Late")));
        Assert.Throws<ObjectDisposedException>(() => history.Undo());
        Assert.Throws<ObjectDisposedException>(() => history.Redo());
        Assert.Throws<ObjectDisposedException>(() => history.Clear());
    }

    [Fact]
    public void HistoryOperationsAreRejectedWhileACommandExecutes()
    {
        using var history = CreateHistory();
        var reentrant = new TestCommand("Reentrant");
        List<Exception?> reentrantExceptions = [];
        reentrant.DuringExecute = () => reentrantExceptions = CaptureReentrantOperations(history);

        history.Execute(reentrant);

        Assert.Equal(4, reentrantExceptions.Count);
        Assert.All(reentrantExceptions, exception => Assert.IsType<InvalidOperationException>(exception));
        Assert.Equal(1, reentrant.ExecuteCount);
        Assert.Equal("Reentrant", history.UndoDisplayName);
    }

    [Fact]
    public void HistoryOperationsAreRejectedWhileACommandUndoes()
    {
        using var history = CreateHistory();
        var reentrant = new TestCommand("Reentrant");
        history.Execute(reentrant);
        List<Exception?> reentrantExceptions = [];
        reentrant.DuringUndo = () => reentrantExceptions = CaptureReentrantOperations(history);

        Assert.True(history.Undo());

        Assert.Equal(4, reentrantExceptions.Count);
        Assert.All(reentrantExceptions, exception => Assert.IsType<InvalidOperationException>(exception));
        Assert.Equal(1, reentrant.UndoCount);
        Assert.Equal("Reentrant", history.RedoDisplayName);
    }

    [Fact]
    public void CommandsWithoutDisposalAreRetainedAndReplayed()
    {
        using var history = CreateHistory(1);
        var evicted = new PlainCommand("Evicted");
        var retained = new PlainCommand("Retained");

        history.Execute(evicted);
        history.Execute(retained);
        Assert.True(history.Undo());
        Assert.True(history.Redo());

        Assert.Equal(1, evicted.ExecuteCount);
        Assert.Equal(2, retained.ExecuteCount);
        Assert.Equal(1, retained.UndoCount);
        Assert.Equal("Retained", history.UndoDisplayName);
    }

    [Fact]
    public void ExecuteRejectsNullCommands()
    {
        using var history = CreateHistory();

        Assert.Throws<ArgumentNullException>(() => history.Execute(null!));
    }

    private CommandHistory CreateHistory(int capacity = CommandHistory.DefaultCapacity)
    {
        var history = new CommandHistory(capacity);
        history.SetDocument(_document);
        return history;
    }

    private static List<Exception?> CaptureReentrantOperations(CommandHistory history) =>
        [
            Record.Exception(() => history.Execute(new PlainCommand("Nested"))),
            Record.Exception(() => history.Undo()),
            Record.Exception(() => history.Redo()),
            Record.Exception(() => history.Clear()),
        ];

    private sealed class AppendCommand(List<string> document, string value) : IEditorCommand
    {
        public string DisplayName => $"Append {value}";

        public void Execute(Document _) => document.Add(value);

        public void Undo(Document _) => document.RemoveAt(document.Count - 1);
    }

    private sealed class PlainCommand(string displayName) : IEditorCommand
    {
        public string DisplayName => displayName;

        public int ExecuteCount { get; private set; }

        public int UndoCount { get; private set; }

        public void Execute(Document _) => ExecuteCount++;

        public void Undo(Document _) => UndoCount++;
    }

    private sealed class TestCommand(string displayName, List<string>? log = null)
        : IEditorCommand, IDisposable
    {
        public string DisplayName => displayName;

        public int ExecuteCount { get; private set; }

        public int UndoCount { get; private set; }

        public int DisposeCount { get; private set; }

        public bool ThrowOnExecute { get; set; }

        public bool ThrowOnUndo { get; set; }

        public Action? DuringExecute { get; set; }

        public Action? DuringUndo { get; set; }

        public void Execute(Document _)
        {
            DuringExecute?.Invoke();
            if (ThrowOnExecute)
                throw new InvalidOperationException($"{displayName} could not be executed.");

            ExecuteCount++;
            log?.Add($"execute:{displayName}");
        }

        public void Undo(Document _)
        {
            DuringUndo?.Invoke();
            if (ThrowOnUndo)
                throw new InvalidOperationException($"{displayName} could not be undone.");

            UndoCount++;
            log?.Add($"undo:{displayName}");
        }

        public void Dispose()
        {
            DisposeCount++;
            log?.Add($"dispose:{displayName}");
        }
    }
}

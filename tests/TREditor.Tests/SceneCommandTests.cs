using System;
using Godot;

namespace TREditor.Tests;

public class SceneCommandTests
{
    private static readonly Transform3D Before = new(Basis.Identity, new Vector3(0, 0, 0));
    private static readonly Transform3D After = new(Basis.Identity, new Vector3(1, 2, 3));

    [Fact]
    public void SubmitExecutesUndoesAndRedoesAgainstTheDocument()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        IEditorCommandSink sink = history;
        MeshObject target = document.Add("Movable", Before, null!);

        sink.Submit(new MoveCommand(target.Id, Before, After));
        Assert.Equal(After, ReadTransform(document, target.Id));

        Assert.True(history.Undo());
        Assert.Equal(Before, ReadTransform(document, target.Id));

        Assert.True(history.Redo());
        Assert.Equal(After, ReadTransform(document, target.Id));
    }

    [Fact]
    public void CommittingAgainstAMissingObjectFailsAndLeavesTheHistoryUnchanged()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);

        Assert.Throws<InvalidOperationException>(
            () => history.Submit(new MoveCommand(new MeshObjectId(7), Before, After))
        );

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void CommandsRunAgainstTheDocumentTheHistoryCurrentlyHolds()
    {
        using var first = new Document();
        using var second = new Document();
        using var history = new CommandHistory();
        history.SetDocument(first);
        MeshObject inFirst = first.Add("Movable", Before, null!);
        MeshObject inSecond = second.Add("Movable", Before, null!);

        history.Submit(new MoveCommand(inFirst.Id, Before, After));
        history.Clear();
        history.SetDocument(second);
        history.Submit(new MoveCommand(inSecond.Id, Before, After));

        Assert.Equal(After, ReadTransform(first, inFirst.Id));
        Assert.Equal(After, ReadTransform(second, inSecond.Id));
    }

    [Fact]
    public void OperationsWithoutADocumentFail()
    {
        using var history = new CommandHistory();

        Assert.Throws<InvalidOperationException>(
            () => history.Submit(new MoveCommand(new MeshObjectId(0), Before, After))
        );
    }

    private static Transform3D ReadTransform(Document document, MeshObjectId id)
    {
        Assert.True(document.TryGetMeshTransform(id, out Transform3D transform));
        return transform;
    }

    private sealed class MoveCommand(MeshObjectId id, Transform3D before, Transform3D after)
        : IEditorCommand
    {
        public string DisplayName => "Move";

        public void Execute(Document document) => Apply(document, after);

        public void Undo(Document document) => Apply(document, before);

        private void Apply(Document document, Transform3D transform)
        {
            if (!document.SetTransform(id, transform))
                throw new InvalidOperationException("The target object no longer exists.");
        }
    }
}

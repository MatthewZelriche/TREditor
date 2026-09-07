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
        MeshObject before = document.Add("Movable", Before, null!);
        MeshObject after = before with { Transform = After };

        sink.Submit(new MoveCommand(before, after));
        Assert.Equal(After, ReadTransform(document, before.Id));

        Assert.True(history.Undo());
        Assert.Equal(Before, ReadTransform(document, before.Id));

        Assert.True(history.Redo());
        Assert.Equal(After, ReadTransform(document, before.Id));
    }

    [Fact]
    public void CommittingAgainstAMissingObjectFailsAndLeavesTheHistoryUnchanged()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        var before = new MeshObject(new MeshObjectId(7), "Missing", Before, null!);
        MeshObject after = before with { Transform = After };

        Assert.Throws<InvalidOperationException>(
            () => history.Submit(new MoveCommand(before, after))
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
        MeshObject firstAfter = inFirst with { Transform = After };
        MeshObject secondAfter = inSecond with { Transform = After };

        history.Submit(new MoveCommand(inFirst, firstAfter));
        history.Clear();
        history.SetDocument(second);
        history.Submit(new MoveCommand(inSecond, secondAfter));

        Assert.Equal(After, ReadTransform(first, inFirst.Id));
        Assert.Equal(After, ReadTransform(second, inSecond.Id));
    }

    [Fact]
    public void OperationsWithoutADocumentFail()
    {
        using var history = new CommandHistory();
        var before = new MeshObject(new MeshObjectId(0), "Missing", Before, null!);
        MeshObject after = before with { Transform = After };

        Assert.Throws<InvalidOperationException>(
            () => history.Submit(new MoveCommand(before, after))
        );
    }

    private static Transform3D ReadTransform(Document document, MeshObjectId id)
    {
        Assert.True(document.TryGetMeshTransform(id, out Transform3D transform));
        return transform;
    }

    private sealed class MoveCommand(MeshObject before, MeshObject after) : IEditorCommand
    {
        public string DisplayName => "Move";

        public void Execute(Document document)
        {
            if (!document.ReplaceObject(before, after))
                throw new InvalidOperationException("The target object no longer exists.");
        }

        public void Undo(Document document)
        {
            if (!document.ReplaceObject(after, before))
                throw new InvalidOperationException("The target object no longer exists.");
        }
    }
}
